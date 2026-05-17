using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
    /// <summary>
    /// Provides access to :.
    /// </summary>
public class CollectionTests : IClassFixture<PlaywrightFixture>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="""CollectionTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public CollectionTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
        _baseUrl = factory.ServerBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Navigating to /collection without authentication should redirect
    /// to the ASP.NET Core Identity login page.
    /// </summary>
    [Fact]
    /// <summary>
    /// Verifies unauthenticated Redirects To Login.
    /// </summary>
    public async Task Unauthenticated_RedirectsToLogin()
    {
        // Arrange
        await using var context = await _playwright.NewContextAsync();
        var page = await context.NewPageAsync();

        // Act
        await page.GotoAsync($"{_baseUrl}/collection");

        // Wait for Blazor WASM to initialize. The Collection page's
        // OnInitializedAsync will issue a navigation to the login page.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        try
        {
            await page.WaitForFunctionAsync(
                "() => !document.querySelector('#app .loading-progress')",
                null, new PageWaitForFunctionOptions { Timeout = 30000 });
        }
        catch (TimeoutException)
        {
            // Blazor may have failed to load; proceed with URL check
        }

        // Assert: Collection page's OnInitializedAsync redirects to login
        Assert.Contains("/Identity/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// After registering (and auto-login), a new user visiting /collection
    /// should see the empty state with the "Explore Map" CTA.
    /// </summary>
    [Fact]
    /// <summary>
    /// Verifies authenticated Shows Empty State.
    /// </summary>
    public async Task Authenticated_ShowsEmptyState()
    {
        // Arrange
        await using var context = await _playwright.NewContextAsync();
        var page = await context.NewPageAsync();

        var email = $"test_empty_{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        // Register a new user (Identity auto-logs in after successful registration)
        var registerPage = new RegisterPage(page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(email, password);

        // Act: navigate to Collection page as authenticated user
        var collectionPage = new CollectionPage(page, _baseUrl);
        await collectionPage.GoToAsync();
        await collectionPage.WaitForCollectionLoadedAsync();

        // Assert: empty state is shown for user with no captures
        var isEmpty = await collectionPage.IsEmptyStateVisibleAsync();
        Assert.True(isEmpty, "Newly registered user should see the empty collection state.");

        // Verify the CTA button is present in the empty state
        var exploreBtn = page.GetByText("Explore Map");
        var isExploreVisible = await exploreBtn.IsVisibleAsync();
        Assert.True(isExploreVisible, "Explore Map CTA button should be visible in empty state.");
    }
}


