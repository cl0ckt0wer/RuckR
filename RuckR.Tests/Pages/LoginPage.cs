using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class LoginPage : BasePage
{
    // Selectors for ASP.NET Core Identity Login form
    private const string UsernameInput = "input[name='Input.Email']"; // Identity uses Email as username field
    private const string PasswordInput = "input[name='Input.Password']";
    private const string LoginButton = "button[type='submit']";
    private const string RegisterLink = "a[href*='Register']";
    private const string ErrorSummary = ".validation-summary-errors";

    public LoginPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/Identity/Account/Login");

    public async Task LoginAsync(string username, string password)
    {
        await Page.FillAsync(UsernameInput, username);
        await Page.FillAsync(PasswordInput, password);
        await Page.ClickAsync(LoginButton);

        // Wait for redirect after successful login (should go to Blazor app)
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
    }

    public async Task GoToRegisterAsync()
    {
        await Page.ClickAsync(RegisterLink);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> HasErrorAsync()
    {
        return await ExistsAsync(ErrorSummary);
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        var element = await Page.QuerySelectorAsync(ErrorSummary);
        return element != null ? await element.TextContentAsync() : null;
    }
}
