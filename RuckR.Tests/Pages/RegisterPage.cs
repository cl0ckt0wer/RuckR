using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

public class RegisterPage : BasePage
{
    private const string UsernameInput = "input[name='Input.Email']"; // Identity uses Email
    private const string PasswordInput = "input[name='Input.Password']";
    private const string ConfirmPasswordInput = "input[name='Input.ConfirmPassword']";
    private const string RegisterButton = "button[type='submit']";
    private const string LoginLink = "a[href*='Login']";

    public RegisterPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    public async Task GoToAsync() => await NavigateToAsync("/Identity/Account/Register");

    public async Task RegisterAsync(string username, string password)
    {
        await Page.FillAsync(UsernameInput, username);
        await Page.FillAsync(PasswordInput, password);
        await Page.FillAsync(ConfirmPasswordInput, password);
        await Page.ClickAsync(RegisterButton);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForBlazorAsync();
    }
}
