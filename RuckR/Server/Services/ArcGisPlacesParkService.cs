using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side class ArcGisPlacesParkService.</summary>
public sealed class ArcGisPlacesParkService : IRealWorldParkService
{
    private const string PlacesBaseUrl = "https://places-api.arcgis.com/arcgis/rest/services/places-service/v1";
    private const string DefaultArcGisReferrer = "https://ruckr.exe.xyz/";
    private const double MaxPlacesRadiusMeters = 10_000.0;
    private const int PageSize = 20;

    private static readonly TimeSpan ParkSearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PitchCandidateSearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CategoryCacheDuration = TimeSpan.FromHours(24);
    private static readonly string[] DefaultPitchCandidateCategoryIds =
    [
        "56aa371be4b08b9a8d573556", // Rugby Stadium
        "63be6904847c3692a84b9c14", // Rugby
        "52e81612bcbc57f1066b7a2c", // Rugby Pitch
        "63be6904847c3692a84b9bfd", // Athletic Field
        "4bf58dd8d48988d184941735", // Stadium
        "4bf58dd8d48988d189941735", // Football Stadium
        "63be6904847c3692a84b9c07", // Football Field
        "4bf58dd8d48988d188941735", // Soccer Stadium
        "4cce455aebf7b749d5e191f5"  // Soccer Field
    ];

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArcGisPlacesParkService> _logger;
    /// <summary>Initializes a new instance of ArcGisPlacesParkService.</summary>
    /// <param name="httpClient">The httpclient.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="cache">The cache.</param>
    /// <param name="logger">The logger.</param>
    public ArcGisPlacesParkService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<ArcGisPlacesParkService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }
    /// <summary>F in dN ea rb yP ar ks As yn c.</summary>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    /// <param name="radiusMeters">The radiusmeters.</param>
    /// <param name="cancellationToken">The cancellationtoken.</param>
    /// <returns>The operation result.</returns>
    public async Task<IReadOnlyList<RealWorldPark>> FindNearbyParksAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var token = ResolvePlacesToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("ArcGIS Places is not configured; recruitable players will not spawn.");
            return Array.Empty<RealWorldPark>();
        }

        var clampedRadius = Math.Clamp(radiusMeters, 1.0, MaxPlacesRadiusMeters);
        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"arcgis-places:parks:{Math.Round(latitude, 3)}:{Math.Round(longitude, 3)}:{Math.Round(clampedRadius)}");

        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ParkSearchCacheDuration;
            return await QueryNearbyParksAsync(latitude, longitude, clampedRadius, token, cancellationToken);
        });

        return cached ?? Array.Empty<RealWorldPark>();
    }
    /// <summary>F in dN ea rb yP it ch Ca nd id at eP la ce sA sy nc.</summary>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    /// <param name="radiusMeters">The radiusmeters.</param>
    /// <param name="cancellationToken">The cancellationtoken.</param>
    /// <returns>The operation result.</returns>
    public async Task<IReadOnlyList<PitchCandidatePlaceDto>> FindNearbyPitchCandidatePlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var token = ResolvePlacesToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("ArcGIS Places is not configured; pitch candidate overlay will be empty.");
            return Array.Empty<PitchCandidatePlaceDto>();
        }

        var clampedRadius = Math.Clamp(radiusMeters, 1.0, MaxPlacesRadiusMeters);
        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"arcgis-places:pitch-candidates:{Math.Round(latitude, 3)}:{Math.Round(longitude, 3)}:{Math.Round(clampedRadius)}");

        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PitchCandidateSearchCacheDuration;
            return await QueryNearbyPitchCandidatePlacesAsync(latitude, longitude, clampedRadius, token, cancellationToken);
        });

        return cached ?? Array.Empty<PitchCandidatePlaceDto>();
    }

    private async Task<IReadOnlyList<RealWorldPark>> QueryNearbyParksAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var categoryIds = await GetParkCategoryIdsAsync(token, cancellationToken);
            var query = new Dictionary<string, string?>
            {
                ["f"] = "json",
                ["x"] = longitude.ToString(CultureInfo.InvariantCulture),
                ["y"] = latitude.ToString(CultureInfo.InvariantCulture),
                ["radius"] = radiusMeters.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = PageSize.ToString(CultureInfo.InvariantCulture),
                ["token"] = token
            };

            if (categoryIds.Count > 0)
            {
                query["categoryIds"] = string.Join(",", categoryIds);
            }
            else
            {
                query["searchText"] = "park";
            }

            using var response = await GetArcGisAsync(
                BuildUrl($"{PlacesBaseUrl}/places/near-point", query),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ArcGIS Places nearby park search failed with status {StatusCode}.",
                    (int)response.StatusCode);
                return Array.Empty<RealWorldPark>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseParkResults(document.RootElement, categoryIds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS Places nearby park search failed.");
            return Array.Empty<RealWorldPark>();
        }
    }

    private async Task<IReadOnlyList<PitchCandidatePlaceDto>> QueryNearbyPitchCandidatePlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var categoryIds = await GetPitchCandidateCategoryIdsAsync(token, cancellationToken);
            var query = new Dictionary<string, string?>
            {
                ["f"] = "json",
                ["x"] = longitude.ToString(CultureInfo.InvariantCulture),
                ["y"] = latitude.ToString(CultureInfo.InvariantCulture),
                ["radius"] = radiusMeters.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = PageSize.ToString(CultureInfo.InvariantCulture),
                ["token"] = token
            };

            if (categoryIds.Count > 0)
            {
                query["categoryIds"] = string.Join(",", categoryIds);
            }
            else
            {
                query["searchText"] = "athletic field stadium sports park";
            }

            using var response = await GetArcGisAsync(
                BuildUrl($"{PlacesBaseUrl}/places/near-point", query),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ArcGIS Places pitch candidate search failed with status {StatusCode}.",
                    (int)response.StatusCode);
                return Array.Empty<PitchCandidatePlaceDto>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParsePitchCandidateResults(document.RootElement);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS Places pitch candidate search failed.");
            return Array.Empty<PitchCandidatePlaceDto>();
        }
    }

    private async Task<IReadOnlyList<string>> GetParkCategoryIdsAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var configured = ResolveConfiguredCategoryIds();
        if (configured.Count > 0)
        {
            return configured;
        }

        var cached = await _cache.GetOrCreateAsync("arcgis-places:park-category-ids", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CategoryCacheDuration;
            return await QueryParkCategoryIdsAsync(token, cancellationToken);
        });

        return cached ?? Array.Empty<string>();
    }

    private async Task<IReadOnlyList<string>> GetPitchCandidateCategoryIdsAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var configured = ResolveConfiguredPitchCandidateCategoryIds();
        if (configured.Count > 0)
        {
            return configured;
        }

        return DefaultPitchCandidateCategoryIds;
    }

    private async Task<IReadOnlyList<string>> QueryParkCategoryIdsAsync(
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new Dictionary<string, string?>
            {
                ["f"] = "json",
                ["filter"] = "park",
                ["token"] = token
            };

            using var response = await GetArcGisAsync(
                BuildUrl($"{PlacesBaseUrl}/categories", query),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ArcGIS Places category lookup failed with status {StatusCode}; falling back to text search.",
                    (int)response.StatusCode);
                return Array.Empty<string>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("categories", out var categories)
                || categories.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var ids = new List<string>();
            foreach (var category in categories.EnumerateArray())
            {
                if (!category.TryGetProperty("categoryId", out var idElement))
                    continue;

                var categoryId = idElement.GetString();
                if (string.IsNullOrWhiteSpace(categoryId))
                    continue;

                var labels = ReadFullLabels(category);
                if (labels.Any(IsRealWorldParkLabel))
                {
                    ids.Add(categoryId);
                }
            }

            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS Places category lookup failed; falling back to text search.");
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<string>> QueryPitchCandidateCategoryIdsAsync(
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var filters = new[] { "rugby", "stadium", "athletic", "sports", "football", "soccer", "park" };
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filter in filters)
            {
                var query = new Dictionary<string, string?>
                {
                    ["f"] = "json",
                    ["filter"] = filter,
                    ["token"] = token
                };

                using var response = await GetArcGisAsync(
                    BuildUrl($"{PlacesBaseUrl}/categories", query),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug(
                        "ArcGIS Places category lookup for filter {Filter} failed with status {StatusCode}.",
                        filter,
                        (int)response.StatusCode);
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("categories", out var categories)
                    || categories.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var category in categories.EnumerateArray())
                {
                    if (!category.TryGetProperty("categoryId", out var idElement))
                        continue;

                    var categoryId = idElement.GetString();
                    if (string.IsNullOrWhiteSpace(categoryId))
                        continue;

                    var labels = ReadFullLabels(category).ToArray();
                    if (labels.Any(IsPitchCandidateCategoryLabel))
                    {
                        ids.Add(categoryId);
                    }
                }
            }

            return ids.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS Places pitch candidate category lookup failed; falling back to text search.");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<RealWorldPark> ParseParkResults(JsonElement root, IReadOnlyList<string> categoryIds)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RealWorldPark>();
        }

        var parks = new List<RealWorldPark>();
        foreach (var result in results.EnumerateArray())
        {
            if (!ResultLooksLikePark(result))
                continue;

            if (!result.TryGetProperty("location", out var location)
                || !location.TryGetProperty("x", out var xElement)
                || !location.TryGetProperty("y", out var yElement)
                || !xElement.TryGetDouble(out var longitude)
                || !yElement.TryGetDouble(out var latitude))
            {
                continue;
            }

            var placeId = result.TryGetProperty("placeId", out var placeIdElement)
                ? placeIdElement.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            var name = result.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? "Park"
                : "Park";

            var distance = result.TryGetProperty("distance", out var distanceElement)
                && distanceElement.TryGetDouble(out var distanceMeters)
                    ? distanceMeters
                    : 0.0;

            parks.Add(new RealWorldPark(placeId, name, latitude, longitude, distance));
        }

        return parks;
    }

    private static IReadOnlyList<PitchCandidatePlaceDto> ParsePitchCandidateResults(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PitchCandidatePlaceDto>();
        }

        var candidates = new List<PitchCandidatePlaceDto>();
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("location", out var location)
                || !location.TryGetProperty("x", out var xElement)
                || !location.TryGetProperty("y", out var yElement)
                || !xElement.TryGetDouble(out var longitude)
                || !yElement.TryGetDouble(out var latitude))
            {
                continue;
            }

            var placeId = result.TryGetProperty("placeId", out var placeIdElement)
                ? placeIdElement.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            var name = result.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? "Candidate place"
                : "Candidate place";

            var labels = ReadResultCategoryLabels(result);
            if (!TryClassifyPitchCandidate(name, labels, out var pitchType, out var reason, out var confidence))
            {
                continue;
            }

            var distance = result.TryGetProperty("distance", out var distanceElement)
                && distanceElement.TryGetDouble(out var distanceMeters)
                    ? distanceMeters
                    : 0.0;

            candidates.Add(new PitchCandidatePlaceDto(
                placeId,
                name,
                latitude,
                longitude,
                distance,
                labels.FirstOrDefault() ?? "Place",
                pitchType,
                reason,
                confidence));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.DistanceMeters)
            .Take(PageSize)
            .ToArray();
    }

    private string? ResolvePlacesToken()
    {
        return FirstConfiguredValue(
            "ArcGISPlacesApiKey",
            "ArcGIS:PlacesApiKey",
            "ARC_GIS_PLACES_API_KEY",
            "ArcGISApiKey",
            "ARC_GIS_API_KEY");
    }

    private string ResolveArcGisReferrer()
    {
        var configured = FirstConfiguredValue(
            "ArcGISReferrer",
            "ArcGIS:Referrer",
            "ARC_GIS_REFERRER");

        return string.IsNullOrWhiteSpace(configured)
            ? DefaultArcGisReferrer
            : configured;
    }

    private IReadOnlyList<string> ResolveConfiguredCategoryIds()
    {
        var value = FirstConfiguredValue(
            "ArcGISPlacesParkCategoryIds",
            "ArcGIS:PlacesParkCategoryIds",
            "ARC_GIS_PLACES_PARK_CATEGORY_IDS");

        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    private IReadOnlyList<string> ResolveConfiguredPitchCandidateCategoryIds()
    {
        var value = FirstConfiguredValue(
            "ArcGISPlacesPitchCandidateCategoryIds",
            "ArcGIS:PlacesPitchCandidateCategoryIds",
            "ARC_GIS_PLACES_PITCH_CANDIDATE_CATEGORY_IDS");

        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    private string? FirstConfiguredValue(params string[] names)
    {
        foreach (var name in names)
        {
            var value = _configuration[name] ?? Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> ReadFullLabels(JsonElement category)
    {
        if (!category.TryGetProperty("fullLabel", out var fullLabel)
            || fullLabel.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var labels = fullLabel
            .EnumerateArray()
            .Where(label => label.ValueKind == JsonValueKind.String)
            .Select(label => label.GetString())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();

        if (labels.Length > 0)
        {
            yield return string.Join(" > ", labels);
            yield break;
        }

        foreach (var labelPath in fullLabel.EnumerateArray())
        {
            if (labelPath.ValueKind != JsonValueKind.Array)
                continue;

            labels = labelPath
                .EnumerateArray()
                .Select(label => label.GetString())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToArray();

            yield return string.Join(" > ", labels);
        }
    }

    private static IReadOnlyList<string> ReadResultCategoryLabels(JsonElement result)
    {
        if (!result.TryGetProperty("categories", out var categories)
            || categories.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var labels = new List<string>();
        foreach (var category in categories.EnumerateArray())
        {
            if (category.TryGetProperty("label", out var label)
                && label.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(label.GetString()))
            {
                labels.Add(label.GetString()!);
            }

            labels.AddRange(ReadFullLabels(category));
        }

        return labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsRealWorldParkLabel(string labelPath)
    {
        var labels = labelPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var leaf = labels.LastOrDefault() ?? string.Empty;
        var parentPath = string.Join(" > ", labels.Take(Math.Max(0, labels.Length - 1)));

        return parentPath.Contains("Landmarks and Outdoors", StringComparison.OrdinalIgnoreCase)
            && leaf.Contains("Park", StringComparison.OrdinalIgnoreCase)
            && !leaf.Contains("Parking", StringComparison.OrdinalIgnoreCase)
            && !leaf.Contains("Amusement", StringComparison.OrdinalIgnoreCase)
            && !leaf.Contains("Water Park", StringComparison.OrdinalIgnoreCase)
            && !leaf.Contains("RV Park", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPitchCandidateCategoryLabel(string labelPath)
    {
        if (IsExcludedPitchCandidate(labelPath))
        {
            return false;
        }

        var value = labelPath.ToLowerInvariant();
        return value.Contains("rugby", StringComparison.Ordinal)
            || value.Contains("stadium", StringComparison.Ordinal)
            || value.Contains("arena", StringComparison.Ordinal)
            || value.Contains("athletic field", StringComparison.Ordinal)
            || value.Contains("sports field", StringComparison.Ordinal)
            || value.Contains("sports complex", StringComparison.Ordinal)
            || value.Contains("playing field", StringComparison.Ordinal)
            || value.Contains("football", StringComparison.Ordinal)
            || value.Contains("soccer", StringComparison.Ordinal)
            || value.Contains("recreation", StringComparison.Ordinal)
            || value.Contains("park", StringComparison.Ordinal);
    }

    private static bool TryClassifyPitchCandidate(
        string name,
        IReadOnlyList<string> labels,
        out string recommendedPitchType,
        out string matchReason,
        out int confidence)
    {
        var haystack = string.Join(" ", new[] { name }.Concat(labels));
        if (IsExcludedPitchCandidate(haystack))
        {
            recommendedPitchType = string.Empty;
            matchReason = string.Empty;
            confidence = 0;
            return false;
        }

        if (haystack.Contains("rugby", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Standard.ToString();
            matchReason = "Rugby signal";
            confidence = 98;
            return true;
        }

        if (haystack.Contains("stadium", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("arena", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Stadium.ToString();
            matchReason = "Stadium venue";
            confidence = 92;
            return true;
        }

        if (haystack.Contains("athletic field", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("sports field", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("playing field", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Standard.ToString();
            matchReason = "Field suitable for match play";
            confidence = 88;
            return true;
        }

        if (haystack.Contains("sports complex", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("recreation ground", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Standard.ToString();
            matchReason = "Multi-field sports venue";
            confidence = 82;
            return true;
        }

        if (haystack.Contains("football", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("soccer", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Standard.ToString();
            matchReason = "Field sport venue";
            confidence = 74;
            return true;
        }

        if (haystack.Contains("park", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("recreation", StringComparison.OrdinalIgnoreCase))
        {
            recommendedPitchType = PitchType.Training.ToString();
            matchReason = "Park or recreation area";
            confidence = 58;
            return true;
        }

        recommendedPitchType = string.Empty;
        matchReason = string.Empty;
        confidence = 0;
        return false;
    }

    private static bool IsExcludedPitchCandidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var excludedTerms = new[]
        {
            "airport parking",
            "amusement",
            "bar",
            "baseball",
            "basketball",
            "dog park",
            "golf",
            "gym",
            "parking",
            "restaurant",
            "rv park",
            "skate park",
            "swimming",
            "tennis",
            "water park"
        };

        return excludedTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ResultLooksLikePark(JsonElement result)
    {
        var placeName = result.TryGetProperty("name", out var name)
            ? name.GetString() ?? string.Empty
            : string.Empty;

        if (IsExcludedParkResult(placeName))
        {
            return false;
        }

        if (placeName.Contains("park", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!result.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return categories.EnumerateArray().Any(category =>
            category.TryGetProperty("label", out var label)
            && label.GetString() is { } labelValue
            && labelValue.Contains("park", StringComparison.OrdinalIgnoreCase)
            && !IsExcludedParkResult(labelValue));
    }

    private static bool IsExcludedParkResult(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var excludedTerms = new[]
        {
            "animal shelter",
            "airport parking",
            "garage",
            "humane society",
            "kennel",
            "park and fly",
            "parking",
            "rv park",
            "veterinary"
        };

        return excludedTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase))
            || (value.Contains("dog", StringComparison.OrdinalIgnoreCase)
                && value.Contains("beach", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("park", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string?> query)
    {
        var parts = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");

        return $"{baseUrl}?{string.Join("&", parts)}";
    }

    private async Task<HttpResponseMessage> GetArcGisAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri(ResolveArcGisReferrer());
        return await _httpClient.SendAsync(request, cancellationToken);
    }
}

