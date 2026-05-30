using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page object for the authenticated profile page.
/// </summary>
public class ProfilePage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilePage"/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The base URL.</param>
    public ProfilePage(IPage page, string baseUrl) : base(page, baseUrl)
    {
    }

    /// <summary>
    /// Opens the profile page.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/profile");

    /// <summary>
    /// Uploads a profile picture through the browser UI.
    /// </summary>
    /// <param name="imagePath">Local image path selected by the browser.</param>
    /// <returns>The saved avatar URL.</returns>
    public async Task<string> UploadAvatarAsync(string imagePath)
    {
        await Page.GetByTestId("profile-edit-btn").ClickAsync();
        await Page.GetByTestId("profile-avatar-file-input").SetInputFilesAsync(imagePath);
        await Page.GetByTestId("profile-avatar-upload-btn").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });

        await Page.GetByTestId("profile-avatar-upload-btn").ClickAsync();
        await Page.GetByTestId("profile-save-message").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });

        return await Page.GetByTestId("profile-avatar-preview").GetAttributeAsync("src") ?? string.Empty;
    }
}
