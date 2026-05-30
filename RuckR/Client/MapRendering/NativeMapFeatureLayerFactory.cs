using dymaptic.GeoBlazor.Core.Components;
using dymaptic.GeoBlazor.Core.Components.Geometries;
using dymaptic.GeoBlazor.Core.Components.Popups;
using dymaptic.GeoBlazor.Core.Components.Renderers;
using dymaptic.GeoBlazor.Core.Components.Symbols;
using dymaptic.GeoBlazor.Core.Enums;
using dymaptic.GeoBlazor.Core.Model;
using RuckR.Shared.Models;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Creates client-side FeatureLayer inputs for the full native GeoBlazor map experiment.
/// </summary>
public static class NativeMapFeatureLayerFactory
{
    private const double EarthRadiusMeters = 6378137;
    private const double ReadyAccuracyMeters = 200;

    /// <summary>Object id field for the client-side candidate-place FeatureLayer.</summary>
    public const string CandidateObjectIdField = "candidateObjectId";

    /// <summary>Renderer category field for the candidate-place FeatureLayer.</summary>
    public const string CandidateRecommendedPitchTypeField = "recommendedPitchType";

    /// <summary>Object id field for the client-side encounter FeatureLayer.</summary>
    public const string EncounterObjectIdField = "encounterObjectId";

    /// <summary>Renderer category field for the encounter FeatureLayer.</summary>
    public const string EncounterRarityField = "rarity";

    /// <summary>Object id field for the client-side player-location FeatureLayer.</summary>
    public const string PlayerObjectIdField = "playerObjectId";

    /// <summary>Object id field for the client-side player-accuracy FeatureLayer.</summary>
    public const string PlayerAccuracyObjectIdField = "playerAccuracyObjectId";

    /// <summary>Renderer category field for player GPS quality layers.</summary>
    public const string PlayerLocationQualityField = "gpsQuality";

    /// <summary>Out fields used by native ArcGIS popups and hit-test results.</summary>
    public static readonly IReadOnlyList<string> OutFields = ["*"];

    /// <summary>Fields declared for the client-side candidate-place FeatureLayer source.</summary>
    public static readonly IReadOnlyList<Field> CandidateFields =
    [
        new(type: FieldType.Oid, name: CandidateObjectIdField, alias: "Candidate ID", nullable: false),
        new(type: FieldType.String, name: "_ruckrType", alias: "RuckR type", length: 40),
        new(type: FieldType.String, name: "_ruckrPlaceId", alias: "Place ID", length: 120),
        new(type: FieldType.String, name: "name", alias: "Name", length: 200),
        new(type: FieldType.String, name: CandidateRecommendedPitchTypeField, alias: "Recommended pitch type", length: 32),
        new(type: FieldType.Integer, name: "confidence", alias: "Confidence"),
        new(type: FieldType.String, name: "categoryLabel", alias: "Category", length: 120),
        new(type: FieldType.String, name: "matchReason", alias: "Match reason", length: 200)
    ];

    /// <summary>Fields declared for the client-side encounter FeatureLayer source.</summary>
    public static readonly IReadOnlyList<Field> EncounterFields =
    [
        new(type: FieldType.Oid, name: EncounterObjectIdField, alias: "Encounter ID", nullable: false),
        new(type: FieldType.String, name: "_ruckrType", alias: "RuckR type", length: 40),
        new(type: FieldType.String, name: "_ruckrEncounterId", alias: "Encounter GUID", length: 80),
        new(type: FieldType.Integer, name: "_ruckrPlayerId", alias: "Player ID"),
        new(type: FieldType.Integer, name: "_ruckrSpotlight", alias: "Spotlight"),
        new(type: FieldType.String, name: "name", alias: "Name", length: 200),
        new(type: FieldType.String, name: "position", alias: "Position", length: 80),
        new(type: FieldType.String, name: EncounterRarityField, alias: "Rarity", length: 40),
        new(type: FieldType.Integer, name: "level", alias: "Level"),
        new(type: FieldType.String, name: "parkName", alias: "Park", length: 200),
        new(type: FieldType.Integer, name: "successChancePercent", alias: "Recruit chance"),
        new(type: FieldType.Integer, name: "baseRecruitmentSeconds", alias: "Recruit seconds"),
        new(type: FieldType.String, name: "expiresAtUtc", alias: "Expires", length: 64)
    ];

    /// <summary>Fields declared for the client-side player-location FeatureLayer source.</summary>
    public static readonly IReadOnlyList<Field> PlayerLocationFields =
    [
        new(type: FieldType.Oid, name: PlayerObjectIdField, alias: "Player location ID", nullable: false),
        new(type: FieldType.String, name: "_ruckrType", alias: "RuckR type", length: 40),
        new(type: FieldType.Double, name: "accuracyMeters", alias: "Accuracy meters"),
        new(type: FieldType.String, name: PlayerLocationQualityField, alias: "GPS quality", length: 32),
        new(type: FieldType.String, name: "positionTimestampUtc", alias: "Timestamp", length: 64)
    ];

    /// <summary>Fields declared for the client-side player-accuracy FeatureLayer source.</summary>
    public static readonly IReadOnlyList<Field> PlayerAccuracyFields =
    [
        new(type: FieldType.Oid, name: PlayerAccuracyObjectIdField, alias: "Accuracy ID", nullable: false),
        new(type: FieldType.String, name: "_ruckrType", alias: "RuckR type", length: 40),
        new(type: FieldType.Double, name: "accuracyMeters", alias: "Accuracy meters"),
        new(type: FieldType.String, name: PlayerLocationQualityField, alias: "GPS quality", length: 32),
        new(type: FieldType.String, name: "positionTimestampUtc", alias: "Timestamp", length: 64)
    ];

    /// <summary>Create a native candidate-place point feature.</summary>
    public static Graphic CreateCandidatePlaceFeature(PitchCandidatePlaceDto candidate, int objectId) =>
        new(
            geometry: new Point(longitude: candidate.Longitude, latitude: candidate.Latitude),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                [CandidateObjectIdField] = objectId,
                ["_ruckrType"] = "candidate-place",
                ["_ruckrPlaceId"] = candidate.PlaceId,
                ["name"] = candidate.Name,
                [CandidateRecommendedPitchTypeField] = candidate.RecommendedPitchType,
                ["confidence"] = candidate.Confidence,
                ["categoryLabel"] = candidate.CategoryLabel,
                ["matchReason"] = candidate.MatchReason
            }));

    /// <summary>Create a native recruitable encounter point feature.</summary>
    public static Graphic CreateEncounterFeature(PlayerEncounterDto encounter, int objectId, bool isSpotlight = false) =>
        new(
            geometry: new Point(longitude: encounter.Longitude, latitude: encounter.Latitude),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                [EncounterObjectIdField] = objectId,
                ["_ruckrType"] = "encounter",
                ["_ruckrSpotlight"] = isSpotlight ? 1 : 0,
                ["_ruckrEncounterId"] = encounter.EncounterId.ToString(),
                ["_ruckrPlayerId"] = encounter.PlayerId,
                ["name"] = encounter.Name,
                ["position"] = encounter.Position,
                [EncounterRarityField] = encounter.Rarity,
                ["level"] = encounter.Level,
                ["parkName"] = encounter.ParkName,
                ["successChancePercent"] = encounter.SuccessChancePercent,
                ["baseRecruitmentSeconds"] = encounter.BaseRecruitmentSeconds,
                ["expiresAtUtc"] = encounter.ExpiresAtUtc.ToString("O")
            }));

    /// <summary>Create a native current-player point feature.</summary>
    public static Graphic CreatePlayerLocationFeature(GeoPosition position) =>
        new(
            geometry: new Point(longitude: position.Longitude, latitude: position.Latitude),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                [PlayerObjectIdField] = 1,
                ["_ruckrType"] = "player-location",
                ["accuracyMeters"] = position.Accuracy,
                [PlayerLocationQualityField] = ResolvePlayerLocationQuality(position.Accuracy),
                ["positionTimestampUtc"] = position.Timestamp.ToString("O")
            }));

    /// <summary>Create a native current-player GPS accuracy polygon feature.</summary>
    public static Graphic CreatePlayerAccuracyFeature(GeoPosition position)
    {
        var accuracyMeters = Math.Max(position.Accuracy ?? 0, 5);
        var center = new Point(longitude: position.Longitude, latitude: position.Latitude);
        var quality = ResolvePlayerLocationQuality(position.Accuracy);

        return new Graphic(
            geometry: new Polygon(
                rings: [CreateGeodesicRing(position.Latitude, position.Longitude, accuracyMeters)],
                spatialReference: SpatialReference.Wgs84,
                centroid: center),
            attributes: new AttributesDictionary(new Dictionary<string, object?>
            {
                [PlayerAccuracyObjectIdField] = 1,
                ["_ruckrType"] = "player-location-accuracy",
                ["accuracyMeters"] = position.Accuracy,
                [PlayerLocationQualityField] = quality,
                ["positionTimestampUtc"] = position.Timestamp.ToString("O")
            }));
    }

    /// <summary>Create the ArcGIS unique-value renderer for candidate pitch categories.</summary>
    public static UniqueValueRenderer CreateCandidatePlaceRenderer() =>
        new(
            defaultLabel: "Pitch candidate",
            defaultSymbol: CreateCandidatePlaceSymbol("Other"),
            field: CandidateRecommendedPitchTypeField,
            uniqueValueInfos:
            [
                new("Standard candidate", CreateCandidatePlaceSymbol(PitchType.Standard.ToString()), PitchType.Standard.ToString()),
                new("Training candidate", CreateCandidatePlaceSymbol(PitchType.Training.ToString()), PitchType.Training.ToString()),
                new("Stadium candidate", CreateCandidatePlaceSymbol(PitchType.Stadium.ToString()), PitchType.Stadium.ToString())
            ]);

    /// <summary>Create the ArcGIS unique-value renderer for encounter rarity categories.</summary>
    public static UniqueValueRenderer CreateEncounterRenderer() =>
        new(
            defaultLabel: "Recruit",
            defaultSymbol: CreateEncounterSymbol("Common"),
            field: EncounterRarityField,
            uniqueValueInfos:
            [
                new("Common recruit", CreateEncounterSymbol("Common"), "Common"),
                new("Uncommon recruit", CreateEncounterSymbol("Uncommon"), "Uncommon"),
                new("Rare recruit", CreateEncounterSymbol("Rare"), "Rare"),
                new("Epic recruit", CreateEncounterSymbol("Epic"), "Epic"),
                new("Legendary recruit", CreateEncounterSymbol("Legendary"), "Legendary")
            ]);

    /// <summary>Create the ArcGIS popup template for recruitable encounters.</summary>
    public static PopupTemplate CreateEncounterPopupTemplate(Func<Task> openRecruitBoard) =>
        new(
            title: "{name}",
            stringContent:
                "<strong>{rarity} {position}</strong><br />" +
                "Level {level}<br />" +
                "Park: {parkName}<br />" +
                "Recruit chance: {successChancePercent}%<br />" +
                "Recruit time: {baseRecruitmentSeconds}s<br />" +
                "Expires: {expiresAtUtc}",
            outFields: OutFields,
            fieldInfos: null,
            content: null,
            expressionInfos: null,
            overwriteActions: true,
            returnGeometry: false,
            actions:
            [
                new ActionButton(
                    title: "Open recruit board",
                    image: string.Empty,
                    actionId: "ruckr-open-recruit-board",
                    callbackFunction: openRecruitBoard,
                    className: "esri-icon-user",
                    active: false,
                    disabled: false,
                    visible: true)
            ]);

    /// <summary>Create the ArcGIS unique-value renderer for player GPS point quality.</summary>
    public static UniqueValueRenderer CreatePlayerLocationRenderer() =>
        new(
            defaultLabel: "Player location",
            defaultSymbol: CreatePlayerLocationSymbol("unknown"),
            field: PlayerLocationQualityField,
            uniqueValueInfos:
            [
                new("GPS ready", CreatePlayerLocationSymbol("ready"), "ready"),
                new("Weak GPS", CreatePlayerLocationSymbol("weak"), "weak"),
                new("GPS unknown", CreatePlayerLocationSymbol("unknown"), "unknown")
            ]);

    /// <summary>Create the ArcGIS unique-value renderer for player GPS accuracy quality.</summary>
    public static UniqueValueRenderer CreatePlayerAccuracyRenderer() =>
        new(
            defaultLabel: "GPS accuracy",
            defaultSymbol: CreatePlayerAccuracySymbol("unknown"),
            field: PlayerLocationQualityField,
            uniqueValueInfos:
            [
                new("Ready accuracy", CreatePlayerAccuracySymbol("ready"), "ready"),
                new("Weak accuracy", CreatePlayerAccuracySymbol("weak"), "weak"),
                new("Unknown accuracy", CreatePlayerAccuracySymbol("unknown"), "unknown")
            ]);

    /// <summary>Resolve the visual quality bucket for a player GPS fix.</summary>
    public static string ResolvePlayerLocationQuality(double? accuracyMeters) =>
        accuracyMeters switch
        {
            null => "unknown",
            <= ReadyAccuracyMeters => "ready",
            _ => "weak"
        };

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

    private static SimpleMarkerSymbol CreateEncounterSymbol(string rarity) =>
        new(
            outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(3)),
            color: EncounterRarityColor(rarity),
            size: new Dimension(19),
            style: SimpleMarkerSymbolStyle.Diamond);

    private static MapColor EncounterRarityColor(string rarity) => rarity switch
    {
        "Uncommon" => new MapColor(47, 133, 90),
        "Rare" => new MapColor(37, 99, 235),
        "Epic" => new MapColor(217, 119, 6),
        "Legendary" => new MapColor(200, 30, 30),
        _ => new MapColor(107, 114, 128)
    };

    private static SimpleMarkerSymbol CreateCandidatePlaceSymbol(string recommendedPitchType)
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

    private static SimpleMarkerSymbol CreatePlayerLocationSymbol(string quality) =>
        new(
            outline: new Outline(color: PlayerLocationOutlineColor(quality), width: new Dimension(3)),
            color: PlayerLocationColor(quality),
            size: new Dimension(16),
            style: SimpleMarkerSymbolStyle.Circle);

    private static SimpleFillSymbol CreatePlayerAccuracySymbol(string quality) =>
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
