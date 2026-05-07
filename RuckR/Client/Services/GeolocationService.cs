using Fluxor;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RuckR.Client.Store.LocationFeature;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class GeolocationService : IGeolocationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<GeolocationService> _logger;
    private IJSObjectReference? _module;
    private DotNetObjectReference<GeolocationService>? _dotNetRef;
    private DateTime? _lastPositionUpdate;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(5);

    public event Action<GeoPosition>? PositionChanged;

    public GeolocationService(IJSRuntime jsRuntime, IDispatcher dispatcher, ILogger<GeolocationService> logger)
    {
        _jsRuntime = jsRuntime;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<GeoPosition?> GetCurrentPositionAsync()
    {
        await EnsureModuleAsync();
        try
        {
            var pos = await _module!.InvokeAsync<IJSObjectReference>("getCurrentPosition");
            var coords = await pos.InvokeAsync<IJSObjectReference>("getProperty", "coords");
            var lat = await coords.InvokeAsync<double>("getProperty", "latitude");
            var lng = await coords.InvokeAsync<double>("getProperty", "longitude");
            var accuracy = await coords.InvokeAsync<double>("getProperty", "accuracy");

            return new GeoPosition
            {
                Latitude = lat,
                Longitude = lng,
                Accuracy = accuracy,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Geolocation getCurrentPosition failed");
            return null;
        }
    }

    public async Task StartWatchAsync()
    {
        await EnsureModuleAsync();
        _dotNetRef?.Dispose();
        _dotNetRef = DotNetObjectReference.Create(this);
        await _module!.InvokeVoidAsync("startWatch", _dotNetRef);
        _dispatcher.Dispatch(new SetGpsWatchingAction(true));
    }

    public void StopWatch()
    {
        if (_module is not null)
        {
            _ = _module.InvokeVoidAsync("stopWatch");
        }
    }

    [JSInvokable]
    public void OnPositionChanged(double latitude, double longitude, double accuracy)
    {
        var now = DateTime.UtcNow;
        if (_lastPositionUpdate.HasValue && (now - _lastPositionUpdate.Value) < ThrottleInterval)
            return;

        _lastPositionUpdate = now;

        var position = new GeoPosition
        {
            Latitude = latitude,
            Longitude = longitude,
            Accuracy = accuracy,
            Timestamp = now
        };

        _dispatcher.Dispatch(new UpdatePositionAction(latitude, longitude, accuracy));
        PositionChanged?.Invoke(position);
    }

    [JSInvokable]
    public void OnPositionError(string errorMessage)
    {
        _dispatcher.Dispatch(new LocationErrorAction(errorMessage));
    }

    private async Task EnsureModuleAsync()
    {
        if (_module is not null)
            return;

        try
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/geolocation.module.js");
        }
        catch (JSException ex)
        {
            _logger.LogError(ex, "Failed to import geolocation JS module");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            StopWatch();
        }
        catch (JSDisconnectedException ex)
        {
            _logger.LogDebug(ex, "JS disconnected during geolocation StopWatch");
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException ex)
            {
                _logger.LogDebug(ex, "JS disconnected during geolocation module disposal");
            }

            _module = null;
        }
    }
}
