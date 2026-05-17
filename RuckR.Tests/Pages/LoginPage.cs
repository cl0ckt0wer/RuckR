using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class LoginPage : BasePage
{
    // Selectors for ASP.NET Core Identity Login form
    private const string UsernameInput = "input[name='Input.Email']"; // Identity uses Email as username field
    private const string PasswordInput = "input[name='Input.Password']";
    private const string LoginButton = "button[type='submit']";
    private const string RegisterLink = "a[href*='Register']";
    private const string ErrorSummary = ".validation-summary-errors";

    /// <summary>
    /// Initializes a new instance of the <see cref="""LoginPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public LoginPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/Identity/Account/Login");

    /// <summary>
    /// Verifies login Async.
    /// </summary>
    /// <param name="username">The username to use.</param>
    /// <param name="password">The password to use.</param>
    public async Task LoginAsync(string username, string password)
    {
        await Page.FillAsync(UsernameInput, username);
        await Page.FillAsync(PasswordInput, password);
        await Page.ClickAsync(LoginButton);

        // Wait for redirect after successful login (should go to Blazor app)
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
        await DismissErrorUiAsync();
    }

    /// <summary>
    /// Verifies go To Register Async.
    /// </summary>
    public async Task GoToRegisterAsync()
    {
        await Page.ClickAsync(RegisterLink);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Verifies has Error Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<bool> HasErrorAsync()
    {
        return await ExistsAsync(ErrorSummary);
    }

    /// <summary>
    /// Verifies get Error Message Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<string?> GetErrorMessageAsync()
    {
        var element = await Page.QuerySelectorAsync(ErrorSummary);
        return element != null ? await element.TextContentAsync() : null;
    }
}


