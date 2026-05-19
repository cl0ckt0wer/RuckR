using RuckR.Shared.Models;
using MapPage = RuckR.Client.Pages.GameMap;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests display-only Spotlight Sighting map UI text and state helpers.
/// </summary>
public class SpotlightSightingUiStateTests
{
    private static readonly DateTime Now = new(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Countdown text uses stable user-facing buckets.</summary>
    [Theory]
    [InlineData(-1, "Expired")]
    [InlineData(0.5, "<1 min left")]
    [InlineData(8, "8m left")]
    [InlineData(62, "1h 2m left")]
    public void FormatEncounterExpiresText_ReturnsExpectedCountdown(double minutesFromNow, string expected)
    {
        var encounter = Encounter(expiresAtUtc: Now.AddMinutes(minutesFromNow));

        var text = MapPage.FormatEncounterExpiresText(encounter, Now);

        Assert.Equal(expected, text);
    }

    /// <summary>Spotlight summary leads with rarity, pitch context, and distance.</summary>
    [Fact]
    public void BuildSpotlightSightingSummary_UsesHuntFirstFields()
    {
        var encounter = Encounter(rarity: "Legendary");

        var summary = MapPage.BuildSpotlightSightingSummary(encounter, "Spotlight Park", "<50m");

        Assert.Equal("Legendary at Spotlight Park · <50m", summary);
    }

    /// <summary>Recruit state labels expose clear ready, blocked, weak, waiting, and expired states.</summary>
    [Theory]
    [InlineData(false, -1d, false, "GPS needed", "blocked")]
    [InlineData(false, 82d, false, "Weak GPS", "weak")]
    [InlineData(false, 12d, false, "Move closer", "waiting")]
    [InlineData(false, 12d, true, "Recruit window", "ready")]
    [InlineData(true, 12d, true, "Sighting expired", "expired")]
    public void BuildRecruitStateLabel_ReturnsExpectedState(
        bool expired,
        double accuracyMeters,
        bool canRecruit,
        string expectedLabel,
        string expectedCssClass)
    {
        var encounter = Encounter(expiresAtUtc: expired ? Now.AddSeconds(-1) : Now.AddMinutes(10));
        var position = accuracyMeters >= 0
            ? new GeoPosition { Latitude = encounter.Latitude, Longitude = encounter.Longitude, Accuracy = accuracyMeters }
            : null;

        var label = MapPage.BuildRecruitStateLabel(encounter, position, canRecruit, Now);

        Assert.Equal(expectedLabel, label);
        Assert.Equal(expectedCssClass, MapPage.RecruitStateCssClassForLabel(label));
    }

    /// <summary>No selected encounter keeps the board in an inert waiting state.</summary>
    [Fact]
    public void BuildRecruitStateLabel_WithNoSelection_AsksForRecruitablePlayer()
    {
        var label = MapPage.BuildRecruitStateLabel(null, lastKnownPosition: null, canRecruit: false, Now);

        Assert.Equal("Select a recruitable player.", label);
        Assert.Equal("waiting", MapPage.RecruitStateCssClassForLabel(label));
    }

    private static PlayerEncounterDto Encounter(
        DateTime? expiresAtUtc = null,
        string rarity = "Rare") =>
        new(
            Guid.Parse("59fb6bd5-cd50-4f49-bd1a-3ea33604d33c"),
            PlayerId: 7,
            Name: "Brass Boot",
            Position: "FlyHalf",
            Rarity: rarity,
            Level: 8,
            Latitude: 51.5074,
            Longitude: -0.1278,
            ExpiresAtUtc: expiresAtUtc ?? Now.AddMinutes(10),
            SuccessChancePercent: 62,
            ParkName: "Spotlight Park",
            ParkPlaceId: "spotlight-park");
}
