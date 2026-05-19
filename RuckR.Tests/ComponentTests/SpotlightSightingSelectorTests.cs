using RuckR.Client.MapRendering;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests client-side Spotlight Sighting selection.
/// </summary>
public class SpotlightSightingSelectorTests
{
    private static readonly DateTime Now = new(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Rare, urgent, nearby encounters should outrank ordinary nearby encounters.</summary>
    [Fact]
    public void Select_PrefersRareUrgentNearbyEncounter()
    {
        var current = new GeoPosition { Latitude = 51.5074, Longitude = -0.1278 };
        var commonNearby = Encounter("Common Nearby", "Common", Now.AddMinutes(5), 51.5074, -0.1278);
        var rareUrgent = Encounter("Rare Urgent", "Rare", Now.AddMinutes(8), 51.5075, -0.1278);
        var uncommonFar = Encounter("Uncommon Far", "Uncommon", Now.AddMinutes(90), 51.6074, -0.1278);

        var selected = SpotlightSightingSelector.Select([commonNearby, rareUrgent, uncommonFar], current, Now);

        Assert.Equal(rareUrgent.EncounterId, selected?.EncounterId);
    }

    /// <summary>Null GPS should still choose safely from rarity and expiry.</summary>
    [Fact]
    public void Select_WithNullGps_UsesRarityAndExpiry()
    {
        var common = Encounter("Common", "Common", Now.AddMinutes(5), 51.5074, -0.1278);
        var epic = Encounter("Epic", "Epic", Now.AddMinutes(90), 51.5074, -0.1278);

        var selected = SpotlightSightingSelector.Select([common, epic], currentPosition: null, Now);

        Assert.Equal(epic.EncounterId, selected?.EncounterId);
    }

    /// <summary>Empty encounter lists have no spotlight.</summary>
    [Fact]
    public void Select_WithEmptyList_ReturnsNull()
    {
        var selected = SpotlightSightingSelector.Select([], currentPosition: null, Now);

        Assert.Null(selected);
    }

    /// <summary>Expired sightings should lose to active sightings of the same rarity.</summary>
    [Fact]
    public void Select_WithExpiredEncounter_PrefersActiveEncounter()
    {
        var expired = Encounter("Expired Rare", "Rare", Now.AddSeconds(-1), 51.5074, -0.1278);
        var active = Encounter("Active Rare", "Rare", Now.AddMinutes(45), 51.5074, -0.1278);

        var selected = SpotlightSightingSelector.Select([expired, active], currentPosition: null, Now);

        Assert.Equal(active.EncounterId, selected?.EncounterId);
    }

    /// <summary>Distance scoring rewards nearby sightings when rarity and expiry match.</summary>
    [Fact]
    public void Select_WithGps_PrefersNearerEncounterWhenRarityAndExpiryMatch()
    {
        var current = new GeoPosition { Latitude = 51.5074, Longitude = -0.1278 };
        var far = Encounter("Rare Far", "Rare", Now.AddMinutes(20), 51.5174, -0.1278);
        var nearby = Encounter("Rare Nearby", "Rare", Now.AddMinutes(20), 51.50745, -0.1278);

        var selected = SpotlightSightingSelector.Select([far, nearby], current, Now);

        Assert.Equal(nearby.EncounterId, selected?.EncounterId);
        Assert.True(
            SpotlightSightingSelector.Score(nearby, current, Now) >
            SpotlightSightingSelector.Score(far, current, Now));
    }

    /// <summary>Ordering is deterministic when scores tie.</summary>
    [Fact]
    public void Select_WithTiedScores_UsesExpiryThenName()
    {
        var laterAlpha = Encounter("Alpha", "Rare", Now.AddMinutes(31), 51.5074, -0.1278);
        var earlierZulu = Encounter("Zulu", "Rare", Now.AddMinutes(30), 51.5074, -0.1278);
        var sameExpiryBeta = Encounter("Beta", "Rare", Now.AddMinutes(30), 51.5074, -0.1278);

        var selected = SpotlightSightingSelector.Select([laterAlpha, earlierZulu, sameExpiryBeta], currentPosition: null, Now);

        Assert.Equal(sameExpiryBeta.EncounterId, selected?.EncounterId);
    }

    private static PlayerEncounterDto Encounter(
        string name,
        string rarity,
        DateTime expiresAtUtc,
        double latitude,
        double longitude) =>
        new(
            Guid.NewGuid(),
            PlayerId: Random.Shared.Next(1, 10_000),
            Name: name,
            Position: "Wing",
            Rarity: rarity,
            Level: 5,
            Latitude: latitude,
            Longitude: longitude,
            ExpiresAtUtc: expiresAtUtc,
            SuccessChancePercent: 65,
            ParkName: "Test Park");
}
