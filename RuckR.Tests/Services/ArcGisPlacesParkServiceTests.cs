using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RuckR.Server.Services;

namespace RuckR.Tests.Services;

public sealed class ArcGisPlacesParkServiceTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "ruckr-places-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindNearbyPitchCandidatePlacesAsync_ReusesMemoryCache_ForSameSearchBucket()
    {
        var handler = new CountingPlacesHandler();
        var service = CreateService(handler);

        var first = await service.FindNearbyPitchCandidatePlacesAsync(51.5074, -0.1278, 5_000);
        var second = await service.FindNearbyPitchCandidatePlacesAsync(51.5074, -0.1278, 5_000);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task FindNearbyPitchCandidatePlacesAsync_ReusesDiskCache_AcrossServiceInstances()
    {
        var firstHandler = new CountingPlacesHandler();
        var firstService = CreateService(firstHandler);
        var first = await firstService.FindNearbyPitchCandidatePlacesAsync(51.5074, -0.1278, 5_000);
        Assert.Single(first);

        var secondHandler = new CountingPlacesHandler();
        var secondService = CreateService(secondHandler);

        var second = await secondService.FindNearbyPitchCandidatePlacesAsync(51.5074, -0.1278, 5_000);

        Assert.Single(second);
        Assert.Equal(1, firstHandler.RequestCount);
        Assert.Equal(0, secondHandler.RequestCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    private ArcGisPlacesParkService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArcGISPlacesApiKey"] = "test-token",
                ["ArcGISPlacesCacheDirectory"] = _cacheRoot
            })
            .Build();

        return new ArcGisPlacesParkService(
            new HttpClient(handler),
            configuration,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ArcGisPlacesParkService>.Instance);
    }

    private sealed class CountingPlacesHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            const string payload = """
            {
              "results": [
                {
                  "placeId": "test-place-1",
                  "name": "Central Athletic Field",
                  "location": { "x": -0.1278, "y": 51.5074 },
                  "distance": 42,
                  "categories": [
                    { "label": "Athletic Field" }
                  ]
                }
              ]
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        }
    }
}
