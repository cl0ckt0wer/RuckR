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
        Assert.IsType<Polygon>(graphics[0].Geometry);
        Assert.IsType<Point>(graphics[1].Geometry);
        Assert.IsType<SimpleFillSymbol>(graphics[0].Symbol);
        Assert.IsType<SimpleMarkerSymbol>(graphics[1].Symbol);

        AssertGraphicAttribute(graphics[0].Attributes, "_ruckrType", "player-location-accuracy");
        AssertGraphicAttribute(graphics[1].Attributes, "_ruckrType", "player-location");
        AssertGraphicAttribute(graphics[0].Attributes, "gpsQuality", "weak");
        AssertGraphicAttribute(graphics[1].Attributes, "gpsQuality", "weak");
        AssertGraphicAttribute(graphics[0].Attributes, "accuracyMeters", 82d);
        AssertGraphicAttribute(graphics[1].Attributes, "accuracyMeters", 82d);
        AssertGraphicAttribute(graphics[0].Attributes, "positionTimestampUtc", timestamp);
        AssertGraphicAttribute(graphics[1].Attributes, "positionTimestampUtc", timestamp);
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
