using System.Text.Json;
using Fluxor;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RuckR.Client.Store.MapFeature;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class MapService : IMapService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<MapService> _logger;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MapService>? _dotNetRef;

    public MapService(IJSRuntime jsRuntime, IDispatcher dispatcher, ILogger<MapService> logger)
    {
        _jsRuntime = jsRuntime;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task InitMapAsync(string containerId, double lat, double lng, int zoom = 15)
    {
        _logger.LogDebug("Map init for container {ContainerId}", containerId);
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/leaflet-map.module.js");
        await _module.InvokeVoidAsync("initMap", containerId, lat, lng, zoom);
        _dispatcher.Dispatch(new MapInitializedAction());
        _logger.LogDebug("Map init complete for container {ContainerId}", containerId);
    }

    public async Task AddPitchMarkersAsync(IEnumerable<PitchModel> pitches)
    {
        if (_module is null)
            return;

        try
        {
            var pitchDtos = pitches.Select(p => new
            {
                id = p.Id,
                latitude = p.Location.Y,
                longitude = p.Location.X,
                name = p.Name,
                type = p.Type.ToString()
            }).ToList();

            var markersJson = JsonSerializer.Serialize(pitchDtos);

            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);

            await _module.InvokeVoidAsync("addPitchMarkers", markersJson, _dotNetRef);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to add pitch markers to map");
        }
    }

    public async Task AddUserMarkerAsync(double lat, double lng)
    {
        if (_module is null)
            return;
        try
        {
            await _module.InvokeVoidAsync("addUserMarker", lat, lng);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to add user marker");
        }
    }

    public async Task CenterOnAsync(double lat, double lng)
    {
        if (_module is null)
            return;
        try
        {
            await _module.InvokeVoidAsync("centerOn", lat, lng);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to center map on ({Lat}, {Lng})", lat, lng);
        }
    }

    public async Task ClearMarkersAsync()
    {
        if (_module is null)
            return;
        try
        {
            await _module.InvokeVoidAsync("clearPitchMarkers");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Failed to clear pitch markers");
        }
    }

    [JSInvokable]
    public void OnPitchClicked(int pitchId)
    {
        _dispatcher.Dispatch(new SelectPitchAction(pitchId));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
            }
            catch (JSDisconnectedException ex)
            {
                _logger.LogDebug(ex, "JS disconnected during map disposal");
            }

            await _module.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }
}
