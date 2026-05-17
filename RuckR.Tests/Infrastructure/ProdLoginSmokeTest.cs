using Microsoft.Playwright;
using Xunit;

namespace RuckR.Tests.Infrastructure;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public class ProdLoginSmokeTest
{
    /// <summary>
    /// Verifies seed User Can Login.
    /// </summary>
    [Fact]
    public async Task SeedUser_CanLogin()
    {
        var password = Environment.GetEnvironmentVariable("RUCKR_PROD_SMOKE_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
            return;

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://ruckr.exe.xyz");
        await page.WaitForLoadStateAsync(LoadState.Load);
        await page.WaitForTimeoutAsync(5000);

        var loginVisible = await page.GetByTestId("nav-login").IsVisibleAsync();
        Assert.True(loginVisible, "Login link should be visible on homepage");

        await page.GetByTestId("nav-login").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.Load);

        Assert.Contains("/Login", page.Url, StringComparison.OrdinalIgnoreCase);

        await page.FillAsync("input[name='Input.Email']", "ruckr1@ruckr.game");
        await page.FillAsync("input[name='Input.Password']", password);
        await page.ScreenshotAsync(new() { Path = "/tmp/login-before-submit.png" });
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.Load);
        await page.WaitForTimeoutAsync(2000);
        await page.ScreenshotAsync(new() { Path = "/tmp/login-after-submit.png" });

        var errorSummary = await page.QuerySelectorAsync(".validation-summary-errors");
        if (errorSummary != null)
        {
            var errorText = (await errorSummary.TextContentAsync())?.Trim();
            throw new Xunit.Sdk.XunitException($"Login failed with error: {errorText}. Final URL: {page.Url}");
        }

        var logoutVisible = await page.GetByTestId("nav-logout").IsVisibleAsync();
        Assert.True(logoutVisible, $"Logout link should be visible after login. Final URL: {page.Url}");
    }
}


