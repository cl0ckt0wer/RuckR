using dymaptic.GeoBlazor.Core.Components;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Parses RuckR entity identifiers from GeoBlazor graphic attributes.
/// </summary>
public static class MapGraphicSelection
{
    /// <summary>Parse a selected map graphic into a RuckR selection descriptor.</summary>
    /// <param name="graphic">Hit-test graphic.</param>
    /// <returns>The parsed selection, or <c>null</c> when the graphic is not a selectable RuckR entity.</returns>
    public static MapSelection? FromGraphic(Graphic graphic)
    {
        var attrs = graphic.Attributes;
        if (!attrs.TryGetValue("_ruckrType", out var type))
        {
            return null;
        }

        return type?.ToString() switch
        {
            "pitch" => TryGetInt(attrs, "_ruckrId", out var pitchId)
                ? MapSelection.Pitch(pitchId)
                : null,
            "encounter" => TryGetGuid(attrs, "_ruckrEncounterId", out var encounterId)
                ? MapSelection.Encounter(encounterId)
                : null,
            "candidate-place" => TryGetString(attrs, "_ruckrPlaceId", out var placeId)
                ? MapSelection.CandidatePlace(
                    placeId,
                    ReadString(attrs, "name", "Candidate place"),
                    ReadString(attrs, "recommendedPitchType", "Pitch"),
                    ReadString(attrs, "confidence", "?"),
                    ReadString(attrs, "matchReason", "matched place category"),
                    ReadString(attrs, "categoryLabel", "Place"))
                : null,
            _ => null
        };
    }

    private static bool TryGetInt(
        dymaptic.GeoBlazor.Core.Model.AttributesDictionary attrs,
        string key,
        out int value)
    {
        if (attrs.TryGetValue(key, out var raw) && raw is int intValue)
        {
            value = intValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetGuid(
        dymaptic.GeoBlazor.Core.Model.AttributesDictionary attrs,
        string key,
        out Guid value)
    {
        if (attrs.TryGetValue(key, out var raw) && Guid.TryParse(raw?.ToString(), out var guidValue))
        {
            value = guidValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(
        dymaptic.GeoBlazor.Core.Model.AttributesDictionary attrs,
        string key,
        out string value)
    {
        if (attrs.TryGetValue(key, out var raw) && raw?.ToString() is { Length: > 0 } stringValue)
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ReadString(
        dymaptic.GeoBlazor.Core.Model.AttributesDictionary attrs,
        string key,
        string fallback) =>
        attrs.TryGetValue(key, out var raw) && raw?.ToString() is { Length: > 0 } value
            ? value
            : fallback;
}

/// <summary>Parsed RuckR map selection.</summary>
public sealed record MapSelection(
    MapSelectionKind Kind,
    int? PitchId = null,
    Guid? EncounterId = null,
    string? CandidatePlaceId = null,
    string? CandidateName = null,
    string? CandidateRecommendedPitchType = null,
    string? CandidateConfidence = null,
    string? CandidateMatchReason = null,
    string? CandidateCategoryLabel = null)
{
    /// <summary>Create a pitch selection.</summary>
    /// <param name="pitchId">Selected pitch id.</param>
    /// <returns>A pitch selection descriptor.</returns>
    public static MapSelection Pitch(int pitchId) => new(MapSelectionKind.Pitch, PitchId: pitchId);

    /// <summary>Create an encounter selection.</summary>
    /// <param name="encounterId">Selected encounter id.</param>
    /// <returns>An encounter selection descriptor.</returns>
    public static MapSelection Encounter(Guid encounterId) => new(MapSelectionKind.Encounter, EncounterId: encounterId);

    /// <summary>Create a candidate-place selection.</summary>
    /// <param name="placeId">Selected candidate place id.</param>
    /// <param name="name">Candidate place display name.</param>
    /// <param name="recommendedPitchType">Recommended pitch type.</param>
    /// <param name="confidence">Match confidence text.</param>
    /// <param name="matchReason">Match reason text.</param>
    /// <param name="categoryLabel">Candidate category label.</param>
    /// <returns>A candidate-place selection descriptor.</returns>
    public static MapSelection CandidatePlace(
        string placeId,
        string name,
        string recommendedPitchType,
        string confidence,
        string matchReason,
        string categoryLabel) =>
        new(
            MapSelectionKind.CandidatePlace,
            CandidatePlaceId: placeId,
            CandidateName: name,
            CandidateRecommendedPitchType: recommendedPitchType,
            CandidateConfidence: confidence,
            CandidateMatchReason: matchReason,
            CandidateCategoryLabel: categoryLabel);
}

/// <summary>RuckR map selection kind.</summary>
public enum MapSelectionKind
{
    /// <summary>A pitch marker was selected.</summary>
    Pitch,

    /// <summary>A recruitable encounter marker was selected.</summary>
    Encounter,

    /// <summary>A candidate pitch place marker was selected.</summary>
    CandidatePlace
}
