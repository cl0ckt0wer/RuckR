using Microsoft.Playwright;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Services;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;
using RuckR.Shared.Models;

namespace RuckR.Tests.E2E;

    /// <summary>
    /// Provides access to i Class Fixture<Playwright Fixture>,.
    /// </summary>
[Collection(nameof(TestCollection))]
public class MapTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""MapTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public MapTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync(grantGeolocation: true, latitude: 51.5074, longitude: -0.1278);
        _page = await _context.NewPageAsync();
        _baseUrl = _factory.ServerBaseUrl;
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    /// <summary>
    /// Verifies map Page Loads Shows Geo Blazor Map.
    /// </summary>
    [Fact]
    public async Task MapPage_Loads_ShowsGeoBlazorMap()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();

        // Verify map container exists in DOM
        var isRendered = await mapPage.IsMapRenderedAsync();
        Assert.True(isRendered, "Map container should be rendered in the DOM");

        // Verify map shell loaded
        var mapLoaded = await mapPage.WaitForMapLoadedAsync();
        Assert.True(mapLoaded, "Map shell should load within timeout");
    }

    /// <summary>
    /// Verifies map Page Shows Onboarding Banner Can Dismiss.
    /// </summary>
    [Fact]
    public async Task MapPage_ShowsOnboardingBanner_CanDismiss()
    {
        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync();
        await mapPage.WaitForMapLoadedAsync();

        // Onboarding should appear on first visit
        var hasOnboarding = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.True(hasOnboarding, "Onboarding banner should appear on first visit");

        // Dismiss it
        await mapPage.DismissOnboardingAsync();

        // Verify banner is hidden after dismiss
        var stillVisible = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.False(stillVisible, "Onboarding banner should be hidden after dismiss");

        // Refresh and verify onboarding does NOT reappear (localStorage flag persisted)
        await _page.ReloadAsync();
        await mapPage.WaitForMapLoadedAsync();
        var hasOnboardingAfterReload = await mapPage.IsOnboardingBannerVisibleAsync();
        Assert.False(hasOnboardingAfterReload, "Onboarding banner should not reappear after reload");
    }

    /// <summary>
    /// Verifies browser geolocation denial shows an actionable recovery state.
    /// </summary>
    [Fact]
    public async Task MapPage_WhenGeoPermissionDenied_ShowsRecoveryMessage()
    {
        var context = await _playwright.NewContextAsync();
        try
        {
            await context.AddInitScriptAsync(
                @"Object.defineProperty(navigator, 'permissions', {
                    configurable: true,
                    value: {
                        query: async () => ({ state: 'denied' })
                    }
                });
                Object.defineProperty(navigator, 'geolocation', {
                    configurable: true,
                    value: {
                        getCurrentPosition: (success, error) => error({ code: 1, message: 'User denied Geolocation' }),
                        watchPosition: (success, error) => {
                            setTimeout(() => error({ code: 1, message: 'User denied Geolocation' }), 0);
                            return 7;
                        },
                        clearWatch: () => {}
                    }
                });");

            var page = await context.NewPageAsync();
            var mapPage = new MapPage(page, _baseUrl);
            await mapPage.GoToAsync("?basemap=empty&mapDiagnostics=false");
            Assert.True(await mapPage.WaitForMapLoadedAsync());

            await page.GetByTestId("gps-readiness").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10_000
            });

            Assert.Contains("Location permission is off", await page.GetByTestId("gps-readiness").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.True(await page.GetByTestId("gps-permission-help").IsVisibleAsync());
            Assert.Contains("Check again", await page.GetByTestId("gps-retry-btn").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.True(await page.GetByTestId("gps-dismiss-btn").IsVisibleAsync());
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies an authenticated map session surfaces a deterministic nearby rare sighting.
    /// </summary>
    [Fact]
    public async Task MapPage_AuthenticatedUser_ShowsSpotlightSightingWithoutMobileOverlap()
    {
        await _page.SetViewportSizeAsync(390, 844);
        var username = $"spotlight_{Guid.NewGuid():N}@test.com";
        var password = "TestPass123!";
        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        _factory.ParkService.UseParks(new RealWorldPark("spotlight-park", "Spotlight Park", 51.5074, -0.1278, 0));
        await SeedRareNearbyEncounterAsync(username);

        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync("?basemap=empty&arcGisWidgets=true&mapDiagnostics=false");
        await mapPage.WaitForMapLoadedAsync();
        await mapPage.WaitForSpotlightEncounterAsync();
        Assert.False(await mapPage.IsMapKeyVisibleAsync(), "Map key should hide while the mobile recruit board is visible.");
        Assert.False(await mapPage.HasIncoherentSpotlightOverlapAsync());

        await mapPage.SelectSpotlightEncounterAsync();
        Assert.Equal("Recruit window", await mapPage.GetSpotlightRecruitStateAsync());
        Assert.Contains("left", await mapPage.GetSpotlightTimerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recruit before the bell", await mapPage.GetSpotlightCopyTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.False(await mapPage.HasIncoherentSpotlightOverlapAsync());
    }

    private async Task SeedRareNearbyEncounterAsync(string username)
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.UserName == username);
            var player = await db.Players.FirstAsync();
            player.Name = "Brass Boot";
            player.Rarity = PlayerRarity.Legendary;
            player.Level = 8;

            db.PlayerEncounters.Add(new PlayerEncounterModel
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PlayerId = player.Id,
                Latitude = 51.5074,
                Longitude = -0.1278,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            await db.SaveChangesAsync();
        });
    }
}


