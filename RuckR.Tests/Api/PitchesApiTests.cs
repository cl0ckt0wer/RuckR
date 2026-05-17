using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using RuckR.Server.Services;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

    /// <summary>
    /// Provides access to :.
    /// </summary>
[Collection(nameof(TestCollection))]
public class PitchesApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""PitchesApiTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    public PitchesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        var username = $"pitchapi_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, username);
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        _client?.Dispose();
    }

    /// <summary>
    /// Verifies create Pitch Authenticated Returns Created Pitch.
    /// </summary>
    [Fact]
    public async Task CreatePitch_Authenticated_ReturnsCreatedPitch()
    {
        var pitchName = $"TestPitch_{Guid.NewGuid():N}";
        var request = new CreatePitchRequest(pitchName, 51.5074, -0.1278, "Training");
        var response = await _client.PostAsJsonAsync("/api/pitches", request);

        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 201 or 200 but got {(int)response.StatusCode}");

        var pitch = await response.Content.ReadFromJsonAsync<PitchModel>();
        Assert.NotNull(pitch);
        Assert.Equal(pitchName, pitch.Name);
        Assert.Equal(PitchType.Training, pitch.Type);
        Assert.Equal(_userId, pitch.CreatorUserId);
        // Location is excluded from JSON serialization (NetTopologySuite geometry cycle)
    }

    /// <summary>
    /// Verifies create Pitch Unauthenticated Returns Unauthorized.
    /// </summary>
    [Fact]
    public async Task CreatePitch_Unauthenticated_ReturnsUnauthorized()
    {
        using var anonymousClient = _factory.CreateClient();
        var request = new CreatePitchRequest($"AnonPitch_{Guid.NewGuid():N}", 51.5074, -0.1278, "Training");

        var response = await anonymousClient.PostAsJsonAsync("/api/pitches", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies create Pitch Invalid Request Returns Bad Request.
    /// </summary>
    [Theory]
    [InlineData(91, -0.1278, "Standard", "Latitude")]
    [InlineData(-91, -0.1278, "Standard", "Latitude")]
    [InlineData(51.5074, 181, "Standard", "Longitude")]
    [InlineData(51.5074, -181, "Standard", "Longitude")]
    [InlineData(51.5074, -0.1278, "InvalidType", "Invalid pitch type")]
    public async Task CreatePitch_InvalidRequest_ReturnsBadRequest(
        double latitude,
        double longitude,
        string type,
        string expectedMessage)
    {
        var request = new CreatePitchRequest($"InvalidPitch_{Guid.NewGuid():N}", latitude, longitude, type);

        var response = await _client.PostAsJsonAsync("/api/pitches", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedMessage, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies create Pitch Authenticated Persists Location And Metadata.
    /// </summary>
    [Fact]
    public async Task CreatePitch_Authenticated_PersistsLocationAndMetadata()
    {
        var pitchName = $"PersistPitch_{Guid.NewGuid():N}";
        var latitude = 51.4564;
        var longitude = -0.3416;
        var request = new CreatePitchRequest(pitchName, latitude, longitude, "Stadium");

        var response = await _client.PostAsJsonAsync("/api/pitches", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PitchModel>();
        Assert.NotNull(created);

        await _factory.ExecuteInDbAsync(async db =>
        {
            var stored = await db.Pitches.FindAsync(created.Id);
            Assert.NotNull(stored);
            Assert.Equal(pitchName, stored.Name);
            Assert.Equal(_userId, stored.CreatorUserId);
            Assert.Equal(PitchType.Stadium, stored.Type);
            Assert.Equal(4326, stored.Location.SRID);
            Assert.Equal(longitude, stored.Location.X, precision: 4);
            Assert.Equal(latitude, stored.Location.Y, precision: 4);
            Assert.True(stored.CreatedAt <= DateTime.UtcNow);
        });
    }

    /// <summary>
    /// Verifies create Pitch From Candidate Authenticated Persists Source Metadata.
    /// </summary>
    [Fact]
    public async Task CreatePitchFromCandidate_Authenticated_PersistsSourceMetadata()
    {
        var pitchName = $"CandidatePitch_{Guid.NewGuid():N}";
        var placeId = $"candidate-place-{Guid.NewGuid():N}";
        var latitude = -42.1234;
        var longitude = 88.5678;
        var request = new CreatePitchFromCandidateRequest(
            pitchName,
            placeId,
            latitude,
            longitude,
            "Standard",
            "Rugby Pitch",
            "Rugby signal",
            98);

        var response = await _client.PostAsJsonAsync("/api/pitches/from-candidate", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PitchModel>();
        Assert.NotNull(created);
        Assert.Equal(pitchName, created.Name);
        Assert.Equal(PitchType.Standard, created.Type);
        Assert.Equal(latitude, created.Latitude, precision: 4);
        Assert.Equal(longitude, created.Longitude, precision: 4);

        await _factory.ExecuteInDbAsync(async db =>
        {
            var stored = await db.Pitches.FindAsync(created.Id);
            Assert.NotNull(stored);
            Assert.Equal("ArcGISPlaces", stored.Source);
            Assert.Equal(placeId, stored.ExternalPlaceId);
            Assert.Equal("Rugby Pitch", stored.SourceCategory);
            Assert.Equal("Rugby signal", stored.SourceMatchReason);
            Assert.Equal(98, stored.SourceConfidence);
        });
    }

    /// <summary>
    /// Verifies create Pitch From Candidate Duplicate Place Id Returns409.
    /// </summary>
    [Fact]
    public async Task CreatePitchFromCandidate_DuplicatePlaceId_Returns409()
    {
        var placeId = $"candidate-place-{Guid.NewGuid():N}";
        var first = new CreatePitchFromCandidateRequest(
            $"CandidatePitch_{Guid.NewGuid():N}",
            placeId,
            -43.1234,
            89.5678,
            "Standard",
            "Rugby Pitch",
            "Rugby signal",
            98);
        var second = first with
        {
            Name = $"CandidatePitch_{Guid.NewGuid():N}",
            Latitude = -44.1234,
            Longitude = 90.5678
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/pitches/from-candidate", first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/api/pitches/from-candidate", second);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies create Pitch From Candidate Near Existing Pitch Returns409.
    /// </summary>
    [Fact]
    public async Task CreatePitchFromCandidate_NearExistingPitch_Returns409()
    {
        var first = new CreatePitchFromCandidateRequest(
            $"CandidatePitch_{Guid.NewGuid():N}",
            $"candidate-place-{Guid.NewGuid():N}",
            -45.1234,
            91.5678,
            "Standard",
            "Rugby Pitch",
            "Rugby signal",
            98);
        var second = first with
        {
            Name = $"CandidatePitch_{Guid.NewGuid():N}",
            PlaceId = $"candidate-place-{Guid.NewGuid():N}",
            Latitude = -45.12345,
            Longitude = 91.56785
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/pitches/from-candidate", first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/api/pitches/from-candidate", second);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies get Pitches Nearby Returns Pitches.
    /// </summary>
    [Fact]
    public async Task GetPitchesNearby_ReturnsPitches()
    {
        var response = await _client.GetAsync("/api/pitches/nearby?lat=51.5074&lng=-0.1278&radius=5000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pitches = await response.Content.ReadFromJsonAsync<List<PitchModel>>();
        Assert.NotNull(pitches);
        Assert.NotEmpty(pitches);
    }

    /// <summary>
    /// Verifies create Pitch Duplicate Name Anywhere Returns409.
    /// </summary>
    [Fact]
    public async Task CreatePitch_DuplicateNameAnywhere_Returns409()
    {
        var pitchName = $"DupPitch_{Guid.NewGuid():N}";

        // First pitch at central London
        var request1 = new CreatePitchRequest(pitchName, 51.5074, -0.1278, "Standard");
        var response1 = await _client.PostAsJsonAsync("/api/pitches", request1);
        Assert.True(response1.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"First pitch creation should succeed but got {(int)response1.StatusCode}");

        // Same name should be rejected even at a distant location.
        var request2 = new CreatePitchRequest(pitchName, -33.8688, 151.2093, "Standard");
        var response2 = await _client.PostAsJsonAsync("/api/pitches", request2);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    /// <summary>
    /// Verifies create Pitch Rate Limit Exceeded Returns429.
    /// </summary>
    [Fact]
    public async Task CreatePitch_RateLimitExceeded_Returns429()
    {
        // Use a unique user for rate limit test to ensure clean counter
        var rateLimitUserId = await _factory.CreateTestUserAsync(
            $"ratelimit_{Guid.NewGuid():N}",
            "TestPass123!");
        using var rateLimitClient = _factory.CreateAuthenticatedClient(rateLimitUserId);

        // Create 5 pitches — all should succeed
        for (int i = 0; i < 5; i++)
        {
            var pitchName = $"RateLimitPitch_{i}_{Guid.NewGuid():N}";
            var request = new CreatePitchRequest(pitchName, 51.5074 + i * 0.001, -0.1278, "Standard");
            var response = await rateLimitClient.PostAsJsonAsync("/api/pitches", request);
            Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"Pitch {i + 1} should succeed but got {(int)response.StatusCode}");
        }

        // 6th request should be rate limited (429)
        var sixthRequest = new CreatePitchRequest($"RateLimitPitch_6_{Guid.NewGuid():N}", 51.5074, -0.1278, "Standard");
        var sixthResponse = await rateLimitClient.PostAsJsonAsync("/api/pitches", sixthRequest);
        Assert.Equal((HttpStatusCode)429, sixthResponse.StatusCode);
    }

    /// <summary>
    /// Verifies ensure Nearby Pitches No Stadium Within Thirty Miles Creates Stadium.
    /// </summary>
    [Fact]
    public async Task EnsureNearbyPitches_NoStadiumWithinThirtyMiles_CreatesStadium()
    {
        using var scope = _factory.Services.CreateScope();
        var pitchDiscovery = scope.ServiceProvider.GetRequiredService<IPitchDiscoveryService>();

        var pitches = await pitchDiscovery.EnsureNearbyPitchesAsync(
            _userId,
            "PitchFounder",
            -12.3456,
            98.7654);

        var stadium = Assert.Single(pitches, p => p.Type == PitchType.Stadium);
        Assert.Equal("PitchFounder's Stadium", stadium.Name);
        Assert.Equal(_userId, stadium.CreatorUserId);
    }

    /// <summary>
    /// Verifies ensure Nearby Pitches Stadium Within Thirty Miles But Not Ten Creates Standard Pitch.
    /// </summary>
    [Fact]
    public async Task EnsureNearbyPitches_StadiumWithinThirtyMilesButNotTen_CreatesStandardPitch()
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            db.Pitches.Add(new PitchModel
            {
                Name = $"ExistingStadium_{Guid.NewGuid():N}",
                Location = CreatePoint(0.30, 10),
                CreatorUserId = _userId,
                Type = PitchType.Stadium,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var scope = _factory.Services.CreateScope();
        var pitchDiscovery = scope.ServiceProvider.GetRequiredService<IPitchDiscoveryService>();

        var pitches = await pitchDiscovery.EnsureNearbyPitchesAsync(
            _userId,
            "PitchFounder",
            0,
            10);

        var standard = Assert.Single(pitches, p => p.Type == PitchType.Standard);
        Assert.Equal("PitchFounder's Standard Pitch", standard.Name);
    }

    /// <summary>
    /// Verifies ensure Nearby Pitches No Pitch Within Two Miles Creates Practice Pitch.
    /// </summary>
    [Fact]
    public async Task EnsureNearbyPitches_NoPitchWithinTwoMiles_CreatesPracticePitch()
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            db.Pitches.Add(new PitchModel
            {
                Name = $"ExistingStadium_{Guid.NewGuid():N}",
                Location = CreatePoint(20.20, 20),
                CreatorUserId = _userId,
                Type = PitchType.Stadium,
                CreatedAt = DateTime.UtcNow
            });
            db.Pitches.Add(new PitchModel
            {
                Name = $"ExistingStandard_{Guid.NewGuid():N}",
                Location = CreatePoint(20.05, 20),
                CreatorUserId = _userId,
                Type = PitchType.Standard,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var scope = _factory.Services.CreateScope();
        var pitchDiscovery = scope.ServiceProvider.GetRequiredService<IPitchDiscoveryService>();

        var pitches = await pitchDiscovery.EnsureNearbyPitchesAsync(
            _userId,
            "PitchFounder",
            20,
            20);

        var practice = Assert.Single(pitches, p => p.Type == PitchType.Training);
        Assert.Equal("PitchFounder's Practice Pitch", practice.Name);
    }

    private static Point CreatePoint(double latitude, double longitude)
        => new(longitude, latitude) { SRID = 4326 };
}


