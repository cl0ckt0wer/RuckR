namespace RuckR.Server.Services;

/// <summary>
/// Raised when a profile avatar upload fails validation.
/// </summary>
public sealed class ProfileAvatarValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileAvatarValidationException"/> class.
    /// </summary>
    /// <param name="message">Validation error message safe to return to the caller.</param>
    public ProfileAvatarValidationException(string message) : base(message)
    {
    }
}
