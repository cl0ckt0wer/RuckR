using Fluxor;
using Microsoft.JSInterop;
using RuckR.Client.Store.LocationFeature;
using RuckR.Shared.Models;
using System.Diagnostics.CodeAnalysis;

namespace RuckR.Client.Services;

/// <summary>
/// Provides browser geolocation access via JS interop and dispatches location updates into Fluxor state.
/// </summary>
public class GeolocationService : IGeolocationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<GeolocationService> _logger;
    private IJSObjectReference? _geoModule;
    private DotNetObjectReference<GeolocationService>? _dotNetRef;
    private double? _watchId;
    private DateTime? _lastPositionUpdate;
    private GeoPosition? _lastAcceptedPosition;
    private double _emaLat;
    private double _emaLng;
    private readonly string _buildStamp;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(5);
    private const double MinDisplacementMeters = 20.0;
    private const double MaxAccuracyMeters = 200.0;
    private const double NullIslandThreshold = 0.05;

    /// <summary>
    /// Raised when a filtered/normalized geolocation update is accepted.
    /// </summary>
    public event Action<GeoPosition>? PositionChanged;

    //#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [DynamicDependency(nameof(OnPositionFromJs))]
    [DynamicDependency(nameof(OnErrorFromJs))]
    /// <summary>
    /// Initializes a new <see cref="GeolocationService"/>.
    /// </summary>
    /// <param name="jsRuntime">The JS interop runtime used to load and invoke geolocation scripts.</param>
    /// <param name="dispatcher">The Fluxor dispatcher used to apply location state updates.</param>
    /// <param name="logger">Logger for geolocation diagnostics.</param>
    public GeolocationService(
        IJSRuntime jsRuntime,
        IDispatcher dispatcher,
        ILogger<GeolocationService> logger)
    {
        _jsRuntime = jsRuntime;
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

    private async Task<IJSObjectReference> GetGeoModuleAsync()
    {
        if (_geoModule is null)
        {
            _geoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/geolocation.module.js");
        }
        return _geoModule;
    }

    /// <summary>
    /// Starts a watched geolocation stream in the browser and dispatches updates to state.
    /// </summary>
    public async Task StartWatchAsync()
    {
        var options = new PositionOptions(false, 15000, 5000);

        var module = await GetGeoModuleAsync();
        _dotNetRef = DotNetObjectReference.Create(this);
        _watchId = await module.InvokeAsync<double>(
            "watchPosition", _dotNetRef, options);

        _dispatcher.Dispatch(new SetGpsWatchingAction(true));
    }

    [JSInvokable]
    /// <summary>
    /// Called from JS when a new geolocation result is available.
    /// </summary>
    /// <param name="result">The latest geolocation result from browser APIs.</param>
    public void OnPositionFromJs(GeolocationResult result)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (_lastPositionUpdate.HasValue && (now - _lastPositionUpdate.Value) < ThrottleInterval)
                return;

            _lastPositionUpdate = now;

            var accuracy = result.Coords.Accuracy;
            var geoPos = new GeoPosition
            {
                Latitude = result.Coords.Latitude,
                Longitude = result.Coords.Longitude,
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
        }
        catch (Exception ex)
        {
            _dispatcher.Dispatch(new LocationErrorAction(ex.Message));
        }
    }

    /// <summary>
    /// Gets the latest user position once via browser geolocation.
    /// </summary>
    /// <returns>Geo position or <c>null</c> if unavailable.</returns>
    public async Task<GeoPosition?> GetCurrentPositionAsync()
    {
        _logger.LogWarning("GetCurrentPositionAsync: starting via JS interop");

        var module = await GetGeoModuleAsync();
        try
        {
            var options = new PositionOptions(false, 8000, 300000);
            var pos = await module.InvokeAsync<GeolocationResult>(
                "getCurrentPosition", options);

            _logger.LogWarning(
                "GetCurrentPositionAsync: success lat={Lat}, lng={Lng}",
                pos.Coords.Latitude, pos.Coords.Longitude);

            return new GeoPosition
            {
                Latitude = pos.Coords.Latitude,
                Longitude = pos.Coords.Longitude,
                Accuracy = pos.Coords.Accuracy,
                Timestamp = pos.TimestampAsUtcDateTime
            };
        }
        catch (JSException ex)
        {
            _logger.LogWarning(
                ex,
                "GetCurrentPositionAsync: JS error msg={Msg}",
                ex.Message);
            return null;
        }
    }

    [JSInvokable]
    /// <summary>
    /// Called from JS when the geolocation watch reports an error.
    /// </summary>
    /// <param name="code">The browser error code.</param>
    /// <param name="message">The browser error message.</param>
    public void OnErrorFromJs(int code, string message)
    {
        _dispatcher.Dispatch(new LocationErrorAction($"Geolocation error {code}: {message}"));
    }

    /// <summary>
    /// Stops the active geolocation watch, if any, and clears last-known position cache.
    /// </summary>
    public void StopWatch()
    {
        if (_watchId.HasValue)
        {
            _geoModule?.InvokeVoidAsync("clearWatch", _watchId.Value);
            _watchId = null;
        }

        _lastAcceptedPosition = null;
    }

    /// <summary>
    /// Disposes JS object references used by the service.
    /// </summary>
    /// <returns>Async disposal task.</returns>
    public async ValueTask DisposeAsync()
    {
        StopWatch();

        if (_geoModule is not null)
        {
            try
            {
                await _geoModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }
}

/// <summary>
/// Maps to the options object passed to browser geolocation methods.
/// </summary>
/// <param name="EnableHighAccuracy">Whether high-accuracy mode is requested.</param>
/// <param name="Timeout">Timeout in milliseconds before geolocation fails.</param>
/// <param name="MaximumAge">Maximum cached location age in milliseconds.</param>
public record PositionOptions(
    bool EnableHighAccuracy,
    int Timeout,
    int MaximumAge);

/// <summary>
/// Mirrors the result returned from JS geolocation calls.
/// </summary>
public class GeolocationResult
{
    /// <summary>
    /// Geographic coordinates returned by the browser.
    /// </summary>
    public GeolocationCoords Coords { get; set; } = null!;

    /// <summary>
    /// Unix epoch milliseconds returned by the browser.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets the timestamp converted to UTC <see cref="DateTime"/>.
    /// </summary>
    public DateTime TimestampAsUtcDateTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).UtcDateTime;
}

/// <summary>
/// Geolocation coordinate payload returned by browser APIs.
/// </summary>
public class GeolocationCoords
{
    /// <summary>
    /// Latitude in decimal degrees.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Horizontal accuracy in meters.
    /// </summary>
    public double Accuracy { get; set; }
}
