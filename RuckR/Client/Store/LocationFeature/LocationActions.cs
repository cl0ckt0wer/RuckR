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
/// Carries a location error message.
/// </summary>
/// <param name="ErrorMessage">Error text.</param>
/// <param name="ErrorCode">Browser geolocation error code, when available.</param>
/// <param name="PermissionStatus">Permission state implied by the error.</param>
public record LocationErrorAction(
    string ErrorMessage,
    int? ErrorCode = null,
    GeolocationPermissionStatus PermissionStatus = GeolocationPermissionStatus.Unknown);

/// <summary>
/// Updates the browser location permission state.
/// </summary>
/// <param name="PermissionStatus">Current browser permission state.</param>
public record SetLocationPermissionAction(GeolocationPermissionStatus PermissionStatus);
