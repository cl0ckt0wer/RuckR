using RuckR.Client.Store.BattleFeature;
using RuckR.Client.Store.GameFeature;
using RuckR.Client.Store.InventoryFeature;
using RuckR.Client.Store.LocationFeature;
using RuckR.Client.Store.MapFeature;
using RuckR.Shared.Models;
using Xunit;

namespace RuckR.Tests.Unit;

public class FluxorReducerTests
{
    [Fact]
    public void Location_UpdatePosition_UpdatesCoordinatesAndClearsError()
    {
        var state = new LocationState { ErrorMessage = "err" };
        var next = LocationReducers.ReduceUpdatePosition(state, new UpdatePositionAction(1.2, 3.4, 5.6));

        Assert.Equal(1.2, next.UserLatitude);
        Assert.Equal(3.4, next.UserLongitude);
        Assert.Equal(5.6, next.AccuracyMeters);
        Assert.Null(next.ErrorMessage);
    }

    [Fact]
    public void Location_Error_SetsErrorAndStopsWatching()
    {
        var state = new LocationState { IsWatching = true };
        var next = LocationReducers.ReduceLocationError(state, new LocationErrorAction("gps error"));

        Assert.False(next.IsWatching);
        Assert.Equal("gps error", next.ErrorMessage);
    }

    [Fact]
    public void Game_SetAuth_UpdatesAuthState()
    {
        var state = new GameState();
        var next = GameReducers.ReduceSetAuthState(state, new SetAuthStateAction(true, "alice"));

        Assert.True(next.IsAuthenticated);
        Assert.Equal("alice", next.Username);
    }

    [Fact]
    public void Game_SetConnection_UpdatesConnectionState()
    {
        var state = new GameState();
        var next = GameReducers.ReduceSetConnectionState(state, new SetConnectionStateAction(true, null));

        Assert.True(next.IsSignalRConnected);
        Assert.Null(next.ConnectionError);
    }

    [Fact]
    public void Game_SetBrowserOnlineState_UpdatesOnlineFlag()
    {
        var state = new GameState { IsBrowserOnline = true };
        var next = GameReducers.ReduceSetBrowserOnlineState(state, new SetBrowserOnlineStateAction(false));

        Assert.False(next.IsBrowserOnline);
    }

    [Fact]
    public void Game_SetConnectionMetrics_UpdatesLatencyAndQueueCount()
    {
        var state = new GameState();
        var next = GameReducers.ReduceSetConnectionMetrics(state, new SetConnectionMetricsAction(125, 3));

        Assert.Equal(125, next.ConnectionLatencyMs);
        Assert.Equal(3, next.PendingActionCount);
    }

    [Fact]
    public void Inventory_FetchResult_SetsPlayersAndLastSynced()
    {
        var state = new InventoryState { IsLoading = true };
        var players = new List<CollectionModel> { new() { Id = 1, UserId = "u", PlayerId = 2 } };

        var next = InventoryReducers.ReduceFetchInventoryResult(state, new FetchInventoryResultAction(players));

        Assert.False(next.IsLoading);
        Assert.Single(next.CollectedPlayers);
        Assert.NotNull(next.LastSynced);
    }

    [Fact]
    public void Inventory_ToggleFavorite_FlipsFlag()
    {
        var state = new InventoryState
        {
            CollectedPlayers = new List<CollectionModel>
            {
                new() { Id = 10, UserId = "u", PlayerId = 2, IsFavorite = false }
            }
        };

        var next = InventoryReducers.ReduceToggleFavorite(state, new ToggleFavoriteAction(10));

        Assert.True(next.CollectedPlayers.First().IsFavorite);
    }

    [Fact]
    public void Map_SetPitches_ReplacesVisiblePitches()
    {
        var state = new MapState();
        var pitches = new List<PitchModel>
        {
            new() { Id = 1, Name = "Pitch 1", CreatorUserId = "u", Type = PitchType.Standard, Location = null! }
        };

        var next = MapReducers.ReduceSetPitches(state, new SetPitchesAction(pitches));

        Assert.Single(next.VisiblePitches);
        Assert.Equal(1, next.VisiblePitches.First().Id);
    }

    [Fact]
    public void Map_ClearSelection_ClearsBothSelections()
    {
        var state = new MapState { SelectedPitchId = 5, SelectedEncounterId = Guid.NewGuid() };
        var next = MapReducers.ReduceClearSelection(state, new ClearSelectionAction());

        Assert.Null(next.SelectedPitchId);
        Assert.Null(next.SelectedEncounterId);
    }

    [Fact]
    public void Map_SelectPitch_ClearsEncounterSelection()
    {
        var state = new MapState { SelectedEncounterId = Guid.NewGuid() };
        var next = MapReducers.ReduceSelectPitch(state, new SelectPitchAction(12));

        Assert.Equal(12, next.SelectedPitchId);
        Assert.Null(next.SelectedEncounterId);
    }

    [Fact]
    public void Map_SelectEncounter_ClearsPitchSelection()
    {
        var encounterId = Guid.NewGuid();
        var state = new MapState { SelectedPitchId = 5 };
        var next = MapReducers.ReduceSelectEncounter(state, new SelectEncounterAction(encounterId));

        Assert.Null(next.SelectedPitchId);
        Assert.Equal(encounterId, next.SelectedEncounterId);
    }

    [Fact]
    public void Map_SetEncounters_ClearsSelectedEncounterWhenItDisappears()
    {
        var selectedEncounterId = Guid.NewGuid();
        var state = new MapState { SelectedEncounterId = selectedEncounterId };
        var encounters = new List<PlayerEncounterDto>
        {
            new(Guid.NewGuid(), 1, "Player", "Wing", "Common", 1, 51.5, -0.1, DateTime.UtcNow.AddMinutes(5), 65)
        };

        var next = MapReducers.ReduceSetEncounters(state, new SetEncountersAction(encounters));

        Assert.Null(next.SelectedEncounterId);
        Assert.Single(next.VisibleEncounters);
    }

    [Fact]
    public void Battle_ChallengeResponded_Completed_MovesToHistory()
    {
        var battle = new BattleModel
        {
            Id = 7,
            ChallengerId = "a",
            OpponentId = "b",
            ChallengerPlayerId = 1,
            OpponentPlayerId = 2,
            Status = BattleStatus.Pending
        };

        var state = new BattleState
        {
            ActiveChallenges = new List<BattleModel> { battle },
            BattleHistory = new List<BattleModel>()
        };

        var next = BattleReducers.ReduceChallengeResponded(state, new ChallengeRespondedAction(7, BattleStatus.Completed));

        Assert.Empty(next.ActiveChallenges);
        Assert.Single(next.BattleHistory);
        Assert.Equal(BattleStatus.Completed, next.BattleHistory.First().Status);
    }

    [Fact]
    public void Battle_Error_SetsErrorAndStopsLoading()
    {
        var state = new BattleState { IsLoading = true };
        var next = BattleReducers.ReduceBattleError(state, new BattleErrorAction("boom"));

        Assert.False(next.IsLoading);
        Assert.Equal("boom", next.ErrorMessage);
    }
}
