using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class RecruitmentService.</summary>
public class RecruitmentService : IRecruitmentService
{
    private const int MaxVisibleEncounters = 8;
    private const double RecruitDistanceMeters = 1000.0;
    private const int RecruitSuccessXp = 25;
    private const int MinimumRecruitDurationSeconds = 10;
    private static readonly TimeSpan EncounterLifetime = TimeSpan.FromHours(2);
    private static readonly TimeSpan LocalRecruiterWindow = TimeSpan.FromSeconds(90);
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly RuckRDbContext _db;
    private readonly IRealWorldParkService _parkService;
    private readonly ILocationTracker _locationTracker;
    /// <summary>Initializes a new instance of RecruitmentService.</summary>
    /// <param name="db">The db.</param>
    /// <param name="parkService">The park service.</param>
    /// <param name="locationTracker">The location tracker.</param>
    public RecruitmentService(RuckRDbContext db, IRealWorldParkService parkService, ILocationTracker locationTracker)
    {
        _db = db;
        _parkService = parkService;
        _locationTracker = locationTracker;
    }
    /// <summary>Get active recruitment encounters for a user in a radius.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="lat">The lat.</param>
    /// <param name="lng">The lng.</param>
    /// <param name="radiusMeters">The search radius in meters.</param>
    /// <returns>The operation result.</returns>
    public async Task<IReadOnlyList<PlayerEncounterDto>> GetEncountersAsync(string userId, double lat, double lng, double radiusMeters)
    {
        var now = DateTime.UtcNow;

        await CleanupExpiredEncountersAsync(now);

        var nearbyParks = await _parkService.FindNearbyParksAsync(lat, lng, radiusMeters);
        if (nearbyParks.Count == 0)
        {
            return Array.Empty<PlayerEncounterDto>();
        }

        var collectedPlayerIds = await _db.Collections
            .Where(c => c.UserId == userId)
            .Select(c => c.PlayerId)
            .ToListAsync();

        var activeEncounters = await _db.PlayerEncounters
            .Include(e => e.Player)
            .Where(e => e.UserId == userId
                && e.ExpiresAtUtc > now
                && !collectedPlayerIds.Contains(e.PlayerId))
            .AsNoTracking()
            .ToListAsync();

        var visibleActiveEncounters = activeEncounters
            .Where(e => e.Player is not null && nearbyParks.Any(park => IsNearPark(e, park)))
            .OrderBy(e => e.ExpiresAtUtc)
            .Take(MaxVisibleEncounters)
            .ToList();

        var encounters = new List<PlayerEncounterDto>(MaxVisibleEncounters);
        foreach (var activeEncounter in visibleActiveEncounters)
        {
            encounters.Add(ToEncounterDto(activeEncounter, activeEncounter.Player!, nearbyParks));
        }

        var remainingSlots = MaxVisibleEncounters - encounters.Count;
        if (remainingSlots <= 0)
        {
            return encounters;
        }

        var activeVisiblePlayerIds = visibleActiveEncounters
            .Select(e => e.PlayerId)
            .ToHashSet();

        var candidates = await _db.Players
            .Where(p => !collectedPlayerIds.Contains(p.Id)
                && !activeVisiblePlayerIds.Contains(p.Id))
            .AsNoTracking()
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return encounters;
        }

        var topCandidates = candidates
            .OrderBy(_ => Random.Shared.Next())
            .Take(remainingSlots)
            .ToList();
        foreach (var player in topCandidates)
        {
            var park = nearbyParks[Random.Shared.Next(nearbyParks.Count)];
            var parkLocation = GeometryFactory.CreatePoint(new Coordinate(park.Longitude, park.Latitude));

            var entry = await GetOrCreateEncounterAsync(userId, player, now, parkLocation, nearbyParks);
            encounters.Add(ToEncounterDto(entry, player, nearbyParks));
        }

        return encounters;
    }
    /// <summary>Start, check, or complete timed recruitment for a specific encounter.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="userPosition">The current user position.</param>
    /// <returns>The operation result.</returns>
    public async Task<RecruitmentAttemptResultDto> AttemptRecruitmentAsync(string userId, RecruitmentAttemptRequest request, GeoPosition userPosition)
    {
        var now = DateTime.UtcNow;
        var encounter = await _db.PlayerEncounters
            .FirstOrDefaultAsync(e => e.Id == request.EncounterId && e.UserId == userId && e.PlayerId == request.PlayerId);
        if (encounter is null)
        {
            return RecruitmentFailure("Encounter expired or invalid.");
        }

        if (encounter.ExpiresAtUtc <= now)
        {
            await DeleteEncounterAsync(encounter.Id);
            return RecruitmentFailure("Encounter expired or invalid.");
        }

        var encounterPos = new GeoPosition { Latitude = encounter.Latitude, Longitude = encounter.Longitude };
        var distanceMeters = GeoPosition.HaversineDistance(userPosition, encounterPos);
        if (distanceMeters > RecruitDistanceMeters)
        {
            return RecruitmentFailure("Go to the park to recruit this player.");
        }

        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlayerId);
        if (player is null)
        {
            return RecruitmentFailure("Player not found.");
        }

        var alreadyCollected = await _db.Collections.AnyAsync(c => c.UserId == userId && c.PlayerId == player.Id);
        if (alreadyCollected)
        {
            return RecruitmentFailure("You already recruited this player.");
        }

        if (encounter.RecruitmentStartedAtUtc is null || encounter.RecruitmentCompletesAtUtc is null)
        {
            var itemKind = await ConsumeRecruitmentItemAsync(userId, request.ItemKind, now);
            if (request.ItemKind != RecruitmentItemKind.None && itemKind == RecruitmentItemKind.None)
            {
                return RecruitmentFailure($"{request.ItemKind} is not available.");
            }

            StartRecruitmentSession(userId, encounter, player, itemKind, now);
            await _db.SaveChangesAsync();

            return RecruitmentInProgress(
                "Recruitment started. Stay nearby until the timer finishes.",
                encounter,
                now);
        }

        if (encounter.RecruitmentCompletesAtUtc > now)
        {
            return RecruitmentInProgress("Recruitment in progress. Stay nearby.", encounter, now);
        }

        var collection = new CollectionModel
        {
            UserId = userId,
            PlayerId = player.Id,
            CapturedAt = now,
            IsFavorite = false,
            CapturedAtPitchId = null
        };

        var profile = await GetOrCreateProfileAsync(userId);
        _db.Collections.Add(collection);
        await AwardExperienceAsync(profile, RecruitSuccessXp);
        await _db.SaveChangesAsync();
        await DeleteEncounterAsync(request.EncounterId);

        return new RecruitmentAttemptResultDto(
            true,
            100,
            "Recruitment success!",
            collection,
            Completed: true,
            BaseDurationSeconds: encounter.RecruitmentBaseDurationSeconds,
            RequiredDurationSeconds: encounter.RecruitmentRequiredDurationSeconds,
            RemainingSeconds: 0,
            CompletesAtUtc: encounter.RecruitmentCompletesAtUtc,
            LocalPlayerCount: encounter.RecruitmentLocalPlayerCount,
            ItemKind: encounter.RecruitmentItemKind,
            Boosts: BuildBoosts(encounter.RecruitmentBaseDurationSeconds, encounter.RecruitmentRequiredDurationSeconds, encounter.RecruitmentLocalPlayerCount, encounter.RecruitmentItemKind));
    }

    private async Task<UserGameProfileModel> GetOrCreateProfileAsync(string userId)
    {
        var profile = await _db.UserGameProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is not null)
        {
            return profile;
        }

        profile = new UserGameProfileModel
        {
            UserId = userId,
            Level = 1,
            Experience = 0,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.UserGameProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    private static int BaseRecruitmentSeconds(PlayerRarity rarity) => rarity switch
    {
        PlayerRarity.Common => 30,
        PlayerRarity.Uncommon => 60,
        PlayerRarity.Rare => 150,
        PlayerRarity.Epic => 300,
        PlayerRarity.Legendary => 600,
        _ => 60
    };

    private void StartRecruitmentSession(
        string userId,
        PlayerEncounterModel encounter,
        PlayerModel player,
        RecruitmentItemKind itemKind,
        DateTime now)
    {
        var baseSeconds = BaseRecruitmentSeconds(player.Rarity);
        var localPlayerCount = CountLocalRecruiters(userId, encounter);
        var requiredSeconds = CalculateRequiredRecruitmentSeconds(baseSeconds, localPlayerCount, itemKind);

        encounter.RecruitmentStartedAtUtc = now;
        encounter.RecruitmentCompletesAtUtc = now.AddSeconds(requiredSeconds);
        encounter.RecruitmentBaseDurationSeconds = baseSeconds;
        encounter.RecruitmentRequiredDurationSeconds = requiredSeconds;
        encounter.RecruitmentLocalPlayerCount = localPlayerCount;
        encounter.RecruitmentItemKind = itemKind;
    }

    private int CountLocalRecruiters(string userId, PlayerEncounterModel encounter)
    {
        var encounterPosition = new GeoPosition { Latitude = encounter.Latitude, Longitude = encounter.Longitude };
        return _locationTracker
            .GetRecentPositions(LocalRecruiterWindow)
            .Where(entry => entry.Key != userId)
            .Count(entry => GeoPosition.HaversineDistance(entry.Value.Position, encounterPosition) <= RecruitDistanceMeters);
    }

    private static int CalculateRequiredRecruitmentSeconds(int baseSeconds, int localPlayerCount, RecruitmentItemKind itemKind)
    {
        var cappedLocalCount = Math.Clamp(localPlayerCount, 0, 4);
        var localReductionPercent = cappedLocalCount switch
        {
            0 => 0,
            1 => 15,
            2 => 25,
            3 => 35,
            _ => 45
        };

        var itemReductionPercent = itemKind switch
        {
            RecruitmentItemKind.Beer when cappedLocalCount > 0 => 20,
            RecruitmentItemKind.Beer => 10,
            RecruitmentItemKind.Whiskey => 35,
            _ => 0
        };
        var flatReductionSeconds = itemKind == RecruitmentItemKind.Chips ? 20 : 0;

        var reduced = (int)Math.Ceiling(baseSeconds * (100 - localReductionPercent - itemReductionPercent) / 100.0)
            - flatReductionSeconds;
        return Math.Max(MinimumRecruitDurationSeconds, reduced);
    }

    private static RecruitmentAttemptResultDto RecruitmentFailure(string message) =>
        new(false, 0, message, null);

    private static RecruitmentAttemptResultDto RecruitmentInProgress(
        string message,
        PlayerEncounterModel encounter,
        DateTime now)
    {
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(((encounter.RecruitmentCompletesAtUtc ?? now) - now).TotalSeconds));
        return new RecruitmentAttemptResultDto(
            false,
            100,
            message,
            null,
            Completed: false,
            BaseDurationSeconds: encounter.RecruitmentBaseDurationSeconds,
            RequiredDurationSeconds: encounter.RecruitmentRequiredDurationSeconds,
            RemainingSeconds: remainingSeconds,
            CompletesAtUtc: encounter.RecruitmentCompletesAtUtc,
            LocalPlayerCount: encounter.RecruitmentLocalPlayerCount,
            ItemKind: encounter.RecruitmentItemKind,
            Boosts: BuildBoosts(encounter.RecruitmentBaseDurationSeconds, encounter.RecruitmentRequiredDurationSeconds, encounter.RecruitmentLocalPlayerCount, encounter.RecruitmentItemKind));
    }

    private static IReadOnlyList<RecruitmentBoostDto> BuildBoosts(
        int baseSeconds,
        int requiredSeconds,
        int localPlayerCount,
        RecruitmentItemKind itemKind)
    {
        var boosts = new List<RecruitmentBoostDto>();
        if (localPlayerCount > 0)
        {
            var percent = Math.Clamp(localPlayerCount, 0, 4) switch
            {
                1 => 15,
                2 => 25,
                3 => 35,
                _ => 45
            };
            boosts.Add(new RecruitmentBoostDto($"{localPlayerCount} local recruiter{(localPlayerCount == 1 ? string.Empty : "s")}", 0, percent));
        }

        if (itemKind != RecruitmentItemKind.None)
        {
            var label = itemKind switch
            {
                RecruitmentItemKind.Chips => "Chips",
                RecruitmentItemKind.Beer => "Beer",
                RecruitmentItemKind.Whiskey => "Whiskey",
                _ => itemKind.ToString()
            };
            boosts.Add(new RecruitmentBoostDto(label, Math.Max(0, baseSeconds - requiredSeconds), 0));
        }

        return boosts;
    }

    private async Task<RecruitmentItemKind> ConsumeRecruitmentItemAsync(
        string userId,
        RecruitmentItemKind requestedItem,
        DateTime now)
    {
        if (requestedItem == RecruitmentItemKind.None)
        {
            return RecruitmentItemKind.None;
        }

        await EnsureStarterRecruitmentItemsAsync(userId, now);

        var item = await _db.UserRecruitmentItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemKind == requestedItem);
        if (item is null || item.Quantity <= 0)
        {
            return RecruitmentItemKind.None;
        }

        item.Quantity--;
        item.UpdatedAtUtc = now;
        return requestedItem;
    }

    private async Task EnsureStarterRecruitmentItemsAsync(string userId, DateTime now)
    {
        var hasAnyItems = await _db.UserRecruitmentItems.AnyAsync(i => i.UserId == userId);
        if (hasAnyItems)
        {
            return;
        }

        _db.UserRecruitmentItems.AddRange(
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Chips, Quantity = 3, UpdatedAtUtc = now },
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Beer, Quantity = 2, UpdatedAtUtc = now },
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Whiskey, Quantity = 1, UpdatedAtUtc = now });
        await _db.SaveChangesAsync();
    }

    private async Task<PlayerEncounterModel> GetOrCreateEncounterAsync(
        string userId,
        PlayerModel player,
        DateTime now,
        Point parkLocation,
        IReadOnlyList<RealWorldPark> nearbyParks)
    {
        var existing = await _db.PlayerEncounters
            .FirstOrDefaultAsync(e => e.UserId == userId && e.PlayerId == player.Id && e.ExpiresAtUtc > now);
        if (existing is not null)
        {
            if (nearbyParks.Any(park => IsNearPark(existing, park)))
            {
                return existing;
            }

            _db.PlayerEncounters.Remove(existing);
        }

        var newEntry = new PlayerEncounterModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlayerId = player.Id,
            Latitude = parkLocation.Y,
            Longitude = parkLocation.X,
            ExpiresAtUtc = now.Add(EncounterLifetime),
            CreatedAtUtc = now
        };

        _db.PlayerEncounters.Add(newEntry);
        await _db.SaveChangesAsync();
        return newEntry;
    }

    private static bool IsNearPark(PlayerEncounterModel encounter, RealWorldPark park)
    {
        var encounterPosition = new GeoPosition
        {
            Latitude = encounter.Latitude,
            Longitude = encounter.Longitude
        };
        var parkPosition = new GeoPosition
        {
            Latitude = park.Latitude,
            Longitude = park.Longitude
        };

        return GeoPosition.HaversineDistance(encounterPosition, parkPosition) <= 25;
    }

    private static RealWorldPark? GetNearestPark(
        double latitude,
        double longitude,
        IReadOnlyList<RealWorldPark> nearbyParks)
    {
        if (nearbyParks.Count == 0)
        {
            return null;
        }

        var encounterPosition = new GeoPosition
        {
            Latitude = latitude,
            Longitude = longitude
        };

        return nearbyParks
            .OrderBy(park => GeoPosition.HaversineDistance(
                encounterPosition,
                new GeoPosition
                {
                    Latitude = park.Latitude,
                    Longitude = park.Longitude
                }))
            .First();
    }

    private static PlayerEncounterDto ToEncounterDto(
        PlayerEncounterModel encounter,
        PlayerModel player,
        IReadOnlyList<RealWorldPark> nearbyParks)
    {
        var encounterPark = GetNearestPark(encounter.Latitude, encounter.Longitude, nearbyParks);
        return new PlayerEncounterDto(
            encounter.Id,
            player.Id,
            player.Name,
            player.Position.ToString(),
            player.Rarity.ToString(),
            player.Level,
            encounter.Latitude,
            encounter.Longitude,
            encounter.ExpiresAtUtc,
            100,
            BaseRecruitmentSeconds(player.Rarity),
            encounterPark?.Name,
            encounterPark?.PlaceId);
    }

    private async Task DeleteEncounterAsync(Guid encounterId)
    {
        var encounter = await _db.PlayerEncounters.FirstOrDefaultAsync(e => e.Id == encounterId);
        if (encounter is null)
        {
            return;
        }

        _db.PlayerEncounters.Remove(encounter);
        await _db.SaveChangesAsync();
    }

    private async Task CleanupExpiredEncountersAsync(DateTime now)
    {
        var expired = await _db.PlayerEncounters
            .Where(e => e.ExpiresAtUtc <= now)
            .ToListAsync();
        if (expired.Count == 0)
        {
            return;
        }

        _db.PlayerEncounters.RemoveRange(expired);
        await _db.SaveChangesAsync();
    }

    private static int LevelForXp(int xp)
    {
        return Math.Clamp(1 + (xp / 100), 1, 100);
    }

    private static Task AwardExperienceAsync(UserGameProfileModel profile, int xp)
    {
        profile.Experience = Math.Max(0, profile.Experience + xp);
        profile.Level = LevelForXp(profile.Experience);
        profile.UpdatedAtUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}

