namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IRateLimitService.</summary>
    public interface IRateLimitService
    {
        Task<bool> IsAllowedAsync(string userId, string action, int maxCount, TimeSpan window);
    }
}
