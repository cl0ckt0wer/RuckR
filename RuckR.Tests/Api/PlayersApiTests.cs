using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
public class PlayersApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;

    public PlayersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var username = $"playerapi_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, username);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
    }

    [Fact]
    public async Task GetPlayers_ReturnsPlayers()
    {
        var response = await _client.GetAsync("/api/players");
        Assert.True(response.IsSuccessStatusCode,
            $"GET /players returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        // Verify response is a non-empty JSON array
        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", body);
        Assert.EndsWith("]", body);
        Assert.True(body.Length > 2, "Expected non-empty player array");
    }

    [Fact]
    public async Task GetPlayers_WithPositionFilter_ReturnsFilteredPlayers()
    {
        var response = await _client.GetAsync("/api/players?position=FlyHalf");
        Assert.True(response.IsSuccessStatusCode,
            $"GET /players?position=FlyHalf returned {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", body);
        Assert.True(body.Length > 2, "Expected non-empty filtered player array");
    }

    [Fact]
    public async Task GetPlayers_WithRarityFilter_ReturnsFilteredPlayers()
    {
        var response = await _client.GetAsync("/api/players?rarity=Legendary");
        Assert.True(response.IsSuccessStatusCode,
            $"GET /players?rarity=Legendary returned {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", body);
    }

    [Fact]
    public async Task GetPlayer_ById_ReturnsPlayer()
    {
        // Get a valid player ID from the nearby endpoint (which returns DTOs)
        var nearbyResponse = await _client.GetAsync("/api/players/nearby?lat=51.5074&lng=-0.1278&radius=50000");
        Assert.True(nearbyResponse.IsSuccessStatusCode);
        var nearbyPlayers = await nearbyResponse.Content.ReadFromJsonAsync<List<NearbyPlayerDto>>();
        Assert.NotNull(nearbyPlayers);
        Assert.NotEmpty(nearbyPlayers);

        var targetId = nearbyPlayers[0].PlayerId;
        var response = await _client.GetAsync($"/api/players/{targetId}");
        Assert.True(response.IsSuccessStatusCode,
            $"GET /players/{targetId} returned {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"\"id\":{targetId}", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPlayer_NonexistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/players/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPlayersNearby_WithValidCoordinates_ReturnsSortedByDistance()
    {
        var response = await _client.GetAsync("/api/players/nearby?lat=51.5074&lng=-0.1278&radius=10000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var players = await response.Content.ReadFromJsonAsync<List<NearbyPlayerDto>>();
        Assert.NotNull(players);
        Assert.NotEmpty(players);

        for (int i = 1; i < players.Count; i++)
        {
            Assert.True(players[i - 1].FuzzyDistanceMeters <= players[i].FuzzyDistanceMeters,
                $"Players not sorted by distance at index {i}");
        }
    }

    [Fact]
    public async Task GetPlayersNearby_InvalidLatAbove90_Returns400()
    {
        var response = await _client.GetAsync("/api/players/nearby?lat=999&lng=-0.1278&radius=10000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPlayersNearby_InvalidLatBelowMinus90_Returns400()
    {
        var response = await _client.GetAsync("/api/players/nearby?lat=-999&lng=-0.1278&radius=10000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPlayersNearby_InvalidLngAbove180_Returns400()
    {
        var response = await _client.GetAsync("/api/players/nearby?lat=51.5074&lng=999&radius=10000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
