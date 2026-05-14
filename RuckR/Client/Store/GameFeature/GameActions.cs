namespace RuckR.Client.Store.GameFeature;

public record SetAuthStateAction(bool IsAuthenticated, string? Username);
public record SetConnectionStateAction(bool IsConnected, string? Error);
public record SetBrowserOnlineStateAction(bool IsOnline);
public record SetConnectionMetricsAction(int? LatencyMs, int PendingActionCount);
