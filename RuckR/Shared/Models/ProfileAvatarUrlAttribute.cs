using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Validates profile avatar URLs, allowing external HTTP(S) URLs and first-party upload paths.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class ProfileAvatarUrlAttribute : ValidationAttribute
    {
        private const string UploadedProfileImagePrefix = "/uploads/profile-images/";

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileAvatarUrlAttribute"/> class.
        /// </summary>
        public ProfileAvatarUrlAttribute()
            : base("Profile picture URL must be an HTTP(S) URL or an uploaded profile image path.")
        {
        }

        /// <inheritdoc />
        public override bool IsValid(object? value)
        {
            if (value is null)
                return true;

            var text = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (text.StartsWith(UploadedProfileImagePrefix, StringComparison.OrdinalIgnoreCase)
                && !text.Contains('\\', StringComparison.Ordinal)
                && !text.Contains("..", StringComparison.Ordinal)
                && Uri.IsWellFormedUriString(text, UriKind.Relative))
            {
                return true;
            }

            return Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https";
        }
    }
}
