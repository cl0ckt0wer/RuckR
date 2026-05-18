using dymaptic.GeoBlazor.Core.Components;
using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Popups;
using dymaptic.GeoBlazor.Core.Components.Symbols;
using dymaptic.GeoBlazor.Core.Enums;
using dymaptic.GeoBlazor.Core.Model;
using RuckR.Shared.Models;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Creates GeoBlazor graphics for RuckR map entities.
/// </summary>
public static class MapGraphicFactory
{
    /// <summary>Create a pitch marker graphic.</summary>
    /// <param name="pitch">Pitch model to render.</param>
    /// <param name="pitchTypeDisplayName">Display label for the pitch type.</param>
    /// <param name="pitchTypeDescription">Short display description for the pitch type.</param>
    /// <returns>A GeoBlazor graphic with RuckR pitch attributes.</returns>
    public static Graphic CreatePitchGraphic(
        PitchModel pitch,
        string pitchTypeDisplayName,
        string pitchTypeDescription) =>
        new(
            geometry: new Point(longitude: pitch.Longitude, latitude: pitch.Latitude),
            symbol: GetPitchSymbol(pitch.Type),
            popupTemplate: CreatePitchPopupTemplate(),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "pitch",
                ["_ruckrId"] = pitch.Id,
                ["name"] = pitch.Name,
                ["pitchType"] = pitchTypeDisplayName,
                ["pitchRole"] = pitchTypeDescription
            }));

    /// <summary>Create a recruitable encounter marker graphic.</summary>
    /// <param name="encounter">Encounter DTO to render.</param>
    /// <returns>A GeoBlazor graphic with RuckR encounter attributes.</returns>
    public static Graphic CreateEncounterGraphic(PlayerEncounterDto encounter) =>
        new(
            geometry: new Point(longitude: encounter.Longitude, latitude: encounter.Latitude),
            symbol: new SimpleMarkerSymbol(
                outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
                color: EncounterRarityColor(encounter.Rarity),
                size: new Dimension(14),
                style: SimpleMarkerSymbolStyle.Circle),
            popupTemplate: CreateEncounterPopupTemplate(),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "encounter",
                ["_ruckrEncounterId"] = encounter.EncounterId.ToString(),
                ["name"] = encounter.Name,
                ["rarity"] = encounter.Rarity,
                ["level"] = encounter.Level,
                ["parkName"] = encounter.ParkName
            }));

    /// <summary>Create a candidate pitch place marker graphic.</summary>
    /// <param name="candidate">Candidate place DTO to render.</param>
    /// <returns>A GeoBlazor graphic with RuckR candidate-place attributes.</returns>
    public static Graphic CreateCandidatePlaceGraphic(PitchCandidatePlaceDto candidate) =>
        new(
            geometry: new Point(longitude: candidate.Longitude, latitude: candidate.Latitude),
            symbol: GetCandidatePlaceSymbol(candidate.RecommendedPitchType),
            popupTemplate: CreateCandidatePlacePopupTemplate(),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "candidate-place",
                ["_ruckrPlaceId"] = candidate.PlaceId,
                ["name"] = candidate.Name,
                ["recommendedPitchType"] = candidate.RecommendedPitchType,
                ["confidence"] = candidate.Confidence,
                ["categoryLabel"] = candidate.CategoryLabel,
                ["matchReason"] = candidate.MatchReason
            }));

    /// <summary>Create the current player location marker graphic.</summary>
    /// <param name="position">Latest GPS position.</param>
    /// <returns>A GeoBlazor graphic with player-location attributes.</returns>
    public static Graphic CreatePlayerLocationGraphic(GeoPosition position) =>
        new(
            geometry: new Point(longitude: position.Longitude, latitude: position.Latitude),
            symbol: GetPlayerLocationSymbol(),
            popupTemplate: CreatePlayerLocationPopupTemplate(),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "player-location",
                ["accuracyMeters"] = position.Accuracy
            }));

    private static MapColor EncounterRarityColor(string rarity) => rarity switch
    {
        "Uncommon" => new MapColor(47, 133, 90),
        "Rare" => new MapColor(37, 99, 235),
        "Epic" => new MapColor(217, 119, 6),
        "Legendary" => new MapColor(200, 30, 30),
        _ => new MapColor(107, 114, 128)
    };

    private static PopupTemplate CreatePitchPopupTemplate() =>
        new(
            title: "{name}",
            stringContent: "{pitchType}<br>{pitchRole}");

    private static PopupTemplate CreateEncounterPopupTemplate() =>
        new(
            title: "{name}",
            stringContent: "Level {level} {rarity}<br>{parkName}");

    private static PopupTemplate CreateCandidatePlacePopupTemplate() =>
        new(
            title: "{name}",
            stringContent: "{recommendedPitchType}<br>{confidence}% confidence<br>{matchReason}");

    private static PopupTemplate CreatePlayerLocationPopupTemplate() =>
        new(
            title: "Your location",
            stringContent: "Accuracy: {accuracyMeters}m");

    private static SimpleMarkerSymbol GetPitchSymbol(PitchType type) => type switch
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

    private static SimpleMarkerSymbol GetCandidatePlaceSymbol(string recommendedPitchType)
    {
        var fill = Enum.TryParse<PitchType>(recommendedPitchType, ignoreCase: true, out var parsed)
            ? parsed switch
            {
                PitchType.Stadium => "#F59E0B",
                PitchType.Standard => "#22C55E",
                PitchType.Training => "#38BDF8",
                _ => "#A855F7"
            }
            : "#A855F7";

        return new SimpleMarkerSymbol(
            outline: new Outline(color: new MapColor("#F5D0FE"), width: new Dimension(3)),
            color: new MapColor(fill),
            size: new Dimension(13),
            style: SimpleMarkerSymbolStyle.Circle);
    }

    private static SimpleMarkerSymbol GetPlayerLocationSymbol() =>
        new(
            outline: new Outline(color: new MapColor("#EFF6FF"), width: new Dimension(3)),
            color: new MapColor("#2563EB"),
            size: new Dimension(16),
            style: SimpleMarkerSymbolStyle.Circle);
}
