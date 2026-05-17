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
public record LocationErrorAction(string ErrorMessage);
