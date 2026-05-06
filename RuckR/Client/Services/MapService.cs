using System.Text.Json;
using Fluxor;
using Microsoft.JSInterop;
using RuckR.Client.Store.MapFeature;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class MapService : IMapService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IDispatcher _dispatcher;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MapService>? _dotNetRef;

    public MapService(IJSRuntime jsRuntime, IDispatcher dispatcher)
    {
        _jsRuntime = jsRuntime;
        _dispatcher = dispatcher;
    }

    public async Task InitMapAsync(string containerId, double lat, double lng, int zoom = 15)
    {
        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/leaflet-map.module.js");
        await _module.InvokeVoidAsync("initMap", containerId, lat, lng, zoom);
        _dispatcher.Dispatch(new MapInitializedAction());
    }

    public async Task AddPitchMarkersAsync(IEnumerable<PitchModel> pitches)
    {
        if (_module is null)
            return;

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

    public async Task AddUserMarkerAsync(double lat, double lng)
    {
        if (_module is null)
            return;
        await _module.InvokeVoidAsync("addUserMarker", lat, lng);
    }

    public async Task CenterOnAsync(double lat, double lng)
    {
        if (_module is null)
            return;
        await _module.InvokeVoidAsync("centerOn", lat, lng);
    }

    public async Task ClearMarkersAsync()
    {
        if (_module is null)
            return;
        await _module.InvokeVoidAsync("clearPitchMarkers");
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
            catch (JSDisconnectedException)
            {
                // Blazor circuit disconnected; no-op
            }

            await _module.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }
}
