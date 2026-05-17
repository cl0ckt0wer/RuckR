using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class RegisterPage : BasePage
{
    private const string UsernameInput = "input[name='Input.Email']"; // Identity uses Email
    private const string PasswordInput = "input[name='Input.Password']";
    private const string ConfirmPasswordInput = "input[name='Input.ConfirmPassword']";
    private const string RegisterButton = "button[type='submit']";
    private const string LoginLink = "a[href*='Login']";

    /// <summary>
    /// Initializes a new instance of the <see cref="""RegisterPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public RegisterPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>
    /// Verifies go To Async.
    /// </summary>
    public async Task GoToAsync() => await NavigateToAsync("/Identity/Account/Register");

    /// <summary>
    /// Verifies register Async.
    /// </summary>
    /// <param name="username">The username to use.</param>
    /// <param name="password">The password to use.</param>
    public async Task RegisterAsync(string username, string password)
    {
        await Page.FillAsync(UsernameInput, username);
        await Page.FillAsync(PasswordInput, password);
        await Page.FillAsync(ConfirmPasswordInput, password);
        await Page.ClickAsync(RegisterButton);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
        await DismissErrorUiAsync();
    }
}


