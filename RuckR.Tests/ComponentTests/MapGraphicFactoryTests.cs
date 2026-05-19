using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Symbols;
using RuckR.Client.MapRendering;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests map graphic generation for RuckR-owned GeoBlazor layers.
/// </summary>
public class MapGraphicFactoryTests
{
    /// <summary>
    /// Verifies player location quality buckets are stable for layer styling.
    /// </summary>
    [Theory]
    [InlineData(null, "unknown")]
    [InlineData(12.0, "ready")]
    [InlineData(50.0, "ready")]
    [InlineData(50.1, "weak")]
    [InlineData(120.0, "weak")]
    public void ResolvePlayerLocationQuality_ReturnsExpectedBucket(double? accuracyMeters, string expected)
    {
        Assert.Equal(expected, MapGraphicFactory.ResolvePlayerLocationQuality(accuracyMeters));
    }

    /// <summary>
    /// Verifies the player layer emits an accuracy area followed by the location dot.
    /// </summary>
    [Fact]
    public void CreatePlayerLocationGraphics_ReturnsAccuracyAreaThenMarkerWithDiagnostics()
    {
        var timestamp = new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);
        var position = new GeoPosition
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            Accuracy = 82,
            Timestamp = timestamp
        };

        var graphics = MapGraphicFactory.CreatePlayerLocationGraphics(position);

        Assert.Equal(2, graphics.Count);
        var accuracyArea = Assert.IsType<Polygon>(graphics[0].Geometry);
        Assert.IsType<Point>(graphics[1].Geometry);
        Assert.IsType<SimpleFillSymbol>(graphics[0].Symbol);
        Assert.IsType<SimpleMarkerSymbol>(graphics[1].Symbol);
        Assert.Single(accuracyArea.Rings);

        AssertGraphicAttribute(graphics[0].Attributes, "_ruckrType", "player-location-accuracy");
        AssertGraphicAttribute(graphics[1].Attributes, "_ruckrType", "player-location");
        AssertGraphicAttribute(graphics[0].Attributes, "gpsQuality", "weak");
        AssertGraphicAttribute(graphics[1].Attributes, "gpsQuality", "weak");
        AssertGraphicAttribute(graphics[0].Attributes, "accuracyMeters", 82d);
        AssertGraphicAttribute(graphics[1].Attributes, "accuracyMeters", 82d);
        AssertGraphicAttribute(graphics[0].Attributes, "positionTimestampUtc", timestamp);
        AssertGraphicAttribute(graphics[1].Attributes, "positionTimestampUtc", timestamp);
    }

    /// <summary>
    /// Verifies recruitable player markers carry the attributes needed by map diagnostics and selection.
    /// </summary>
    [Fact]
    public void CreateEncounterGraphic_ReturnsProminentMarkerWithDiagnostics()
    {
        var encounterId = Guid.Parse("7f4b9eb3-1d2d-4dd5-bd7a-06dddcbe6fe5");
        var expiresAtUtc = new DateTime(2026, 5, 18, 14, 0, 0, DateTimeKind.Utc);
        var encounter = new PlayerEncounterDto(
            encounterId,
            PlayerId: 42,
            Name: "Scrum Sprinter",
            Position: "Wing",
            Rarity: "Rare",
            Level: 6,
            Latitude: 26.052688,
            Longitude: -80.158062,
            ExpiresAtUtc: expiresAtUtc,
            SuccessChancePercent: 72,
            ParkName: "Frost Park",
            ParkPlaceId: "frost-park");

        var graphic = MapGraphicFactory.CreateEncounterGraphic(encounter);

        Assert.IsType<Point>(graphic.Geometry);
        var symbol = Assert.IsType<SimpleMarkerSymbol>(graphic.Symbol);
        Assert.Equal(dymaptic.GeoBlazor.Core.Enums.SimpleMarkerSymbolStyle.Diamond, symbol.Style);
        AssertGraphicAttribute(graphic.Attributes, "_ruckrType", "encounter");
        AssertGraphicAttribute(graphic.Attributes, "_ruckrSpotlight", false);
        AssertGraphicAttribute(graphic.Attributes, "_ruckrEncounterId", encounterId.ToString());
        AssertGraphicAttribute(graphic.Attributes, "_ruckrPlayerId", 42);
        AssertGraphicAttribute(graphic.Attributes, "name", "Scrum Sprinter");
        AssertGraphicAttribute(graphic.Attributes, "position", "Wing");
        AssertGraphicAttribute(graphic.Attributes, "rarity", "Rare");
        AssertGraphicAttribute(graphic.Attributes, "level", 6);
        AssertGraphicAttribute(graphic.Attributes, "parkName", "Frost Park");
        AssertGraphicAttribute(graphic.Attributes, "successChancePercent", 72);
        AssertGraphicAttribute(graphic.Attributes, "expiresAtUtc", expiresAtUtc);
    }

    /// <summary>
    /// Verifies spotlight encounter markers carry distinct styling and diagnostics.
    /// </summary>
    [Fact]
    public void CreateEncounterGraphic_WhenSpotlight_ReturnsDistinctMarkerWithDiagnostics()
    {
        var encounter = new PlayerEncounterDto(
            Guid.Parse("92527b83-f4d9-4e80-802a-738a0f5f3795"),
            PlayerId: 99,
            Name: "Brass Boot",
            Position: "FlyHalf",
            Rarity: "Legendary",
            Level: 12,
            Latitude: 51.5074,
            Longitude: -0.1278,
            ExpiresAtUtc: new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc),
            SuccessChancePercent: 44);

        var graphic = MapGraphicFactory.CreateEncounterGraphic(encounter, isSpotlight: true);

        var symbol = Assert.IsType<SimpleMarkerSymbol>(graphic.Symbol);
        Assert.Equal(dymaptic.GeoBlazor.Core.Enums.SimpleMarkerSymbolStyle.Diamond, symbol.Style);
        AssertGraphicAttribute(graphic.Attributes, "_ruckrSpotlight", true);
    }

    private static void AssertGraphicAttribute(
        dymaptic.GeoBlazor.Core.Model.AttributesDictionary attributes,
        string key,
        object? expected)
    {
        Assert.True(attributes.TryGetValue(key, out var actual), $"Expected graphic attribute '{key}'.");
        Assert.Equal(expected, actual);
    }
}
