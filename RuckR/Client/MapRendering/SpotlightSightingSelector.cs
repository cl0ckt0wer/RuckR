using RuckR.Shared.Models;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Chooses the client-side Spotlight Sighting from encounters already visible on the map.
/// </summary>
public static class SpotlightSightingSelector
{
    /// <summary>Select the best visible encounter to spotlight.</summary>
    /// <param name="encounters">Visible encounters already returned by the server.</param>
    /// <param name="currentPosition">Latest GPS position, if available.</param>
    /// <param name="utcNow">Current UTC time for expiry scoring.</param>
    /// <returns>The selected encounter, or null when none are visible.</returns>
    public static PlayerEncounterDto? Select(
        IReadOnlyList<PlayerEncounterDto> encounters,
        GeoPosition? currentPosition,
        DateTime utcNow)
    {
        if (encounters.Count == 0)
        {
            return null;
        }

        return encounters
            .OrderByDescending(encounter => Score(encounter, currentPosition, utcNow))
            .ThenBy(encounter => encounter.ExpiresAtUtc)
            .ThenBy(encounter => encounter.Name, StringComparer.Ordinal)
            .First();
    }

    /// <summary>Return a deterministic score for spotlight ordering.</summary>
    public static double Score(PlayerEncounterDto encounter, GeoPosition? currentPosition, DateTime utcNow)
    {
        var rarityScore = RarityRank(encounter.Rarity) * 10_000;
        var expiryScore = ExpiryUrgencyScore(encounter.ExpiresAtUtc - utcNow);
        var distanceScore = currentPosition is null
            ? 0
            : DistanceScore(GeoPosition.HaversineDistance(
                currentPosition,
                new GeoPosition { Latitude = encounter.Latitude, Longitude = encounter.Longitude }));

        return rarityScore + expiryScore + distanceScore;
    }

    private static int RarityRank(string rarity) => rarity switch
    {
        "Legendary" => 5,
        "Epic" => 4,
        "Rare" => 3,
        "Uncommon" => 2,
        "Common" => 1,
        _ => 0
    };

    private static double ExpiryUrgencyScore(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return -10_000;
        }

        var minutes = remaining.TotalMinutes;
        return minutes switch
        {
            <= 5 => 700,
            <= 15 => 500,
            <= 30 => 300,
            <= 60 => 150,
            _ => 0
        };
    }

    private static double DistanceScore(double distanceMeters)
    {
        if (distanceMeters <= 75)
        {
            return 500;
        }

        if (distanceMeters <= 250)
        {
            return 250;
        }

        if (distanceMeters <= 500)
        {
            return 100;
        }

        return 0;
    }
}
