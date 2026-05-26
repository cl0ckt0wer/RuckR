using dymaptic.GeoBlazor.Core.Components;
using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Renderers;
using dymaptic.GeoBlazor.Core.Components.Symbols;
using dymaptic.GeoBlazor.Core.Enums;
using dymaptic.GeoBlazor.Core.Model;
using RuckR.Shared.Models;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Creates client-side FeatureLayer inputs for the native GeoBlazor map experiment.
/// </summary>
public static class NativePitchFeatureLayerFactory
{
    /// <summary>The object id field used by the client-side pitch FeatureLayer.</summary>
    public const string ObjectIdField = "pitchId";

    /// <summary>The categorical field used by the pitch unique-value renderer.</summary>
    public const string PitchTypeField = "pitchType";

    /// <summary>Out fields used by ArcGIS popups and hit-test results.</summary>
    public static readonly IReadOnlyList<string> OutFields = ["*"];

    /// <summary>Fields declared for the client-side pitch FeatureLayer source.</summary>
    public static readonly IReadOnlyList<Field> Fields =
    [
        new(type: FieldType.Oid, name: ObjectIdField, alias: "Pitch ID", nullable: false),
        new(type: FieldType.String, name: "name", alias: "Name", length: 200),
        new(type: FieldType.String, name: PitchTypeField, alias: "Pitch type", length: 32),
        new(type: FieldType.String, name: "pitchRole", alias: "Role", length: 80),
        new(type: FieldType.String, name: "source", alias: "Source", length: 50),
        new(type: FieldType.Integer, name: "sourceConfidence", alias: "Source confidence")
    ];

    /// <summary>Create a pitch feature for the client-side native GeoBlazor FeatureLayer.</summary>
    /// <param name="pitch">Pitch model to render.</param>
    /// <param name="pitchTypeDisplayName">Display label for the pitch type.</param>
    /// <param name="pitchTypeDescription">Short display description for popups.</param>
    /// <returns>A GeoBlazor graphic without a per-feature symbol.</returns>
    public static Graphic CreatePitchFeature(
        PitchModel pitch,
        string pitchTypeDisplayName,
        string pitchTypeDescription) =>
        new(
            geometry: new Point(longitude: pitch.Longitude, latitude: pitch.Latitude),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "pitch",
                ["_ruckrId"] = pitch.Id,
                [ObjectIdField] = pitch.Id,
                ["name"] = pitch.Name,
                [PitchTypeField] = pitchTypeDisplayName,
                ["pitchRole"] = pitchTypeDescription,
                ["source"] = pitch.Source,
                ["sourceConfidence"] = pitch.SourceConfidence
            }));

    /// <summary>Create the ArcGIS unique-value renderer for pitch categories.</summary>
    /// <returns>A renderer keyed by <see cref="PitchTypeField"/>.</returns>
    public static UniqueValueRenderer CreatePitchTypeRenderer() =>
        new(
            defaultLabel: "Pitch",
            defaultSymbol: CreatePitchSymbol(PitchType.Standard),
            field: PitchTypeField,
            uniqueValueInfos:
            [
                new("Standard pitch", CreatePitchSymbol(PitchType.Standard), "Standard pitch"),
                new("Training pitch", CreatePitchSymbol(PitchType.Training), "Training pitch"),
                new("Stadium", CreatePitchSymbol(PitchType.Stadium), "Stadium")
            ]);

    private static SimpleMarkerSymbol CreatePitchSymbol(PitchType type) => type switch
    {
        PitchType.Standard => new SimpleMarkerSymbol(
            outline: new Outline(color: new MapColor("#F8FAFC"), width: new Dimension(2)),
            color: new MapColor("#22C55E"),
            size: new Dimension(15),
            style: SimpleMarkerSymbolStyle.Square),
        PitchType.Training => new SimpleMarkerSymbol(
            outline: new Outline(color: new MapColor("#082F49"), width: new Dimension(2)),
            color: new MapColor("#38BDF8"),
            size: new Dimension(15),
            style: SimpleMarkerSymbolStyle.Triangle),
        PitchType.Stadium => new SimpleMarkerSymbol(
            outline: new Outline(color: new MapColor("#111827"), width: new Dimension(3)),
            color: new MapColor("#F59E0B"),
            size: new Dimension(19),
            style: SimpleMarkerSymbolStyle.Diamond),
        _ => new SimpleMarkerSymbol(
            outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
            color: new MapColor(107, 114, 128),
            size: new Dimension(12),
            style: SimpleMarkerSymbolStyle.Circle)
    };
}
