using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public interface IMapService : IAsyncDisposable
{
    Task InitMapAsync(string containerId, double lat, double lng, int zoom = 15);
    Task AddPitchMarkersAsync(IEnumerable<PitchModel> pitches, GeoPosition? userPosition = null);
    Task AddUserMarkerAsync(double lat, double lng);
    Task CenterOnAsync(double lat, double lng);
    Task ClearMarkersAsync();
}
