using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Renderers;
using dymaptic.GeoBlazor.Core.Enums;
using RuckR.Client.MapRendering;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests native GeoBlazor FeatureLayer inputs for pitch rendering.
/// </summary>
public class NativePitchFeatureLayerFactoryTests
{
    /// <summary>
    /// Verifies the pitch FeatureLayer schema has the fields required by ArcGIS renderers and popups.
    /// </summary>
    [Fact]
    public void Fields_ExposeObjectIdAndPitchType()
    {
        Assert.Contains(NativePitchFeatureLayerFactory.Fields, field =>
            field.Name == NativePitchFeatureLayerFactory.ObjectIdField && field.Type == FieldType.Oid);
        Assert.Contains(NativePitchFeatureLayerFactory.Fields, field =>
            field.Name == NativePitchFeatureLayerFactory.PitchTypeField && field.Type == FieldType.String);
        Assert.Contains("*", NativePitchFeatureLayerFactory.OutFields);
    }

    /// <summary>
    /// Verifies pitch models become point features with RuckR selection and popup attributes.
    /// </summary>
    [Fact]
    public void CreatePitchFeature_ReturnsPointWithPitchAttributes()
    {
        var pitch = new PitchModel
        {
            Id = 42,
            Name = "Test Stadium",
            Type = PitchType.Stadium,
            Latitude = 51.5074,
            Longitude = -0.1278
        };

        var feature = NativePitchFeatureLayerFactory.CreatePitchFeature(pitch, "Stadium", "Major venue");

        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(51.5074, point.Latitude);
        Assert.Equal(-0.1278, point.Longitude);
        AssertGraphicAttribute(feature.Attributes, "_ruckrType", "pitch");
        AssertGraphicAttribute(feature.Attributes, "_ruckrId", 42);
        AssertGraphicAttribute(feature.Attributes, NativePitchFeatureLayerFactory.ObjectIdField, 42);
        AssertGraphicAttribute(feature.Attributes, NativePitchFeatureLayerFactory.PitchTypeField, "Stadium");
    }

    /// <summary>
    /// Verifies the native renderer can drive ArcGIS legend categories by pitch type.
    /// </summary>
    [Fact]
    public void CreatePitchTypeRenderer_ReturnsUniqueValueRendererForPitchType()
    {
        var renderer = NativePitchFeatureLayerFactory.CreatePitchTypeRenderer();

        Assert.Equal(NativePitchFeatureLayerFactory.PitchTypeField, renderer.Field);
        Assert.Equal("Pitch", renderer.DefaultLabel);
        Assert.NotNull(renderer.DefaultSymbol);
        Assert.NotNull(renderer.UniqueValueInfos);
        Assert.Contains(renderer.UniqueValueInfos!, item => item.Value == "Standard pitch");
        Assert.Contains(renderer.UniqueValueInfos!, item => item.Value == "Training pitch");
        Assert.Contains(renderer.UniqueValueInfos!, item => item.Value == "Stadium");
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
