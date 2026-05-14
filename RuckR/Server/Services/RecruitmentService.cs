using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;

public class RecruitmentService : IRecruitmentService
{
    private const double RecruitDistanceMeters = 75.0;
    private const int RecruitSuccessXp = 25;
    private static readonly TimeSpan EncounterLifetime = TimeSpan.FromHours(2);
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly RuckRDbContext _db;
    private readonly IRealWorldParkService _parkService;

    public RecruitmentService(RuckRDbContext db, IRealWorldParkService parkService)
    {
        _db = db;
        _parkService = parkService;
    }

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

        var candidates = await _db.Players
            .Where(p => !collectedPlayerIds.Contains(p.Id))
            .AsNoTracking()
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return Array.Empty<PlayerEncounterDto>();
        }

        var profile = await GetOrCreateProfileAsync(userId);
        var userLevel = profile.Level;
        var topCandidates = candidates
            .OrderBy(_ => Random.Shared.Next())
            .Take(8)
            .ToList();

        var encounters = new List<PlayerEncounterDto>(topCandidates.Count);
        foreach (var player in topCandidates)
        {
            var park = nearbyParks[Random.Shared.Next(nearbyParks.Count)];
            var parkLocation = GeometryFactory.CreatePoint(new Coordinate(park.Longitude, park.Latitude));

            var entry = await GetOrCreateEncounterAsync(userId, player, now, parkLocation, nearbyParks);
            var encounterPark = GetNearestPark(entry.Latitude, entry.Longitude, nearbyParks);
            encounters.Add(new PlayerEncounterDto(
                entry.Id,
                player.Id,
                player.Name,
                player.Position.ToString(),
                player.Rarity.ToString(),
                player.Level,
                entry.Latitude,
                entry.Longitude,
                entry.ExpiresAtUtc,
                CalculateSuccessChance(userLevel, player.Level, player.Rarity),
                encounterPark?.Name,
                encounterPark?.PlaceId));
        }

        return encounters;
    }

    public async Task<RecruitmentAttemptResultDto> AttemptRecruitmentAsync(string userId, RecruitmentAttemptRequest request, GeoPosition userPosition)
    {
        var now = DateTime.UtcNow;
        var encounter = await _db.PlayerEncounters
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EncounterId && e.UserId == userId && e.PlayerId == request.PlayerId);
        if (encounter is null)
        {
            return new RecruitmentAttemptResultDto(false, 0, "Encounter expired or invalid.", null);
        }

        if (encounter.ExpiresAtUtc <= now)
        {
            await DeleteEncounterAsync(encounter.Id);
            return new RecruitmentAttemptResultDto(false, 0, "Encounter expired or invalid.", null);
        }

        var encounterPos = new GeoPosition { Latitude = encounter.Latitude, Longitude = encounter.Longitude };
        var distanceMeters = GeoPosition.HaversineDistance(userPosition, encounterPos);
        if (distanceMeters > RecruitDistanceMeters)
        {
            return new RecruitmentAttemptResultDto(false, 0, "Go to the park to recruit this player.", null);
        }

        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlayerId);
        if (player is null)
        {
            return new RecruitmentAttemptResultDto(false, 0, "Player not found.", null);
        }

        var alreadyCollected = await _db.Collections.AnyAsync(c => c.UserId == userId && c.PlayerId == player.Id);
        if (alreadyCollected)
        {
            return new RecruitmentAttemptResultDto(false, 0, "You already recruited this player.", null);
        }

        var profile = await GetOrCreateProfileAsync(userId);
        var userLevel = profile.Level;
        var chance = CalculateSuccessChance(userLevel, player.Level, player.Rarity);
        var roll = Random.Shared.Next(1, 101);
        if (roll > chance)
        {
            return new RecruitmentAttemptResultDto(false, chance, "Recruitment failed. Try again.", null);
        }

        var collection = new CollectionModel
        {
            UserId = userId,
            PlayerId = player.Id,
            CapturedAt = now,
            IsFavorite = false,
            CapturedAtPitchId = null
        };

        _db.Collections.Add(collection);
        await AwardExperienceAsync(profile, RecruitSuccessXp);
        await _db.SaveChangesAsync();
        await DeleteEncounterAsync(request.EncounterId);

        return new RecruitmentAttemptResultDto(true, chance, "Recruitment success!", collection);
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

    private static int CalculateSuccessChance(int userLevel, int playerLevel, PlayerRarity rarity)
    {
        var levelDelta = userLevel - playerLevel;
        var rarityModifier = rarity switch
        {
            PlayerRarity.Common => 10,
            PlayerRarity.Uncommon => 0,
            PlayerRarity.Rare => -10,
            PlayerRarity.Epic => -20,
            PlayerRarity.Legendary => -30,
            _ => 0
        };

        var chance = 65 + (levelDelta * 3) + rarityModifier;
        return Math.Clamp(chance, 5, 95);
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
