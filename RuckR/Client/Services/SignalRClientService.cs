using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using RuckR.Client.Store.BattleFeature;
using RuckR.Client.Store.GameFeature;
using RuckR.Shared.Models;

namespace RuckR.Client.Services
{
    public class SignalRClientService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly IDispatcher _dispatcher;
        private readonly NavigationManager _navigation;
        private readonly HashSet<int> _activeBattleGroups = new();

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

        public event Action<bool, string?>? ConnectionStateChanged;
        public event Action<ChallengeNotification>? ChallengeReceived;
        public event Action<BattleResult>? BattleResolved;
        public event Action<PitchModel>? PitchDiscovered;
        public event Action<List<NearbyPlayerDto>>? NearbyPlayersUpdated;

        public SignalRClientService(IDispatcher dispatcher, NavigationManager navigation)
        {
            _dispatcher = dispatcher;
            _navigation = navigation;
        }

        public async Task StartAsync()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navigation.ToAbsoluteUri("/battlehub"))
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            _hubConnection.On<ChallengeNotification>("ReceiveChallenge", HandleReceiveChallenge);
            _hubConnection.On<BattleResult>("BattleResolved", HandleBattleResolved);
            _hubConnection.On<PitchModel>("PitchDiscovered", HandlePitchDiscovered);
            _hubConnection.On<List<NearbyPlayerDto>>("NearbyPlayersUpdated", HandleNearbyPlayersUpdated);

            _hubConnection.Reconnecting += HandleReconnecting;
            _hubConnection.Reconnected += HandleReconnected;
            _hubConnection.Closed += HandleClosed;

            await _hubConnection.StartAsync();

            _dispatcher.Dispatch(new SetConnectionStateAction(true, null));
            ConnectionStateChanged?.Invoke(true, null);
        }

        public async Task StopAsync()
        {
            if (_hubConnection is not null)
            {
                _hubConnection.Reconnecting -= HandleReconnecting;
                _hubConnection.Reconnected -= HandleReconnected;
                _hubConnection.Closed -= HandleClosed;
                await _hubConnection.StopAsync();
            }
        }

        public async Task UpdateLocationAsync(double lat, double lng)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
                await _hubConnection.SendAsync("UpdateLocation", lat, lng);
        }

        public async Task SendChallengeAsync(string opponentUsername, int playerId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
                await _hubConnection.SendAsync("SendChallenge", opponentUsername, playerId);
        }

        public async Task AcceptChallengeAsync(int battleId, int playerId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
                await _hubConnection.SendAsync("AcceptChallenge", battleId, playerId);
        }

        public async Task DeclineChallengeAsync(int battleId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
                await _hubConnection.SendAsync("DeclineChallenge", battleId);
        }

        public async Task JoinBattleGroupAsync(int battleId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("JoinBattleGroup", battleId);
                _activeBattleGroups.Add(battleId);
            }
        }

        private void HandleReceiveChallenge(ChallengeNotification notification)
        {
            var battle = new BattleModel
            {
                Id = notification.ChallengeId,
                ChallengerId = notification.ChallengerUsername,
                OpponentId = string.Empty,
                ChallengerPlayerId = 0,
                OpponentPlayerId = 0,
                Status = BattleStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _dispatcher.Dispatch(new ChallengeReceivedAction(battle));
            ChallengeReceived?.Invoke(notification);
        }

        private void HandleBattleResolved(BattleResult result)
        {
            var battle = new BattleModel
            {
                ChallengerId = result.WinnerUsername,
                OpponentId = result.LoserUsername,
                Status = BattleStatus.Completed,
                WinnerId = result.WinnerUsername,
                CreatedAt = result.CreatedAt
            };

            _dispatcher.Dispatch(new BattleCompletedAction(battle));
            BattleResolved?.Invoke(result);
        }

        private void HandlePitchDiscovered(PitchModel pitch)
        {
            PitchDiscovered?.Invoke(pitch);
        }

        private void HandleNearbyPlayersUpdated(List<NearbyPlayerDto> players)
        {
            NearbyPlayersUpdated?.Invoke(players);
        }

        private Task HandleReconnecting(Exception? ex)
        {
            _dispatcher.Dispatch(new SetConnectionStateAction(false, "Reconnecting..."));
            ConnectionStateChanged?.Invoke(false, "Reconnecting...");
            return Task.CompletedTask;
        }

        private async Task HandleReconnected(string? connectionId)
        {
            _dispatcher.Dispatch(new SetConnectionStateAction(true, null));
            ConnectionStateChanged?.Invoke(true, null);

            foreach (var battleId in _activeBattleGroups.ToArray())
            {
                try
                {
                    if (_hubConnection?.State == HubConnectionState.Connected)
                        await _hubConnection.SendAsync("JoinBattleGroup", battleId);
                }
                catch
                {
                    _activeBattleGroups.Remove(battleId);
                }
            }
        }

        private Task HandleClosed(Exception? ex)
        {
            _dispatcher.Dispatch(new SetConnectionStateAction(false, "Connection lost"));
            ConnectionStateChanged?.Invoke(false, "Connection lost");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
    }
}
