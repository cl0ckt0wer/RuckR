using System.Net;
using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
public class BattlesApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _clientA = null!;
    private HttpClient _clientB = null!;
    private HttpClient _clientC = null!;
    private HttpClient _clientD = null!;
    private string _userIdA = null!;
    private string _userIdB = null!;
    private string _userIdC = null!;
    private string _userIdD = null!;
    private string _usernameA = null!;
    private string _usernameB = null!;
    private string _usernameC = null!;
    private string _usernameD = null!;
    private int _playerIdA;
    private int _playerIdB;
    private int _pitchId;

    public BattlesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Create test users
        _usernameA = $"battle_a_{Guid.NewGuid():N}";
        _usernameB = $"battle_b_{Guid.NewGuid():N}";
        _usernameC = $"battle_c_{Guid.NewGuid():N}";
        _usernameD = $"battle_d_{Guid.NewGuid():N}";

        _userIdA = await _factory.CreateTestUserAsync(_usernameA, "TestPass123!");
        _userIdB = await _factory.CreateTestUserAsync(_usernameB, "TestPass123!");
        _userIdC = await _factory.CreateTestUserAsync(_usernameC, "TestPass123!");
        _userIdD = await _factory.CreateTestUserAsync(_usernameD, "TestPass123!");

        _clientA = _factory.CreateAuthenticatedClient(_userIdA, _usernameA);
        _clientB = _factory.CreateAuthenticatedClient(_userIdB, _usernameB);
        _clientC = _factory.CreateAuthenticatedClient(_userIdC, _usernameC);
        _clientD = _factory.CreateAuthenticatedClient(_userIdD, _usernameD);

        // Get seed data: two distinct players + a pitch
        await _factory.ExecuteInDbAsync(db =>
        {
            var players = db.Players.OrderBy(p => p.Id).Take(2).ToList();
            _playerIdA = players[0].Id;
            _playerIdB = players[1].Id;
            _pitchId = db.Pitches.OrderBy(p => p.Id).First().Id;
            return Task.CompletedTask;
        });

        // Seed collections: User A owns player A, User B owns player B
        await _factory.SeedCollectionAsync(_userIdA, _playerIdA, _pitchId);
        await _factory.SeedCollectionAsync(_userIdB, _playerIdB, _pitchId);
        // Users C and D also need collections for the 4-pending test
        await _factory.SeedCollectionAsync(_userIdC, _playerIdA, _pitchId);
        await _factory.SeedCollectionAsync(_userIdD, _playerIdA, _pitchId);
    }

    public async Task DisposeAsync()
    {
        _clientA?.Dispose();
        _clientB?.Dispose();
        _clientC?.Dispose();
        _clientD?.Dispose();
    }

    /// <summary>
    /// POST /battles/challenge creates a pending challenge (User A challenges User B).
    /// </summary>
    [Fact]
    public async Task Challenge_CreatesPendingChallenge()
    {
        // Arrange
        var request = new ChallengeRequest(_usernameB, _playerIdA);

        // Act
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var battle = await response.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);
        Assert.Equal(_userIdA, battle.ChallengerId);
        Assert.Equal(_userIdB, battle.OpponentId);
        Assert.Equal(_playerIdA, battle.ChallengerPlayerId);
        Assert.Equal(BattleStatus.Pending, battle.Status);
        Assert.True(battle.Id > 0);
    }

    /// <summary>
    /// POST /battles/challenge with self returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Challenge_Self_Returns400()
    {
        // Arrange: try to challenge yourself
        var request = new ChallengeRequest(_usernameA, _playerIdA);

        // Act
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot challenge yourself", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POST /battles/challenge with 4th pending returns 400 (max 3 pending).
    /// </summary>
    [Fact]
    public async Task Challenge_FourthPending_Returns400()
    {
        // Arrange: create 3 pending challenges from User A
        // Use User B, C, D as opponents
        var r1 = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameC, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var r3 = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameD, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);

        // Act: 4th challenge should fail
        // Create a fresh user E as the 4th opponent
        var usernameE = $"battle_e_{Guid.NewGuid():N}";
        await _factory.CreateTestUserAsync(usernameE, "TestPass123!");
        var requestE = new ChallengeRequest(usernameE, _playerIdA);
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", requestE);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pending challenges", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POST /battles/{id}/accept transitions to Accepted.
    /// </summary>
    [Fact]
    public async Task Accept_TransitionsToAccepted()
    {
        // Arrange: User A challenges User B
        var challengeResponse = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var battle = await challengeResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);

        // Act: User B accepts with their player
        var acceptRequest = new AcceptChallengeRequest(_playerIdB);
        var acceptResponse = await _clientB.PostAsJsonAsync($"/api/battles/{battle.Id}/accept", acceptRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var acceptedBattle = await acceptResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(acceptedBattle);
        Assert.Equal(BattleStatus.Accepted, acceptedBattle.Status);
        Assert.Equal(_playerIdB, acceptedBattle.OpponentPlayerId);
    }

    /// <summary>
    /// POST /battles/{id}/decline transitions to Declined.
    /// </summary>
    [Fact]
    public async Task Decline_TransitionsToDeclined()
    {
        // Arrange: User A challenges User B
        var challengeResponse = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var battle = await challengeResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);

        // Act: User B declines
        var declineResponse = await _clientB.PostAsync($"/api/battles/{battle.Id}/decline", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);

        // Verify via history that it's declined
        var historyResponse = await _clientB.GetAsync("/api/battles/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(history);
        var declinedBattle = history!.FirstOrDefault(b => b.Id == battle.Id);
        Assert.NotNull(declinedBattle);
        Assert.Equal(BattleStatus.Declined, declinedBattle.Status);
    }

    /// <summary>
    /// GET /battles/pending returns pending challenges for both users.
    /// User A (challenger) sees outgoing; User B (opponent) sees incoming.
    /// </summary>
    [Fact]
    public async Task GetPending_ReturnsPendingChallenges()
    {
        // Arrange: User A challenges User B
        var challengeResponse = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var battle = await challengeResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);

        // Act & Assert: User A sees outgoing pending challenge
        var pendingA = await _clientA.GetAsync("/api/battles/pending");
        Assert.Equal(HttpStatusCode.OK, pendingA.StatusCode);
        var listA = await pendingA.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(listA);
        Assert.Contains(listA!, b => b.Id == battle.Id && b.ChallengerId == _userIdA);

        // Act & Assert: User B sees incoming pending challenge
        var pendingB = await _clientB.GetAsync("/api/battles/pending");
        Assert.Equal(HttpStatusCode.OK, pendingB.StatusCode);
        var listB = await pendingB.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(listB);
        Assert.Contains(listB!, b => b.Id == battle.Id && b.OpponentId == _userIdB);
    }

    /// <summary>
    /// GET /battles/history returns completed (non-pending) battles.
    /// </summary>
    [Fact]
    public async Task GetHistory_ReturnsCompletedBattles()
    {
        // Arrange: create a challenge, then decline it to move it to history
        var challengeResponse = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var battle = await challengeResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);

        // Decline to move to history
        await _clientB.PostAsync($"/api/battles/{battle.Id}/decline", null);

        // Act: check history for both users
        var historyA = await _clientA.GetAsync("/api/battles/history");
        Assert.Equal(HttpStatusCode.OK, historyA.StatusCode);
        var listA = await historyA.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(listA);
        Assert.Contains(listA!, b => b.Id == battle.Id);

        var historyB = await _clientB.GetAsync("/api/battles/history");
        Assert.Equal(HttpStatusCode.OK, historyB.StatusCode);
        var listB = await historyB.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(listB);
        Assert.Contains(listB!, b => b.Id == battle.Id);
    }

    /// <summary>
    /// Lazy-expiry: challenge older than 24h expires on GET /battles/pending.
    /// </summary>
    [Fact]
    public async Task LazyExpiry_ChallengeOlderThan24h_ExpiresOnPending()
    {
        // Arrange: create a challenge
        var challengeResponse = await _clientA.PostAsJsonAsync("/api/battles/challenge",
            new ChallengeRequest(_usernameB, _playerIdA));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var battle = await challengeResponse.Content.ReadFromJsonAsync<BattleModel>();
        Assert.NotNull(battle);

        // Manually set CreatedAt to 25 hours ago to trigger lazy-expiry
        await _factory.ExecuteInDbAsync(async db =>
        {
            var b = await db.Battles.FindAsync(battle.Id);
            Assert.NotNull(b);
            b!.CreatedAt = DateTime.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
        });

        // Act: GET /battles/pending — should trigger lazy-expiry and exclude this battle
        var pendingA = await _clientA.GetAsync("/api/battles/pending");
        Assert.Equal(HttpStatusCode.OK, pendingA.StatusCode);
        var listA = await pendingA.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(listA);
        Assert.DoesNotContain(listA!, b => b.Id == battle.Id);

        // Verify the battle is now in history with Expired status
        var historyA = await _clientA.GetAsync("/api/battles/history");
        Assert.Equal(HttpStatusCode.OK, historyA.StatusCode);
        var historyList = await historyA.Content.ReadFromJsonAsync<List<BattleModel>>();
        Assert.NotNull(historyList);
        var expiredBattle = historyList!.FirstOrDefault(b => b.Id == battle.Id);
        Assert.NotNull(expiredBattle);
        Assert.Equal(BattleStatus.Expired, expiredBattle.Status);
    }
}
