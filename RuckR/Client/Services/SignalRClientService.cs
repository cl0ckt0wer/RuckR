using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using RuckR.Client.Store.BattleFeature;
using RuckR.Client.Store.GameFeature;
using RuckR.Shared.Models;
using System.Diagnostics;

namespace RuckR.Client.Services
{
    /// <summary>
    /// Manages SignalR connection lifecycle and dispatches updates into state and event callbacks.
    /// </summary>
    public class SignalRClientService : IAsyncDisposable
    {
        private const int MaxQueuedActions = 50;
        private static readonly TimeSpan LatencySampleInterval = TimeSpan.FromSeconds(15);
        private const string LocationQueueKey = "location";

        private HubConnection? _hubConnection;
        private readonly IDispatcher _dispatcher;
        private readonly IState<GameState> _gameState;
        private readonly NavigationManager _navigation;
        private readonly ILogger<SignalRClientService> _logger;
        private readonly HashSet<int> _activeBattleGroups = new();
        private readonly List<QueuedHubAction> _outboundQueue = new();
        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private CancellationTokenSource? _latencySamplingCts;

        /// <summary>
        /// Gets a value indicating whether the hub connection is established.
        /// </summary>
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Gets the current SignalR connection state.
        /// </summary>
        public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

        /// <summary>
        /// Raised whenever the SignalR connection state changes.
        /// </summary>
        public event Action<bool, string?>? ConnectionStateChanged;

        /// <summary>
        /// Raised when a challenge notification is received.
        /// </summary>
        public event Action<ChallengeNotification>? ChallengeReceived;

        /// <summary>
        /// Raised when a battle result is resolved.
        /// </summary>
        public event Action<BattleResult>? BattleResolved;

        /// <summary>
        /// Raised when a pitch is discovered by SignalR gameplay events.
        /// </summary>
        public event Action<PitchModel>? PitchDiscovered;

        /// <summary>
        /// Raised when nearby players are refreshed by SignalR.
        /// </summary>
        public event Action<List<NearbyPlayerDto>>? NearbyPlayersUpdated;

        /// <summary>
        /// Initializes a new SignalR client service.
        /// </summary>
        /// <param name="dispatcher">Dispatcher for Fluxor action notifications.</param>
        /// <param name="gameState">Current game state used for send eligibility checks.</param>
        /// <param name="navigation">Navigation manager for building the hub endpoint URI.</param>
        /// <param name="logger">Logger for connection and dispatch diagnostics.</param>
        public SignalRClientService(
            IDispatcher dispatcher,
            IState<GameState> gameState,
            NavigationManager navigation,
            ILogger<SignalRClientService> logger)
        {
            _dispatcher = dispatcher;
            _gameState = gameState;
            _navigation = navigation;
            _logger = logger;
        }

        /// <summary>
        /// Starts the SignalR connection and replays queued outbound actions.
        /// </summary>
        /// <returns>A task representing the asynchronous startup flow.</returns>
        public async Task StartAsync()
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                StartLatencySampling();
                await ReplayQueuedActionsAsync();
                return;
            }

            if (_hubConnection?.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }

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

            _logger.LogInformation("SignalR connection established");
            _dispatcher.Dispatch(new SetConnectionStateAction(true, null));
            ConnectionStateChanged?.Invoke(true, null);

            StartLatencySampling();
            await ReplayQueuedActionsAsync();
        }

        /// <summary>
        /// Stops the SignalR connection and tears down event handlers.
        /// </summary>
        /// <returns>A task representing the asynchronous stop flow.</returns>
        public async Task StopAsync()
        {
            _latencySamplingCts?.Cancel();
            if (_hubConnection is not null)
            {
                _hubConnection.Reconnecting -= HandleReconnecting;
                _hubConnection.Reconnected -= HandleReconnected;
                _hubConnection.Closed -= HandleClosed;
                await _hubConnection.StopAsync();
            }
        }

        /// <summary>
        /// Sends or queues a location update.
        /// </summary>
        /// <param name="lat">Latitude.</param>
        /// <param name="lng">Longitude.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdateLocationAsync(double lat, double lng)
        {
            await SendOrQueueAsync(new QueuedHubAction(
                "UpdateLocation",
                hub => hub.SendAsync("UpdateLocation", lat, lng),
                LocationQueueKey));
        }

        /// <summary>
        /// Sends or queues a new battle challenge request.
        /// </summary>
        /// <param name="opponentUsername">Opponent username.</param>
        /// <param name="playerId">Challenger collection player id.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendChallengeAsync(string opponentUsername, int playerId)
        {
            var idempotencyKey = Guid.NewGuid().ToString("N");
            await SendOrQueueAsync(new QueuedHubAction(
                "SendChallenge",
                hub => hub.SendAsync("SendChallenge", opponentUsername, playerId, idempotencyKey),
                null));
        }

        /// <summary>
        /// Sends or queues a battle acceptance action.
        /// </summary>
        /// <param name="battleId">Battle identifier.</param>
        /// <param name="playerId">Selected player id for acceptance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AcceptChallengeAsync(int battleId, int playerId)
        {
            await SendOrQueueAsync(new QueuedHubAction(
                "AcceptChallenge",
                hub => hub.SendAsync("AcceptChallenge", battleId, playerId),
                null));
        }

        /// <summary>
        /// Sends or queues a battle decline action.
        /// </summary>
        /// <param name="battleId">Battle identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeclineChallengeAsync(int battleId)
        {
            await SendOrQueueAsync(new QueuedHubAction(
                "DeclineChallenge",
                hub => hub.SendAsync("DeclineChallenge", battleId),
                null));
        }

        /// <summary>
        /// Joins a battle room/group and tracks active groups.
        /// </summary>
        /// <param name="battleId">Battle group identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task JoinBattleGroupAsync(int battleId)
        {
            _activeBattleGroups.Add(battleId);
            await SendOrQueueAsync(new QueuedHubAction(
                "JoinBattleGroup",
                hub => hub.SendAsync("JoinBattleGroup", battleId),
                $"battle-group:{battleId}"));
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
            _logger.LogWarning(ex, "SignalR reconnecting");
            _dispatcher.Dispatch(new SetConnectionStateAction(false, "Reconnecting..."));
            ConnectionStateChanged?.Invoke(false, "Reconnecting...");
            return Task.CompletedTask;
        }

        private async Task HandleReconnected(string? connectionId)
        {
            _logger.LogInformation("SignalR reconnected ({ConnectionId})", connectionId);
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

            StartLatencySampling();
            await ReplayQueuedActionsAsync();
        }

        private Task HandleClosed(Exception? ex)
        {
            _logger.LogWarning(ex, "SignalR connection closed");
            _dispatcher.Dispatch(new SetConnectionStateAction(false, "Connection lost"));
            _dispatcher.Dispatch(new SetConnectionMetricsAction(null, _outboundQueue.Count));
            ConnectionStateChanged?.Invoke(false, "Connection lost");
            return Task.CompletedTask;
        }

        private async Task SendOrQueueAsync(QueuedHubAction action)
        {
            if (!CanSend)
            {
                await EnqueueActionAsync(action);
                return;
            }

            try
            {
                await action.SendAsync(_hubConnection!);
                await ReplayQueuedActionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queueing SignalR action {ActionName} after send failure", action.Name);
                await EnqueueActionAsync(action);
            }
        }

        private async Task EnqueueActionAsync(QueuedHubAction action)
        {
            await _queueLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(action.CoalesceKey))
                {
                    var existingIndex = _outboundQueue.FindIndex(q => q.CoalesceKey == action.CoalesceKey);
                    if (existingIndex >= 0)
                    {
                        _outboundQueue[existingIndex] = action;
                    }
                    else
                    {
                        _outboundQueue.Add(action);
                    }
                }
                else
                {
                    _outboundQueue.Add(action);
                }

                while (_outboundQueue.Count > MaxQueuedActions)
                {
                    _outboundQueue.RemoveAt(0);
                }

                DispatchConnectionMetrics();
            }
            finally
            {
                _queueLock.Release();
            }
        }

        private async Task ReplayQueuedActionsAsync()
        {
            if (!CanSend)
                return;

            while (CanSend)
            {
                QueuedHubAction? action;

                await _queueLock.WaitAsync();
                try
                {
                    action = _outboundQueue.Count == 0 ? null : _outboundQueue[0];
                }
                finally
                {
                    _queueLock.Release();
                }

                if (action is null)
                    return;

                try
                {
                    await action.SendAsync(_hubConnection!);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stopping SignalR queue replay after {ActionName} failed", action.Name);
                    return;
                }

                await _queueLock.WaitAsync();
                try
                {
                    if (_outboundQueue.Count > 0 && ReferenceEquals(_outboundQueue[0], action))
                    {
                        _outboundQueue.RemoveAt(0);
                    }
                    else
                    {
                        _outboundQueue.Remove(action);
                    }

                    DispatchConnectionMetrics();
                }
                finally
                {
                    _queueLock.Release();
                }
            }
        }

        private void StartLatencySampling()
        {
            _latencySamplingCts?.Cancel();
            _latencySamplingCts = new CancellationTokenSource();
            _ = SampleLatencyUntilStoppedAsync(_latencySamplingCts.Token);
        }

        private async Task SampleLatencyUntilStoppedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await SampleLatencyAsync();

                using var timer = new PeriodicTimer(LatencySampleInterval);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await SampleLatencyAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task SampleLatencyAsync()
        {
            if (!CanSend)
            {
                _dispatcher.Dispatch(new SetConnectionMetricsAction(null, _outboundQueue.Count));
                return;
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _hubConnection!.InvokeAsync<long>("Ping");
                stopwatch.Stop();

                _dispatcher.Dispatch(new SetConnectionMetricsAction((int)stopwatch.ElapsedMilliseconds, _outboundQueue.Count));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SignalR latency sample failed");
                _dispatcher.Dispatch(new SetConnectionMetricsAction(null, _outboundQueue.Count));
            }
        }

        private void DispatchConnectionMetrics()
        {
            _dispatcher.Dispatch(new SetConnectionMetricsAction(_gameState.Value.ConnectionLatencyMs, _outboundQueue.Count));
        }

        private bool CanSend =>
            _gameState.Value.IsBrowserOnline &&
            _hubConnection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Stops the connection and performs async disposal.
        /// </summary>
        /// <returns>A task representing the asynchronous disposal flow.</returns>
        public async ValueTask DisposeAsync()
        {
            _latencySamplingCts?.Cancel();
            await StopAsync();
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            _latencySamplingCts?.Dispose();
            _queueLock.Dispose();
        }

        private sealed record QueuedHubAction(
            string Name,
            Func<HubConnection, Task> SendAsync,
            string? CoalesceKey);
    }
}
