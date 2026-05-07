using System.Net;
using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

public class CollectionApiTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _username = null!;

    public CollectionApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _username = $"colltest_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(_username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, _username);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
    }

    /// <summary>
    /// Helper to get a single player and pitch ID from seed data.
    /// </summary>
    private async Task<(int playerId, int pitchId, double pitchLat, double pitchLng)> GetTestPlayerAndPitchAsync()
    {
        int playerId = 0, pitchId = 0;
        double pitchLat = 0, pitchLng = 0;

        await _factory.ExecuteInDbAsync(db =>
        {
            var player = db.Players.First();
            var pitch = db.Pitches.First();
            playerId = player.Id;
            pitchId = pitch.Id;
            pitchLat = pitch.Location.Y;
            pitchLng = pitch.Location.X;
            return Task.CompletedTask;
        });

        return (playerId, pitchId, pitchLat, pitchLng);
    }

    /// <summary>
    /// Captures a player and returns the created collection model.
    /// </summary>
    private async Task<CollectionModel> CaptureTestPlayerAsync(int playerId, int pitchId, double pitchLat, double pitchLng)
    {
        _factory.LocationTracker.SetPosition(_userId, pitchLat, pitchLng);
        var request = new CapturePlayerRequest(playerId, pitchId);
        var response = await _client.PostAsJsonAsync("/collection/capture", request);
        response.EnsureSuccessStatusCode();
        var collection = await response.Content.ReadFromJsonAsync<CollectionModel>();
        return collection!;
    }

    /// <summary>
    /// GET /collection returns the user's collection (authenticated).
    /// </summary>
    [Fact]
    public async Task GetCollection_ReturnsUserCollection()
    {
        // Arrange: capture a player first
        var (playerId, pitchId, pitchLat, pitchLng) = await GetTestPlayerAndPitchAsync();
        var captured = await CaptureTestPlayerAsync(playerId, pitchId, pitchLat, pitchLng);

        // Act
        var response = await _client.GetAsync("/collection");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var collections = await response.Content.ReadFromJsonAsync<List<CollectionModel>>();
        Assert.NotNull(collections);
        Assert.NotEmpty(collections);
        Assert.Contains(collections, c => c.PlayerId == playerId);
    }

    /// <summary>
    /// POST /collection/capture with valid player + pitch + GPS position returns 201 Created.
    /// </summary>
    [Fact]
    public async Task CapturePlayer_WithValidData_Returns201()
    {
        // Arrange
        var (playerId, pitchId, pitchLat, pitchLng) = await GetTestPlayerAndPitchAsync();
        _factory.LocationTracker.SetPosition(_userId, pitchLat, pitchLng);
        var request = new CapturePlayerRequest(playerId, pitchId);

        // Act
        var response = await _client.PostAsJsonAsync("/collection/capture", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var collection = await response.Content.ReadFromJsonAsync<CollectionModel>();
        Assert.NotNull(collection);
        Assert.Equal(_userId, collection.UserId);
        Assert.Equal(playerId, collection.PlayerId);
        Assert.Equal(pitchId, collection.CapturedAtPitchId);
        Assert.False(collection.IsFavorite);
    }

    /// <summary>
    /// POST /collection/capture without GPS position returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task CapturePlayer_WithoutGpsPosition_Returns400()
    {
        // Arrange: get a player and pitch, but do NOT set GPS position
        var (playerId, pitchId, _, _) = await GetTestPlayerAndPitchAsync();
        _factory.LocationTracker.ClearPosition(_userId);

        var request = new CapturePlayerRequest(playerId, pitchId);

        // Act
        var response = await _client.PostAsJsonAsync("/collection/capture", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GPS position required", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POST /collection/capture duplicate player returns 409 Conflict.
    /// </summary>
    [Fact]
    public async Task CapturePlayer_Duplicate_Returns409()
    {
        // Arrange: capture a player once
        var (playerId, pitchId, pitchLat, pitchLng) = await GetTestPlayerAndPitchAsync();
        await CaptureTestPlayerAsync(playerId, pitchId, pitchLat, pitchLng);

        // Second capture of the same player should return 409
        _factory.LocationTracker.SetPosition(_userId, pitchLat, pitchLng); // refresh GPS
        var request = new CapturePlayerRequest(playerId, pitchId);

        // Act
        var response = await _client.PostAsJsonAsync("/collection/capture", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("already collected", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POST /collection/{id}/favorite toggles the IsFavorite flag.
    /// </summary>
    [Fact]
    public async Task ToggleFavorite_TogglesFlag()
    {
        // Arrange: capture a player first
        var (playerId, pitchId, pitchLat, pitchLng) = await GetTestPlayerAndPitchAsync();
        var collection = await CaptureTestPlayerAsync(playerId, pitchId, pitchLat, pitchLng);
        Assert.False(collection.IsFavorite);

        // Act: toggle favorite ON
        var toggleOnResponse = await _client.PostAsync($"/collection/{collection.Id}/favorite", null);
        Assert.Equal(HttpStatusCode.OK, toggleOnResponse.StatusCode);

        // Verify it's now true
        var getResponse = await _client.GetAsync("/collection");
        var collections = await getResponse.Content.ReadFromJsonAsync<List<CollectionModel>>();
        var updated = collections!.First(c => c.Id == collection.Id);
        Assert.True(updated.IsFavorite);

        // Act: toggle favorite OFF
        var toggleOffResponse = await _client.PostAsync($"/collection/{collection.Id}/favorite", null);
        Assert.Equal(HttpStatusCode.OK, toggleOffResponse.StatusCode);

        // Verify it's back to false
        var getResponse2 = await _client.GetAsync("/collection");
        var collections2 = await getResponse2.Content.ReadFromJsonAsync<List<CollectionModel>>();
        var updated2 = collections2!.First(c => c.Id == collection.Id);
        Assert.False(updated2.IsFavorite);
    }
}
