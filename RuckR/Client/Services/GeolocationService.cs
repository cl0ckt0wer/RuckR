using Fluxor;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RuckR.Client.Store.LocationFeature;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class GeolocationService : IGeolocationService
{
    private readonly Microsoft.JSInterop.IGeolocationService _blazorators;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<GeolocationService> _logger;
    private double? _watchId;
    private TaskCompletionSource<GeoPosition?>? _positionTcs;
    private DateTime? _lastPositionUpdate;
    private GeoPosition? _lastAcceptedPosition;
    private double _emaLat;
    private double _emaLng;
    private readonly string _buildStamp;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(5);
    private const double MinDisplacementMeters = 20.0;
    private const double MaxAccuracyMeters = 200.0;
    private const double NullIslandThreshold = 0.05;

    public event Action<GeoPosition>? PositionChanged;

    public GeolocationService(
        Microsoft.JSInterop.IGeolocationService blazorators,
        IDispatcher dispatcher,
        ILogger<GeolocationService> logger)
    {
        _blazorators = blazorators;
        _dispatcher = dispatcher;
        _logger = logger;

        try
        {
            var attr = typeof(GeolocationService).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .Cast<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildTimestamp");
            _buildStamp = attr?.Value ?? "dev";
        }
        catch
        {
            _buildStamp = "dev";
        }
    }

    public Task<GeoPosition?> GetCurrentPositionAsync()
    {
        _logger.LogWarning("GetCurrentPositionAsync: starting via blazorators");
        _positionTcs?.TrySetCanceled();
        _positionTcs = new TaskCompletionSource<GeoPosition?>();

        var options = new PositionOptions
        {
            EnableHighAccuracy = false,
            Timeout = 8000,
            MaximumAge = 300000
        };

        _blazorators.GetCurrentPosition(
            position =>
            {
                _logger.LogWarning("GetCurrentPositionAsync: blazorators success lat={Lat}, lng={Lng}",
                    position.Coords.Latitude, position.Coords.Longitude);
                _positionTcs.TrySetResult(new GeoPosition
                {
                    Latitude = position.Coords.Latitude,
                    Longitude = position.Coords.Longitude,
                    Accuracy = position.Coords.Accuracy,
                    Timestamp = position.TimestampAsUtcDateTime
                });
            },
            error =>
            {
                _logger.LogWarning("GetCurrentPositionAsync: blazorators error code={Code}, msg={Msg}",
                    error.Code, error.Message);
                _positionTcs.TrySetResult(null);
            },
            options);

        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        timeoutCts.Token.Register(() =>
        {
            if (_positionTcs.TrySetResult(null))
                _logger.LogWarning("GetCurrentPositionAsync: C# timeout after 10s (browser never called back)");
        });

        return _positionTcs.Task;
    }

    public Task StartWatchAsync()
    {
        var options = new PositionOptions
        {
            EnableHighAccuracy = false,
            Timeout = 15000,
            MaximumAge = 5000
        };

        _watchId = _blazorators.WatchPosition(
            position =>
            {
                var now = DateTime.UtcNow;
                if (_lastPositionUpdate.HasValue && (now - _lastPositionUpdate.Value) < ThrottleInterval)
                    return;

                _lastPositionUpdate = now;

                var accuracy = position.Coords.Accuracy;

                var geoPos = new GeoPosition
                {
                    Latitude = position.Coords.Latitude,
                    Longitude = position.Coords.Longitude,
                    Accuracy = accuracy,
                    Timestamp = now
                };

                if (Math.Abs(geoPos.Latitude) < NullIslandThreshold && Math.Abs(geoPos.Longitude) < NullIslandThreshold)
                {
                    _logger.LogWarning(
                        "WatchPosition: discarding null-island position lat={Lat:F6} lng={Lng:F6}",
                        geoPos.Latitude, geoPos.Longitude);
                    return;
                }

                if (_lastAcceptedPosition is null)
                {
                    _logger.LogWarning(
                        "WatchPosition: ACCEPTED (first) — build={Build} lat={Lat:F6} lng={Lng:F6} accuracy={Accuracy:F0}m",
                        _buildStamp, geoPos.Latitude, geoPos.Longitude, accuracy);

                    _lastAcceptedPosition = geoPos;
                    _emaLat = geoPos.Latitude;
                    _emaLng = geoPos.Longitude;

                    _dispatcher.Dispatch(new UpdatePositionAction(geoPos.Latitude, geoPos.Longitude, accuracy));
                    PositionChanged?.Invoke(geoPos);
                    return;
                }

                if (accuracy > MaxAccuracyMeters)
                {
                    _logger.LogDebug(
                        "WatchPosition: discarding position — accuracy {Accuracy:F0}m exceeds threshold {Threshold:F0}m",
                        accuracy, MaxAccuracyMeters);
                    return;
                }

                var displacement = GeoPosition.HaversineDistance(_lastAcceptedPosition, geoPos);
                if (displacement < Math.Max(accuracy, MinDisplacementMeters))
                {
                    _logger.LogDebug(
                        "WatchPosition: discarding position — displacement {Displacement:F1}m < threshold {Threshold:F1}m (accuracy={Accuracy:F0}m)",
                        displacement, Math.Max(accuracy, MinDisplacementMeters), accuracy);
                    return;
                }

                _lastAcceptedPosition = geoPos;

                var alpha = 1.0 / (1.0 + Math.Max(accuracy / 10.0, 1.0));
                _emaLat = alpha * geoPos.Latitude + (1.0 - alpha) * _emaLat;
                _emaLng = alpha * geoPos.Longitude + (1.0 - alpha) * _emaLng;

                var smoothedPos = new GeoPosition
                {
                    Latitude = _emaLat,
                    Longitude = _emaLng,
                    Accuracy = accuracy,
                    Timestamp = now
                };

                _logger.LogWarning(
                    "WatchPosition: ACCEPTED — build={Build} raw=({RawLat:F6},{RawLng:F6}) ema=({EmaLat:F6},{EmaLng:F6}) alpha={Alpha:F2} displacement={Displacement:F1}m",
                    _buildStamp, geoPos.Latitude, geoPos.Longitude,
                    smoothedPos.Latitude, smoothedPos.Longitude,
                    alpha, displacement);

                _dispatcher.Dispatch(new UpdatePositionAction(smoothedPos.Latitude, smoothedPos.Longitude, accuracy));
                PositionChanged?.Invoke(smoothedPos);
            },
            error =>
            {
                _dispatcher.Dispatch(new LocationErrorAction(error.Message));
            },
            options);

        _dispatcher.Dispatch(new SetGpsWatchingAction(true));
        return Task.CompletedTask;
    }

    public void StopWatch()
    {
        if (_watchId.HasValue)
        {
            _blazorators.ClearWatch(_watchId.Value);
            _watchId = null;
        }

        _lastAcceptedPosition = null;
    }

    public ValueTask DisposeAsync()
    {
        StopWatch();
        _positionTcs?.TrySetCanceled();
        return ValueTask.CompletedTask;
    }
}
