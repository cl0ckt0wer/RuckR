namespace RuckR.Client.Store.LocationFeature;

public record UpdatePositionAction(double Latitude, double Longitude, double Accuracy);
public record SetGpsWatchingAction(bool IsWatching);
public record LocationErrorAction(string ErrorMessage);
