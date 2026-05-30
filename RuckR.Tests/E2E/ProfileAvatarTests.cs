using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

/// <summary>
/// Browser tests for profile avatar upload.
/// </summary>
[Collection(nameof(TestCollection))]
public class ProfileAvatarTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFElEQVR42mP8z8AARLJgwiAGsgwAL9QCBGZwRIoAAAAASUVORK5CYII=");

    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;
    private string? _avatarSourcePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileAvatarTests"/> class.
    /// </summary>
    /// <param name="factory">The web app factory.</param>
    /// <param name="playwright">The Playwright fixture.</param>
    public ProfileAvatarTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;
        _avatarSourcePath = Path.Combine(Path.GetTempPath(), $"ruckr-avatar-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(_avatarSourcePath, TinyPngBytes);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();

        if (!string.IsNullOrWhiteSpace(_avatarSourcePath) && File.Exists(_avatarSourcePath))
            File.Delete(_avatarSourcePath);
    }

    /// <summary>
    /// Verifies a user can set a profile picture through the browser UI.
    /// </summary>
    [Fact]
    public async Task Profile_SetProfilePicture_UploadsAndDisplaysUploadedAvatar()
    {
        var username = $"avatar_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";
        await _factory.CreateTestUserAsync(username, password);

        var loginPage = new LoginPage(_page, _baseUrl);
        await loginPage.GoToAsync();
        await loginPage.LoginAsync(username, password);

        var profilePage = new ProfilePage(_page, _baseUrl);
        await profilePage.GoToAsync();

        var avatarUrl = await profilePage.UploadAvatarAsync(_avatarSourcePath!);

        Assert.StartsWith("/uploads/profile-images/", avatarUrl);
        Assert.Matches(@"\.(webp|jpg|jpeg|png)$", avatarUrl);

        var avatarResponse = await _page.Context.APIRequest.GetAsync($"{_baseUrl.TrimEnd('/')}{avatarUrl}");
        Assert.True(avatarResponse.Ok, $"Expected uploaded avatar URL to be served, got {avatarResponse.Status}.");
        var contentType = avatarResponse.Headers.TryGetValue("content-type", out var header) ? header : string.Empty;
        Assert.Contains("image/", contentType);

        await _page.WaitForFunctionAsync(
            @"() => {
                const img = document.querySelector('[data-testid=""profile-avatar-preview""]');
                return img && img.complete && img.naturalWidth > 0 && img.naturalHeight > 0;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }
}
