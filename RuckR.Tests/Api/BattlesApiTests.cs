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

        await _factory.ExecuteInDbAsync(db =>
        {
            var players = db.Players.OrderBy(p => p.Id).Take(2).ToList();
            _playerIdA = players[0].Id;
            _playerIdB = players[1].Id;
            _pitchId = db.Pitches.OrderBy(p => p.Id).First().Id;
            return Task.CompletedTask;
        });

        await _factory.SeedCollectionAsync(_userIdA, _playerIdA, _pitchId);
        await _factory.SeedCollectionAsync(_userIdB, _playerIdB, _pitchId);
        await _factory.SeedCollectionAsync(_userIdC, _playerIdA, _pitchId);
        await _factory.SeedCollectionAsync(_userIdD, _playerIdA, _pitchId);
    }

    public Task DisposeAsync()
    {
        _clientA.Dispose();
        _clientB.Dispose();
        _clientC.Dispose();
        _clientD.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Challenge_CreatesPendingChallengeWithoutRecruitSelection()
    {
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameB));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var battle = await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(battle);
        Assert.Equal(_userIdA, battle.ChallengerId);
        Assert.Equal(_userIdB, battle.OpponentId);
        Assert.Equal(_usernameA, battle.ChallengerUsername);
        Assert.Equal(_usernameB, battle.OpponentUsername);
        Assert.Null(battle.ChallengerPlayerId);
        Assert.Null(battle.ChallengerPlayer);
        Assert.Null(battle.ChallengerMove);
        Assert.Equal(BattleStatus.Pending, battle.Status);
    }

    [Fact]
    public async Task Challenge_Self_Returns400()
    {
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot challenge yourself", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Challenge_FourthPending_Returns400()
    {
        var r1 = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameB));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var r2 = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameC));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var r3 = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameD));
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);

        var usernameE = $"battle_e_{Guid.NewGuid():N}";
        var userIdE = await _factory.CreateTestUserAsync(usernameE, "TestPass123!");
        await _factory.SeedCollectionAsync(userIdE, _playerIdB, _pitchId);

        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(usernameE));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pending challenges", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accept_TransitionsToAcceptedWithoutResolving()
    {
        var battle = await CreateChallengeAsync();

        var acceptResponse = await _clientB.PostAsJsonAsync($"/api/battles/{battle.Id}/accept", new AcceptChallengeRequest());

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var acceptedBattle = await acceptResponse.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(acceptedBattle);
        Assert.Equal(BattleStatus.Accepted, acceptedBattle.Status);
        Assert.NotNull(acceptedBattle.AcceptedAt);
        Assert.Null(acceptedBattle.Result);
        Assert.Null(acceptedBattle.WinnerId);
        Assert.False(acceptedBattle.ChallengerSubmitted);
        Assert.False(acceptedBattle.OpponentSubmitted);
    }

    [Fact]
    public async Task Decline_TransitionsToDeclined()
    {
        var battle = await CreateChallengeAsync();

        var declineResponse = await _clientB.PostAsync($"/api/battles/{battle.Id}/decline", null);

        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        var history = await GetHistoryAsync(_clientB);
        var declinedBattle = history.FirstOrDefault(b => b.Id == battle.Id);
        Assert.NotNull(declinedBattle);
        Assert.Equal(BattleStatus.Declined, declinedBattle.Status);
    }

    [Fact]
    public async Task GetPending_ReturnsPendingAndAcceptedChallenges()
    {
        var pendingBattle = await CreateChallengeAsync();
        var acceptedBattle = await CreateAcceptedBattleAsync();

        var listA = await GetPendingAsync(_clientA);
        var listB = await GetPendingAsync(_clientB);

        Assert.Contains(listA, b => b.Id == pendingBattle.Id && b.Status == BattleStatus.Pending);
        Assert.Contains(listA, b => b.Id == acceptedBattle.Id && b.Status == BattleStatus.Accepted);
        Assert.Contains(listB, b => b.Id == pendingBattle.Id && b.Status == BattleStatus.Pending);
        Assert.Contains(listB, b => b.Id == acceptedBattle.Id && b.Status == BattleStatus.Accepted);
    }

    [Fact]
    public async Task Selection_FirstSubmissionStaysHiddenFromOpponent()
    {
        var battle = await CreateAcceptedBattleAsync();

        var submitResponse = await _clientA.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdA, BattleMove.Rock));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var challengerView = await submitResponse.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(challengerView);
        Assert.Equal(BattleStatus.Accepted, challengerView.Status);
        Assert.True(challengerView.ChallengerSubmitted);
        Assert.Equal(_playerIdA, challengerView.ChallengerPlayerId);
        Assert.Equal(BattleMove.Rock, challengerView.ChallengerMove);
        Assert.Null(challengerView.Result);

        var opponentView = (await GetPendingAsync(_clientB)).Single(b => b.Id == battle.Id);
        Assert.True(opponentView.ChallengerSubmitted);
        Assert.False(opponentView.OpponentSubmitted);
        Assert.Null(opponentView.ChallengerPlayerId);
        Assert.Null(opponentView.ChallengerPlayer);
        Assert.Null(opponentView.ChallengerMove);
    }

    [Fact]
    public async Task Selection_SecondSubmissionResolvesBattle()
    {
        var battle = await CreateAcceptedBattleAsync();
        await _clientA.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdA, BattleMove.Rock));

        var response = await _clientB.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdB, BattleMove.Scissors));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(completed);
        Assert.Equal(BattleStatus.Completed, completed.Status);
        Assert.NotNull(completed.Result);
        Assert.Contains(completed.WinnerId, new[] { _userIdA, _userIdB });
        Assert.Equal(BattleMove.Rock, completed.ChallengerMove);
        Assert.Equal(BattleMove.Scissors, completed.OpponentMove);
        Assert.NotNull(completed.ChallengerScore);
        Assert.NotNull(completed.OpponentScore);

        var historyA = await GetHistoryAsync(_clientA);
        Assert.Contains(historyA, b => b.Id == battle.Id && b.Status == BattleStatus.Completed);
    }

    [Fact]
    public async Task Selection_CannotSubmitBeforeAccept()
    {
        var battle = await CreateChallengeAsync();

        var response = await _clientA.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdA, BattleMove.Paper));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Selection_CannotSubmitUnownedRecruit()
    {
        var battle = await CreateAcceptedBattleAsync();

        var response = await _clientA.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdB, BattleMove.Spock));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Selection_RetrySameCompletedSelectionIsIdempotent()
    {
        var battle = await CreateAcceptedBattleAsync();
        await _clientA.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdA, BattleMove.Lizard));
        var first = await _clientB.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdB, BattleMove.Spock));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var retry = await _clientB.PostAsJsonAsync(
            $"/api/battles/{battle.Id}/selection",
            new BattleSelectionRequest(_playerIdB, BattleMove.Spock));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryBattle = await retry.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(retryBattle);
        Assert.Equal(BattleStatus.Completed, retryBattle.Status);
    }

    [Fact]
    public async Task Challenge_WithIdempotencyKey_Deduplicates()
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var r1 = await _clientA.PostAsJsonAsync(
            $"/api/battles/challenge?idempotencyKey={idempotencyKey}",
            new ChallengeRequest(_usernameB));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var battle1 = await r1.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(battle1);

        var r2 = await _clientA.PostAsJsonAsync(
            $"/api/battles/challenge?idempotencyKey={idempotencyKey}",
            new ChallengeRequest(_usernameB));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var battle2 = await r2.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(battle2);
        Assert.Equal(battle1.Id, battle2.Id);
    }

    [Fact]
    public async Task Accept_SecondAcceptReturnsBadRequestOrConflict()
    {
        var battle = await CreateChallengeAsync();

        var accept1 = await _clientB.PostAsJsonAsync($"/api/battles/{battle.Id}/accept", new AcceptChallengeRequest());
        Assert.Equal(HttpStatusCode.OK, accept1.StatusCode);

        var accept2 = await _clientB.PostAsJsonAsync($"/api/battles/{battle.Id}/accept", new AcceptChallengeRequest());

        Assert.True(accept2.StatusCode == HttpStatusCode.BadRequest
                    || accept2.StatusCode == HttpStatusCode.Conflict);
    }

    private async Task<BattleSummaryDto> CreateChallengeAsync()
    {
        var response = await _clientA.PostAsJsonAsync("/api/battles/challenge", new ChallengeRequest(_usernameB));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var battle = await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(battle);
        return battle;
    }

    private async Task<BattleSummaryDto> CreateAcceptedBattleAsync()
    {
        var battle = await CreateChallengeAsync();
        var response = await _clientB.PostAsJsonAsync($"/api/battles/{battle.Id}/accept", new AcceptChallengeRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
        Assert.NotNull(accepted);
        return accepted;
    }

    private static async Task<List<BattleSummaryDto>> GetPendingAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/battles/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var battles = await response.Content.ReadFromJsonAsync<List<BattleSummaryDto>>();
        Assert.NotNull(battles);
        return battles;
    }

    private static async Task<List<BattleSummaryDto>> GetHistoryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/battles/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var battles = await response.Content.ReadFromJsonAsync<List<BattleSummaryDto>>();
        Assert.NotNull(battles);
        return battles;
    }
}
