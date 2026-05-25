using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Server.Hubs;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

/// <summary>Defines the server-side class RecruitmentService.</summary>
public class RecruitmentService : IRecruitmentService
{
    private const int MaxVisibleEncounters = 8;
    private const double RecruitDistanceMeters = 5000.0;
    private const int RecruitSuccessXp = 25;
    private const int MinimumRecruitDurationSeconds = 10;
    private static readonly TimeSpan EncounterLifetime = TimeSpan.FromHours(2);
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly RuckRDbContext _db;
    private readonly IRealWorldParkService _parkService;
    private readonly IHubContext<BattleHub> _hubContext;

    /// <summary>Initializes a new instance of RecruitmentService.</summary>
    /// <param name="db">The db.</param>
    /// <param name="parkService">The park service.</param>
    /// <param name="hubContext">The hub context used to notify clients about shared encounter changes.</param>
    public RecruitmentService(
        RuckRDbContext db,
        IRealWorldParkService parkService,
        IHubContext<BattleHub> hubContext)
    {
        _db = db;
        _parkService = parkService;
        _hubContext = hubContext;
    }

    /// <summary>Get active shared recruitment encounters for a user in a radius.</summary>
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
            .Include(e => e.Participants)
            .Where(e => e.ExpiresAtUtc > now
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
            encounters.Add(ToEncounterDto(activeEncounter, activeEncounter.Player!, nearbyParks, userId));
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
            var entry = await GetOrCreateSharedEncounterAsync(userId, player, now, park);
            encounters.Add(ToEncounterDto(entry, player, nearbyParks, userId));
        }

        return encounters
            .GroupBy(e => e.EncounterId)
            .Select(g => g.First())
            .Take(MaxVisibleEncounters)
            .ToList();
    }

    /// <summary>Start, join, check, or complete timed recruitment for a specific shared encounter.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="userPosition">The current user position.</param>
    /// <returns>The operation result.</returns>
    public async Task<RecruitmentAttemptResultDto> AttemptRecruitmentAsync(string userId, RecruitmentAttemptRequest request, GeoPosition userPosition)
    {
        var now = DateTime.UtcNow;
        var encounter = await _db.PlayerEncounters
            .Include(e => e.Player)
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == request.EncounterId && e.PlayerId == request.PlayerId);
        if (encounter?.Player is null)
        {
            return await RecruitmentAlreadyCompletedOrInvalidAsync(userId, request.PlayerId);
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

        var alreadyCollected = await _db.Collections.AnyAsync(c => c.UserId == userId && c.PlayerId == encounter.PlayerId);
        if (alreadyCollected)
        {
            return RecruitmentFailure("You already recruited this player.");
        }

        var currentUserParticipant = encounter.Participants.FirstOrDefault(p => p.UserId == userId);
        var timerReady = encounter.RecruitmentCompletesAtUtc.HasValue
            && encounter.RecruitmentCompletesAtUtc.Value <= now;

        if (currentUserParticipant is null && timerReady)
        {
            return RecruitmentFailure("Recruitment is already ready to claim. Refresh the map and choose another recruit.");
        }

        if (currentUserParticipant is null)
        {
            var isStartingTimer = encounter.RecruitmentStartedAtUtc is null || encounter.RecruitmentCompletesAtUtc is null;
            var itemKind = RecruitmentItemKind.None;
            if (isStartingTimer)
            {
                itemKind = await ConsumeRecruitmentItemAsync(userId, request.ItemKind, now);
                if (request.ItemKind != RecruitmentItemKind.None && itemKind == RecruitmentItemKind.None)
                {
                    return RecruitmentFailure($"{request.ItemKind} is not available.");
                }
            }

            currentUserParticipant = new RecruitmentParticipantModel
            {
                EncounterId = encounter.Id,
                UserId = userId,
                JoinedAtUtc = now,
                Latitude = userPosition.Latitude,
                Longitude = userPosition.Longitude,
                AccuracyMeters = userPosition.Accuracy
            };

            encounter.Participants.Add(currentUserParticipant);
            _db.RecruitmentParticipants.Add(currentUserParticipant);

            if (isStartingTimer)
            {
                encounter.RecruitmentStartedAtUtc = now;
                encounter.RecruitmentItemKind = itemKind;
            }

            RecalculateSharedRecruitmentTimer(encounter, encounter.Player, now);
            await _db.SaveChangesAsync();
            await NotifyRecruitmentChangedAsync(encounter.Id);

            return RecruitmentInProgress(
                isStartingTimer
                    ? "Recruitment started. Everyone who joins before the timer finishes can claim this player."
                    : "Joined shared recruitment. Stay nearby until the timer finishes.",
                encounter,
                now,
                currentUserJoined: true);
        }

        if (!timerReady)
        {
            return RecruitmentInProgress("Recruitment in progress. Stay nearby.", encounter, now, currentUserJoined: true);
        }

        return await CompleteSharedRecruitmentAsync(userId, encounter, now);
    }

    private async Task<RecruitmentAttemptResultDto> RecruitmentAlreadyCompletedOrInvalidAsync(string userId, int playerId)
    {
        var collection = await _db.Collections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.PlayerId == playerId);
        if (collection is not null)
        {
            return new RecruitmentAttemptResultDto(
                true,
                100,
                "Recruitment already completed.",
                collection,
                Completed: true,
                AwardedUserCount: 0,
                CurrentUserJoined: true);
        }

        return RecruitmentFailure("Encounter expired or invalid.");
    }

    private async Task<RecruitmentAttemptResultDto> CompleteSharedRecruitmentAsync(
        string currentUserId,
        PlayerEncounterModel encounter,
        DateTime now)
    {
        var participantUserIds = encounter.Participants
            .Select(p => p.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (!participantUserIds.Contains(currentUserId))
        {
            return RecruitmentFailure("Join this recruitment before claiming it.");
        }

        CollectionModel? currentUserCollection = null;
        var awardedCount = 0;
        foreach (var participantUserId in participantUserIds)
        {
            var existingCollection = await _db.Collections
                .FirstOrDefaultAsync(c => c.UserId == participantUserId && c.PlayerId == encounter.PlayerId);
            if (existingCollection is not null)
            {
                if (participantUserId == currentUserId)
                {
                    currentUserCollection = existingCollection;
                }

                continue;
            }

            var collection = new CollectionModel
            {
                UserId = participantUserId,
                PlayerId = encounter.PlayerId,
                CapturedAt = now,
                IsFavorite = false,
                CapturedAtPitchId = null
            };

            _db.Collections.Add(collection);
            var profile = await GetOrCreateProfileAsync(participantUserId);
            AwardExperience(profile, RecruitSuccessXp, now);
            awardedCount++;

            if (participantUserId == currentUserId)
            {
                currentUserCollection = collection;
            }
        }

        foreach (var participant in encounter.Participants)
        {
            participant.CollectionAwardedAtUtc ??= now;
        }

        var baseSeconds = encounter.RecruitmentBaseDurationSeconds;
        var requiredSeconds = encounter.RecruitmentRequiredDurationSeconds;
        var participantCount = encounter.Participants.Count;
        var groupItem = encounter.RecruitmentItemKind;
        var boosts = BuildBoosts(baseSeconds, requiredSeconds, participantCount, groupItem);

        _db.PlayerEncounters.Remove(encounter);
        await _db.SaveChangesAsync();
        await NotifyRecruitmentRemovedAsync(encounter.Id);

        return new RecruitmentAttemptResultDto(
            true,
            100,
            awardedCount > 1
                ? $"Recruitment success! {awardedCount} teammates claimed this player."
                : "Recruitment success!",
            currentUserCollection,
            Completed: true,
            BaseDurationSeconds: baseSeconds,
            RequiredDurationSeconds: requiredSeconds,
            RemainingSeconds: 0,
            CompletesAtUtc: encounter.RecruitmentCompletesAtUtc,
            LocalPlayerCount: participantCount,
            ItemKind: groupItem,
            Boosts: boosts,
            ParticipantCount: participantCount,
            CurrentUserJoined: true,
            AwardedUserCount: awardedCount);
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

    private static void RecalculateSharedRecruitmentTimer(
        PlayerEncounterModel encounter,
        PlayerModel player,
        DateTime now)
    {
        var startedAt = encounter.RecruitmentStartedAtUtc ?? now;
        var baseSeconds = BaseRecruitmentSeconds(player.Rarity);
        var participantCount = Math.Max(1, encounter.Participants.Select(p => p.UserId).Distinct().Count());
        var requiredSeconds = CalculateRequiredRecruitmentSeconds(baseSeconds, participantCount, encounter.RecruitmentItemKind);

        encounter.RecruitmentStartedAtUtc = startedAt;
        encounter.RecruitmentCompletesAtUtc = startedAt.AddSeconds(requiredSeconds);
        encounter.RecruitmentBaseDurationSeconds = baseSeconds;
        encounter.RecruitmentRequiredDurationSeconds = requiredSeconds;
        encounter.RecruitmentLocalPlayerCount = participantCount;
    }

    private static int CalculateRequiredRecruitmentSeconds(int baseSeconds, int participantCount, RecruitmentItemKind itemKind)
    {
        var helperCount = Math.Clamp(participantCount - 1, 0, 4);
        var localReductionPercent = helperCount switch
        {
            0 => 0,
            1 => 15,
            2 => 25,
            3 => 35,
            _ => 45
        };

        var itemReductionPercent = itemKind switch
        {
            RecruitmentItemKind.Beer when helperCount > 0 => 20,
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
        DateTime now,
        bool currentUserJoined)
    {
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(((encounter.RecruitmentCompletesAtUtc ?? now) - now).TotalSeconds));
        var participantCount = Math.Max(1, encounter.Participants.Select(p => p.UserId).Distinct().Count());
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
            LocalPlayerCount: participantCount,
            ItemKind: encounter.RecruitmentItemKind,
            Boosts: BuildBoosts(encounter.RecruitmentBaseDurationSeconds, encounter.RecruitmentRequiredDurationSeconds, participantCount, encounter.RecruitmentItemKind),
            ParticipantCount: participantCount,
            CurrentUserJoined: currentUserJoined);
    }

    private static IReadOnlyList<RecruitmentBoostDto> BuildBoosts(
        int baseSeconds,
        int requiredSeconds,
        int participantCount,
        RecruitmentItemKind itemKind)
    {
        var boosts = new List<RecruitmentBoostDto>();
        var helperCount = Math.Clamp(participantCount - 1, 0, 4);
        if (helperCount > 0)
        {
            var percent = helperCount switch
            {
                1 => 15,
                2 => 25,
                3 => 35,
                _ => 45
            };
            boosts.Add(new RecruitmentBoostDto($"{participantCount} participants", 0, percent));
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

    private async Task<PlayerEncounterModel> GetOrCreateSharedEncounterAsync(
        string createdByUserId,
        PlayerModel player,
        DateTime now,
        RealWorldPark park)
    {
        var areaKey = AreaKeyForPark(park);
        var existing = await _db.PlayerEncounters
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.AreaKey == areaKey
                && e.PlayerId == player.Id
                && e.ExpiresAtUtc > now);
        if (existing is not null)
        {
            return existing;
        }

        var parkLocation = GeometryFactory.CreatePoint(new Coordinate(park.Longitude, park.Latitude));
        var newEntry = new PlayerEncounterModel
        {
            Id = Guid.NewGuid(),
            UserId = createdByUserId,
            PlayerId = player.Id,
            Latitude = parkLocation.Y,
            Longitude = parkLocation.X,
            AreaKey = areaKey,
            ParkPlaceId = park.PlaceId,
            ExpiresAtUtc = now.Add(EncounterLifetime),
            CreatedAtUtc = now
        };

        _db.PlayerEncounters.Add(newEntry);
        await _db.SaveChangesAsync();
        return newEntry;
    }

    private static string AreaKeyForPark(RealWorldPark park) =>
        !string.IsNullOrWhiteSpace(park.PlaceId)
            ? $"place:{park.PlaceId.Trim().ToLowerInvariant()}"
            : $"geo:{Math.Round(park.Latitude, 5):F5}:{Math.Round(park.Longitude, 5):F5}";

    private static bool IsNearPark(PlayerEncounterModel encounter, RealWorldPark park)
    {
        if (!string.IsNullOrWhiteSpace(encounter.ParkPlaceId)
            && string.Equals(encounter.ParkPlaceId, park.PlaceId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
        IReadOnlyList<RealWorldPark> nearbyParks,
        string currentUserId)
    {
        var encounterPark = GetNearestPark(encounter.Latitude, encounter.Longitude, nearbyParks);
        var participantCount = encounter.Participants
            .Select(p => p.UserId)
            .Distinct()
            .Count();
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
            encounter.ParkPlaceId ?? encounterPark?.PlaceId,
            participantCount,
            encounter.Participants.Any(p => p.UserId == currentUserId),
            encounter.RecruitmentItemKind,
            encounter.RecruitmentStartedAtUtc,
            encounter.RecruitmentCompletesAtUtc,
            encounter.RecruitmentRequiredDurationSeconds);
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
        await NotifyRecruitmentRemovedAsync(encounterId);
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

    private static void AwardExperience(UserGameProfileModel profile, int xp, DateTime now)
    {
        profile.Experience = Math.Max(0, profile.Experience + xp);
        profile.Level = LevelForXp(profile.Experience);
        profile.UpdatedAtUtc = now;
    }

    private async Task NotifyRecruitmentChangedAsync(Guid encounterId)
    {
        await _hubContext.Clients.All.SendAsync("RecruitmentEncounterChanged", encounterId);
    }

    private async Task NotifyRecruitmentRemovedAsync(Guid encounterId)
    {
        await _hubContext.Clients.All.SendAsync("RecruitmentEncounterRemoved", encounterId);
    }
}
