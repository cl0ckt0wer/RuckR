namespace RuckR.Client.Store.LocationFeature;

/// <summary>
/// Updates user position in state.
/// </summary>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
/// <param name="Accuracy">Accuracy in meters.</param>
public record UpdatePositionAction(double Latitude, double Longitude, double Accuracy);

/// <summary>
/// Indicates geolocation watch status change.
/// </summary>
/// <param name="IsWatching">Whether geolocation watch is running.</param>
public record SetGpsWatchingAction(bool IsWatching);

/// <summary>
/// Marks the start of a browser geolocation search attempt.
/// </summary>
/// <param name="StartedAtUtc">UTC time when the search attempt started.</param>
public record StartLocationSearchAction(DateTime StartedAtUtc);

/// <summary>
/// Carries a location error message.
/// </summary>
/// <param name="ErrorMessage">Error text.</param>
/// <param name="ErrorCode">Browser geolocation error code, when available.</param>
/// <param name="PermissionStatus">Permission state implied by the error.</param>
/// <param name="ErrorAtUtc">UTC time when the error was observed.</param>
public record LocationErrorAction(
    string ErrorMessage,
    int? ErrorCode = null,
    GeolocationPermissionStatus PermissionStatus = GeolocationPermissionStatus.Unknown,
    DateTime? ErrorAtUtc = null);

/// <summary>
/// Updates the browser location permission state.
/// </summary>
/// <param name="PermissionStatus">Current browser permission state.</param>
public record SetLocationPermissionAction(GeolocationPermissionStatus PermissionStatus);

/// <summary>
/// Expires a provisional geolocation search after its grace window.
/// </summary>
/// <param name="ExpiredAtUtc">UTC time when the grace window expired.</param>
public record LocationSearchExpiredAction(DateTime ExpiredAtUtc);
