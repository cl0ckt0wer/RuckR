using System.Net;
using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
public class UsersApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _playerId;
    private int _pitchId;

    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _factory.LocationTracker.ClearAll();

        var username = $"usersapi_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, username);

        await _factory.ExecuteInDbAsync(db =>
        {
            _playerId = db.Players.OrderBy(p => p.Id).First().Id;
            _pitchId = db.Pitches.OrderBy(p => p.Id).First().Id;
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync()
    {
        _factory.LocationTracker.ClearAll();
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetNearbyUsers_ReturnsOnlyChallengeableRecentUsers()
    {
        var nearbyUsername = $"nearby_{Guid.NewGuid():N}";
        var staleUsername = $"stale_{Guid.NewGuid():N}";
        var emptyUsername = $"empty_{Guid.NewGuid():N}";
        var nearbyUserId = await _factory.CreateTestUserAsync(nearbyUsername, "TestPass123!");
        var staleUserId = await _factory.CreateTestUserAsync(staleUsername, "TestPass123!");
        var emptyUserId = await _factory.CreateTestUserAsync(emptyUsername, "TestPass123!");
        await _factory.SeedCollectionAsync(nearbyUserId, _playerId, _pitchId);
        await _factory.SeedCollectionAsync(staleUserId, _playerId, _pitchId);

        _factory.LocationTracker.SetPosition(_userId, 51.5074, -0.1278);
        _factory.LocationTracker.SetPosition(nearbyUserId, 51.50741, -0.12779);
        _factory.LocationTracker.SetPosition(emptyUserId, 51.50742, -0.12780);
        _factory.LocationTracker.SetPosition(staleUserId, 51.50741, -0.12779, DateTime.UtcNow.AddMinutes(-5));

        var response = await _client.GetAsync("/api/users/nearby?lat=51.5074&lng=-0.1278&radius=1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<NearbyUserDto>>();
        Assert.NotNull(users);
        var user = Assert.Single(users);
        Assert.Equal(nearbyUserId, user.UserId);
        Assert.Equal(nearbyUsername, user.Username);
        Assert.True(user.RecruitCount > 0);
        Assert.Equal(DistanceBucket.Within50m, user.DistanceBucket);
        Assert.DoesNotContain(users, u => u.UserId == _userId);
        Assert.DoesNotContain(users, u => u.UserId == staleUserId);
        Assert.DoesNotContain(users, u => u.UserId == emptyUserId);
    }

    [Fact]
    public async Task GetNearbyUsers_InvalidCoordinates_Returns400()
    {
        var response = await _client.GetAsync("/api/users/nearby?lat=999&lng=-0.1278&radius=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
