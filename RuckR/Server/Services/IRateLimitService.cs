namespace RuckR.Server.Services
{
    public interface IRateLimitService
    {
        Task<bool> IsAllowedAsync(string userId, string action, int maxCount, TimeSpan window);
    }
}