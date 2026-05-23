using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Renderers;
using dymaptic.GeoBlazor.Core.Enums;
using RuckR.Client.MapRendering;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests native GeoBlazor FeatureLayer inputs for non-pitch map entities.
/// </summary>
public class NativeMapFeatureLayerFactoryTests
{
    /// <summary>
    /// Verifies all native layer schemas expose object id fields and popup out fields.
    /// </summary>
    [Fact]
    public void Fields_ExposeObjectIdsAndOutFields()
    {
        Assert.Contains(NativeMapFeatureLayerFactory.CandidateFields, field =>
            field.Name == NativeMapFeatureLayerFactory.CandidateObjectIdField && field.Type == FieldType.Oid);
        Assert.Contains(NativeMapFeatureLayerFactory.EncounterFields, field =>
            field.Name == NativeMapFeatureLayerFactory.EncounterObjectIdField && field.Type == FieldType.Oid);
        Assert.Contains(NativeMapFeatureLayerFactory.PlayerLocationFields, field =>
            field.Name == NativeMapFeatureLayerFactory.PlayerObjectIdField && field.Type == FieldType.Oid);
        Assert.Contains(NativeMapFeatureLayerFactory.PlayerAccuracyFields, field =>
            field.Name == NativeMapFeatureLayerFactory.PlayerAccuracyObjectIdField && field.Type == FieldType.Oid);
        Assert.Contains("*", NativeMapFeatureLayerFactory.OutFields);
    }

    /// <summary>
    /// Verifies ArcGIS candidate place features preserve RuckR selection attributes.
    /// </summary>
    [Fact]
    public void CreateCandidatePlaceFeature_ReturnsPointWithSelectionAttributes()
    {
        var candidate = new PitchCandidatePlaceDto(
            "place-1",
            "Rugby Field",
            51.5,
            -0.12,
            150,
            "Rugby Pitch",
            PitchType.Training.ToString(),
            "Field sport venue",
            81);

        var feature = NativeMapFeatureLayerFactory.CreateCandidatePlaceFeature(candidate, 7);

        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(51.5, point.Latitude);
        Assert.Equal(-0.12, point.Longitude);
        AssertGraphicAttribute(feature.Attributes, "_ruckrType", "candidate-place");
        AssertGraphicAttribute(feature.Attributes, "_ruckrPlaceId", "place-1");
        AssertGraphicAttribute(feature.Attributes, NativeMapFeatureLayerFactory.CandidateObjectIdField, 7);
        AssertGraphicAttribute(feature.Attributes, NativeMapFeatureLayerFactory.CandidateRecommendedPitchTypeField, PitchType.Training.ToString());
    }

    /// <summary>
    /// Verifies ArcGIS encounter features preserve RuckR selection attributes.
    /// </summary>
    [Fact]
    public void CreateEncounterFeature_ReturnsPointWithSelectionAttributes()
    {
        var encounterId = Guid.NewGuid();
        var encounter = new PlayerEncounterDto(
            encounterId,
            12,
            "Ada Ruck",
            "Fly-half",
            "Rare",
            8,
            51.6,
            -0.13,
            DateTime.UtcNow.AddMinutes(10),
            65,
            45,
            "Test Park");

        var feature = NativeMapFeatureLayerFactory.CreateEncounterFeature(encounter, 3, isSpotlight: true);

        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(51.6, point.Latitude);
        Assert.Equal(-0.13, point.Longitude);
        AssertGraphicAttribute(feature.Attributes, "_ruckrType", "encounter");
        AssertGraphicAttribute(feature.Attributes, "_ruckrEncounterId", encounterId.ToString());
        AssertGraphicAttribute(feature.Attributes, "_ruckrSpotlight", 1);
        AssertGraphicAttribute(feature.Attributes, NativeMapFeatureLayerFactory.EncounterObjectIdField, 3);
        AssertGraphicAttribute(feature.Attributes, NativeMapFeatureLayerFactory.EncounterRarityField, "Rare");
    }

    /// <summary>
    /// Verifies player GPS becomes separate point and polygon native features.
    /// </summary>
    [Fact]
    public void CreatePlayerFeatures_ReturnPointAndAccuracyPolygon()
    {
        var position = new GeoPosition
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            Accuracy = 25,
            Timestamp = DateTime.UtcNow
        };

        var pointFeature = NativeMapFeatureLayerFactory.CreatePlayerLocationFeature(position);
        var accuracyFeature = NativeMapFeatureLayerFactory.CreatePlayerAccuracyFeature(position);

        Assert.IsType<Point>(pointFeature.Geometry);
        Assert.IsType<Polygon>(accuracyFeature.Geometry);
        AssertGraphicAttribute(pointFeature.Attributes, "_ruckrType", "player-location");
        AssertGraphicAttribute(accuracyFeature.Attributes, "_ruckrType", "player-location-accuracy");
        AssertGraphicAttribute(pointFeature.Attributes, NativeMapFeatureLayerFactory.PlayerLocationQualityField, "ready");
        AssertGraphicAttribute(accuracyFeature.Attributes, NativeMapFeatureLayerFactory.PlayerLocationQualityField, "ready");
    }

    /// <summary>
    /// Verifies native renderers are driven by ArcGIS unique-value categories.
    /// </summary>
    [Fact]
    public void CreateRenderers_ReturnUniqueValueRenderers()
    {
        Assert.IsType<UniqueValueRenderer>(NativeMapFeatureLayerFactory.CreateCandidatePlaceRenderer());
        Assert.IsType<UniqueValueRenderer>(NativeMapFeatureLayerFactory.CreateEncounterRenderer());
        Assert.IsType<UniqueValueRenderer>(NativeMapFeatureLayerFactory.CreatePlayerLocationRenderer());
        Assert.IsType<UniqueValueRenderer>(NativeMapFeatureLayerFactory.CreatePlayerAccuracyRenderer());
    }

    /// <summary>
    /// Verifies encounter popups expose recruit details and a GeoBlazor action hook.
    /// </summary>
    [Fact]
    public async Task CreateEncounterPopupTemplate_ReturnsRecruitSummaryAction()
    {
        var actionInvoked = false;
        var popup = NativeMapFeatureLayerFactory.CreateEncounterPopupTemplate(() =>
        {
            actionInvoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal("{name}", popup.Title);
        Assert.NotNull(popup.StringContent);
        Assert.Contains("{successChancePercent}", popup.StringContent);
        Assert.Contains("{baseRecruitmentSeconds}", popup.StringContent);
        Assert.NotNull(popup.OutFields);
        Assert.Contains("*", popup.OutFields);

        Assert.NotNull(popup.Actions);
        var action = Assert.Single(popup.Actions);
        Assert.Equal("ruckr-open-recruit-board", action.ActionId);
        Assert.Equal("Open recruit board", action.Title);
        Assert.NotNull(action.CallbackFunction);

        await action.CallbackFunction!.Invoke();
        Assert.True(actionInvoked);
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
