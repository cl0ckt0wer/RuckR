using dymaptic.GeoBlazor.Core.Components;
using dymaptic.GeoBlazor.Core.Components.Geometries;
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
    private const double EarthRadiusMeters = 6378137;
    private const double ReadyAccuracyMeters = 50;

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
            symbol: GetPlayerLocationSymbol(ResolvePlayerLocationQuality(position.Accuracy)),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "player-location",
                ["accuracyMeters"] = position.Accuracy,
                ["gpsQuality"] = ResolvePlayerLocationQuality(position.Accuracy),
                ["positionTimestampUtc"] = position.Timestamp
            }));

    /// <summary>Resolve the visual quality bucket for a player GPS fix.</summary>
    /// <param name="accuracyMeters">Horizontal GPS accuracy in meters.</param>
    /// <returns>A stable quality bucket for layer styling and diagnostics.</returns>
    public static string ResolvePlayerLocationQuality(double? accuracyMeters) =>
        accuracyMeters switch
        {
            null => "unknown",
            <= ReadyAccuracyMeters => "ready",
            _ => "weak"
        };

    /// <summary>Create the current player location accuracy circle and marker graphics.</summary>
    /// <param name="position">Latest GPS position.</param>
    /// <returns>Accuracy circle followed by the player-location marker graphic.</returns>
    public static IReadOnlyList<Graphic> CreatePlayerLocationGraphics(GeoPosition position) =>
    [
        CreatePlayerAccuracyGraphic(position),
        CreatePlayerLocationGraphic(position)
    ];

    private static Graphic CreatePlayerAccuracyGraphic(GeoPosition position)
    {
        var accuracyMeters = Math.Max(position.Accuracy ?? 0, 5);
        var center = new Point(longitude: position.Longitude, latitude: position.Latitude);
        var quality = ResolvePlayerLocationQuality(position.Accuracy);

        return new Graphic(
            geometry: new Polygon(
                rings: [CreateGeodesicRing(position.Latitude, position.Longitude, accuracyMeters)],
                spatialReference: SpatialReference.Wgs84,
                centroid: center),
            symbol: GetPlayerAccuracySymbol(quality),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                ["_ruckrType"] = "player-location-accuracy",
                ["accuracyMeters"] = position.Accuracy,
                ["gpsQuality"] = quality,
                ["positionTimestampUtc"] = position.Timestamp
            }));
    }

    private static MapPath CreateGeodesicRing(double latitude, double longitude, double radiusMeters)
    {
        const int segments = 80;
        var centerLatitude = DegreesToRadians(latitude);
        var centerLongitude = DegreesToRadians(longitude);
        var angularDistance = radiusMeters / EarthRadiusMeters;
        var ring = new MapPoint[segments + 1];

        for (var i = 0; i <= segments; i++)
        {
            var bearing = 2 * Math.PI * i / segments;
            var latitudeRadians = Math.Asin(
                Math.Sin(centerLatitude) * Math.Cos(angularDistance)
                + Math.Cos(centerLatitude) * Math.Sin(angularDistance) * Math.Cos(bearing));
            var longitudeRadians = centerLongitude + Math.Atan2(
                Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(centerLatitude),
                Math.Cos(angularDistance) - Math.Sin(centerLatitude) * Math.Sin(latitudeRadians));

            ring[i] = new MapPoint([RadiansToDegrees(longitudeRadians), RadiansToDegrees(latitudeRadians)]);
        }

        return new MapPath(ring);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    private static MapColor EncounterRarityColor(string rarity) => rarity switch
    {
        "Uncommon" => new MapColor(47, 133, 90),
        "Rare" => new MapColor(37, 99, 235),
        "Epic" => new MapColor(217, 119, 6),
        "Legendary" => new MapColor(200, 30, 30),
        _ => new MapColor(107, 114, 128)
    };

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

    private static SimpleMarkerSymbol GetPlayerLocationSymbol(string quality) =>
        new(
            outline: new Outline(color: PlayerLocationOutlineColor(quality), width: new Dimension(3)),
            color: PlayerLocationColor(quality),
            size: new Dimension(16),
            style: SimpleMarkerSymbolStyle.Circle);

    private static SimpleFillSymbol GetPlayerAccuracySymbol(string quality) =>
        new(
            outline: new Outline(color: PlayerAccuracyOutlineColor(quality), width: new Dimension(1.5)),
            color: PlayerAccuracyFillColor(quality),
            style: SimpleFillSymbolStyle.Solid);

    private static MapColor PlayerLocationColor(string quality) => quality switch
    {
        "ready" => new MapColor("#2563EB"),
        "weak" => new MapColor("#D97706"),
        _ => new MapColor("#64748B")
    };

    private static MapColor PlayerLocationOutlineColor(string quality) => quality switch
    {
        "ready" => new MapColor("#EFF6FF"),
        "weak" => new MapColor("#FFF7ED"),
        _ => new MapColor("#F8FAFC")
    };

    private static MapColor PlayerAccuracyOutlineColor(string quality) => quality switch
    {
        "ready" => new MapColor(new double[] { 37, 99, 235, 0.45 }),
        "weak" => new MapColor(new double[] { 217, 119, 6, 0.5 }),
        _ => new MapColor(new double[] { 100, 116, 139, 0.45 })
    };

    private static MapColor PlayerAccuracyFillColor(string quality) => quality switch
    {
        "ready" => new MapColor(new double[] { 37, 99, 235, 0.16 }),
        "weak" => new MapColor(new double[] { 217, 119, 6, 0.18 }),
        _ => new MapColor(new double[] { 100, 116, 139, 0.14 })
    };
}
