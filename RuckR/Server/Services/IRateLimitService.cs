namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side interface IRateLimitService.</summary>
    public interface IRateLimitService
    {
        /// <summary>Determines whether an action is currently allowed under rate-limiting constraints.</summary>
        /// <param name="userId">User identifier used for scoping limits.</param>
        /// <param name="action">Action key being rate-limited.</param>
        /// <param name="maxCount">Maximum allowed actions in the time window.</param>
        /// <param name="window">Rolling time window to evaluate.</param>
        /// <returns><see langword="true"/> when the action is allowed; otherwise <see langword="false"/>.</returns>
        Task<bool> IsAllowedAsync(string userId, string action, int maxCount, TimeSpan window);
    }
}
