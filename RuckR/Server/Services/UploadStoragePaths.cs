using Microsoft.AspNetCore.Hosting;

namespace RuckR.Server.Services;

/// <summary>
/// Resolves stable upload storage paths outside release folders.
/// </summary>
internal static class UploadStoragePaths
{
    public const string ProfileImagesRequestPath = "/uploads/profile-images";
    public const string ProfileImagesDirectoryName = "profile-images";

    public static string ResolveUploadsRoot(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["Uploads:RootPath"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredRoot));
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "uploads"));
    }

    public static string ResolveProfileImagesRoot(IConfiguration configuration, IWebHostEnvironment environment) =>
        Path.Combine(ResolveUploadsRoot(configuration, environment), ProfileImagesDirectoryName);
}
