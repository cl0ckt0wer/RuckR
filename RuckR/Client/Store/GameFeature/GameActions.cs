namespace RuckR.Client.Store.GameFeature;

public record SetAuthStateAction(bool IsAuthenticated, string? Username);
public record SetConnectionStateAction(bool IsConnected, string? Error);
