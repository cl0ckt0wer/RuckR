using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RuckR.Server.Services;

public sealed class ArcGisPlacesParkService : IRealWorldParkService
{
    private const string PlacesBaseUrl = "https://places-api.arcgis.com/arcgis/rest/services/places-service/v1";
    private const string DefaultArcGisReferrer = "https://ruckr.exe.xyz/";
    private const double MaxPlacesRadiusMeters = 10_000.0;
    private const int PageSize = 20;

    private static readonly TimeSpan ParkSearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CategoryCacheDuration = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArcGisPlacesParkService> _logger;

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
