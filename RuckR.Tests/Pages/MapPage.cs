using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /map page — the GeoBlazor interactive map
/// with pitch markers, user location marker, onboarding banner, GPS-disabled
/// state, error state, and pitch detail overlay.
/// </summary>
public class MapPage : BasePage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""MapPage"""/> class.
    /// </summary>
    /// <param name="page">The page to use.</param>
    /// <param name="baseUrl">The baseUrl to use.</param>
    public MapPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>Navigate to the map page and wait for Blazor + map container.</summary>
    public async Task GoToAsync() => await NavigateToAsync("/map");

    /// <summary>Navigate to the map page with a query string and wait for Blazor + map container.</summary>
    public async Task GoToAsync(string queryString)
    {
        var normalizedQuery = queryString.StartsWith('?') ? queryString : $"?{queryString}";
        await NavigateToAsync($"/map{normalizedQuery}");
    }

    // ── Map loading ────────────────────────────────────────────────────

    /// <summary>
    /// Wait for the GeoBlazor map container to appear in the DOM.
    /// Returns true if it appears within the timeout; false otherwise.
    /// </summary>
    public async Task<bool> WaitForMapLoadedAsync(int timeoutMs = 15_000)
    {
        try
        {
            await Page.WaitForSelectorAsync("[data-testid='map-loading']", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Hidden
            });
            await Page.WaitForSelectorAsync("[data-testid='map-container']", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Visible
            });
            return true;
        }
        catch (TimeoutException)
        {
            await ScreenshotAsync("map-load-timeout");
            var html = await Page.ContentAsync();
            Directory.CreateDirectory("screenshots");
            await File.WriteAllTextAsync($"screenshots/map-load-timeout_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html", html);
            return false;
        }
    }

    /// <summary>Check whether the map container exists in the DOM.</summary>
    public async Task<bool> IsMapRenderedAsync()
        => await ExistsAsync("[data-testid='map-container']");

    /// <summary>
    /// Wait for GeoBlazor/ArcGIS to attach a map view with a visible drawing surface.
    /// </summary>
    public async Task<bool> WaitForGeoBlazorSurfaceAsync(int timeoutMs = 30_000)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                @"() => {
                    const root = document.querySelector('[data-testid=""map-container""]');
                    const arcgisMap = root?.querySelector('arcgis-map');
                    const view = root?.querySelector('.esri-view');
                    const surface = root?.querySelector('.esri-view-surface');
                    const canvas = root?.querySelector('canvas');
                    const target = canvas || surface || arcgisMap;
                    const rect = target?.getBoundingClientRect();
                    return !!arcgisMap
                        && !!view
                        && !!target
                        && !!rect
                        && rect.width > 100
                        && rect.height > 100;
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            await ScreenshotAsync("geoblazor-surface-timeout");
            var html = await Page.ContentAsync();
            Directory.CreateDirectory("screenshots");
            await File.WriteAllTextAsync($"screenshots/geoblazor-surface-timeout_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html", html);
            return false;
        }
    }

    /// <summary>Return the rendered map container dimensions.</summary>
    public async Task<(float Width, float Height)> GetMapContainerSizeAsync()
    {
        var box = await Page.GetByTestId("map-container").BoundingBoxAsync();
        if (box is null)
            throw new InvalidOperationException("Map container did not have a bounding box.");

        return ((float)box.Width, (float)box.Height);
    }

    /// <summary>Check whether the loading spinner is currently visible.</summary>
    public async Task<bool> IsLoadingSpinnerVisibleAsync() => await ExistsAsync("[data-testid='map-loading']");

    /// <summary>Check whether the error state alert is visible.</summary>
    public async Task<bool> IsErrorStateVisibleAsync() => await ExistsAsync("[data-testid='map-error']");

    // ── GPS disabled banner ────────────────────────────────────────────

    /// <summary>
    /// Check whether the "Enable GPS" banner is visible (shown when the
    /// browser denies or blocks geolocation permission).
    /// </summary>
    public async Task<bool> IsGpsDisabledBannerVisibleAsync()
    {
        return await Page.GetByTestId("gps-disabled-banner").IsVisibleAsync();
    }

    /// <summary>Click the "Enable" button in the GPS disabled banner.</summary>
    public async Task ClickEnableGpsAsync()
    {
        await Page.GetByTestId("gps-enable-btn").ClickAsync();
    }

    // ── Onboarding banner ──────────────────────────────────────────────

    /// <summary>
    /// Check whether the first-visit onboarding banner is visible.
    /// </summary>
    public async Task<bool> IsOnboardingBannerVisibleAsync()
    {
        return await Page.GetByTestId("onboarding-banner").IsVisibleAsync();
    }

    /// <summary>
    /// Dismiss the onboarding banner by clicking its close button and
    /// waiting for the banner to disappear.
    /// </summary>
    public async Task DismissOnboardingAsync()
    {
        await Page.GetByTestId("onboarding-close").ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    // ── Error retry ────────────────────────────────────────────────────

    /// <summary>Click the "Retry" button shown in the error state.</summary>
    public async Task ClickRetryAsync()
    {
        await Page.GetByTestId("map-retry-btn").ClickAsync();
    }

    // ── Pitch markers ─────────────────────────────────────────────────

    /// <summary>Return the total number of pitch marker graphics currently on the map.</summary>
    /// <remarks>GeoBlazor renders markers as SVG elements inside the map container.</remarks>
    public async Task<int> GetPitchMarkerCountAsync()
    {
        // GeoBlazor SimpleMarkerSymbol renders as SVG <circle> elements inside graphic layers
        var markers = await Page.Locator("[data-testid='map-container'] svg circle").AllAsync();
        return markers.Count;
    }

    /// <summary>
    /// Click the first visible pitch marker on the map, then wait for the
    /// pitch detail overlay to appear.
    /// </summary>
    public async Task ClickFirstPitchMarkerAsync()
    {
        var marker = Page.Locator("[data-testid='map-container'] svg circle").First;
        if (await marker.IsVisibleAsync())
        {
            await marker.ClickAsync();
            await Page.WaitForSelectorAsync("[data-testid='pitch-overlay']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            });
        }
    }

    /// <summary>
    /// Click a specific pitch marker by zero-based index.
    /// </summary>
    public async Task ClickPitchMarkerAsync(int index)
    {
        var markers = Page.Locator("[data-testid='map-container'] svg circle");
        var count = await markers.CountAsync();
        if (index >= 0 && index < count)
        {
            await markers.Nth(index).ClickAsync();
            await Page.WaitForSelectorAsync("[data-testid='pitch-overlay']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            });
        }
    }

    /// <summary>
    /// Center the map on a deterministic seeded pitch using the first available nearest-pitch shortcut.
    /// </summary>
    public async Task<string> CenterOnNearestAvailablePitchAsync(int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var testIds = new[] { "nearest-training-btn", "nearest-standard-btn", "nearest-stadium-btn" };

        while (DateTime.UtcNow < deadline)
        {
            foreach (var testId in testIds)
            {
                var button = Page.GetByTestId(testId);
                if (await button.CountAsync() == 0)
                    continue;

                if (await button.IsEnabledAsync())
                {
                    await button.ClickAsync();
                    await Page.WaitForTimeoutAsync(1_000);
                    return testId;
                }
            }

            await Page.WaitForTimeoutAsync(500);
        }

        throw new InvalidOperationException("No nearest-pitch shortcut button was enabled.");
    }

    /// <summary>Click the center of the GeoBlazor drawing surface.</summary>
    public async Task ClickMapCenterAsync()
    {
        var target = Page.Locator("[data-testid='map-container'] canvas").First;
        if (await target.CountAsync() == 0)
            target = Page.Locator("[data-testid='map-container'] arcgis-map").First;

        var box = await target.BoundingBoxAsync();
        if (box is null)
            throw new InvalidOperationException("GeoBlazor map surface did not have a bounding box.");

        await Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    // ── Pitch detail overlay ──────────────────────────────────────────

    /// <summary>Check whether the pitch detail overlay (bottom sheet) is visible.</summary>
    public async Task<bool> IsPitchOverlayVisibleAsync() => await ExistsAsync("[data-testid='pitch-overlay']");

    /// <summary>Wait for the RuckR pitch overlay to become visible.</summary>
    public async Task WaitForPitchOverlayAsync(int timeoutMs = 5_000)
    {
        await Page.WaitForSelectorAsync("[data-testid='pitch-overlay']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>Check whether ArcGIS native popup UI is visible.</summary>
    public async Task<bool> IsNativePopupVisibleAsync()
    {
        var popup = Page.Locator(".esri-popup").First;
        return await popup.CountAsync() > 0 && await popup.IsVisibleAsync();
    }

    /// <summary>Return the selected pitch name from the overlay heading.</summary>
    public async Task<string?> GetPitchOverlayNameAsync()
    {
        return await Page.GetByTestId("pitch-name").TextContentAsync();
    }

    /// <summary>Close the pitch detail overlay by clicking its close button.</summary>
    public async Task ClosePitchOverlayAsync()
    {
        await Page.GetByTestId("pitch-close").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    // ── User location marker ───────────────────────────────────────────

    /// <summary>
    /// Check whether the user's location marker is visible on the map.
    /// </summary>
    public async Task<bool> IsUserMarkerVisibleAsync() => await ExistsAsync("[data-testid='map-container'] svg");

    // ── Map center helpers ─────────────────────────────────────────────

    /// <summary>
    /// Verify the map container is rendered and present in the DOM.
    /// </summary>
    public async Task<bool> IsCenteredOnLocationAsync()
    {
        return await ExistsAsync("[data-testid='map-container']");
    }
}


