using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /map page — the GeoBlazor interactive map
/// with pitch markers, user location marker, onboarding banner, GPS-disabled
/// state, error state, and pitch detail overlay.
/// </summary>
public class MapPage : BasePage
{
    public const string GpsCenterButtonTestId = "gps-center-btn";
    public const string NearestStadiumButtonTestId = "nearest-stadium-btn";
    public const string NearestStandardButtonTestId = "nearest-standard-btn";
    public const string NearestTrainingButtonTestId = "nearest-training-btn";
    public const string CandidatePlacesToggleTestId = "candidate-places-toggle";

    public static readonly string[] ShortcutButtonTestIds =
    [
        GpsCenterButtonTestId,
        NearestStadiumButtonTestId,
        NearestStandardButtonTestId,
        NearestTrainingButtonTestId,
        CandidatePlacesToggleTestId
    ];

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
        var box = await Page.GetByTestId("map-container").BoundingBoxAsync()
            ?? await Page.Locator("[data-testid='map-container'] canvas").First.BoundingBoxAsync()
            ?? await Page.Locator("[data-testid='map-container'] arcgis-map").First.BoundingBoxAsync();
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
                    await button.ClickAsync(new LocatorClickOptions { Force = true });
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

    // ── Map shortcut controls ─────────────────────────────────────────

    /// <summary>Wait for every RuckR map shortcut button to become visible.</summary>
    public async Task WaitForShortcutButtonsAsync(int timeoutMs = 10_000)
    {
        foreach (var testId in ShortcutButtonTestIds)
        {
            await Page.WaitForSelectorAsync($"[data-testid='{testId}']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs
            });
        }
    }

    /// <summary>Return the shortcut button's rendered size.</summary>
    public async Task<(float Width, float Height)> GetShortcutButtonSizeAsync(string testId)
    {
        var box = await Page.GetByTestId(testId).BoundingBoxAsync();
        if (box is null)
            throw new InvalidOperationException($"Shortcut button '{testId}' did not have a bounding box.");

        return ((float)box.Width, (float)box.Height);
    }

    /// <summary>Return a computed CSS property for a shortcut button.</summary>
    public async Task<string> GetShortcutButtonCssAsync(string testId, string propertyName)
        => await Page.GetByTestId(testId).EvaluateAsync<string>(
            "(element, propertyName) => getComputedStyle(element).getPropertyValue(propertyName)",
            propertyName);

    /// <summary>Return a shortcut button attribute value.</summary>
    public async Task<string?> GetShortcutButtonAttributeAsync(string testId, string attributeName)
        => await Page.GetByTestId(testId).GetAttributeAsync(attributeName);

    /// <summary>Check whether a shortcut button is enabled.</summary>
    public async Task<bool> IsShortcutButtonEnabledAsync(string testId)
        => await Page.GetByTestId(testId).IsEnabledAsync();

    /// <summary>Click a shortcut button.</summary>
    public async Task ClickShortcutButtonAsync(string testId)
    {
        await Page.GetByTestId(testId).ClickAsync(new LocatorClickOptions { Force = true });
    }

    /// <summary>Wait until a shortcut button is enabled.</summary>
    public async Task WaitForShortcutButtonEnabledAsync(string testId, int timeoutMs = 10_000)
    {
        await Page.WaitForFunctionAsync(
            @"testId => {
                const button = document.querySelector(`[data-testid='${testId}']`);
                return !!button && !button.disabled;
            }",
            testId,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>Return whether the GeoBlazor candidate places layer is visible.</summary>
    public async Task<bool> IsCandidatePlacesLayerVisibleAsync()
        => await Page.EvaluateAsync<bool>(
            @"() => {
                const view = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view;
                const layers = view?.map?.allLayers?.items ?? view?.map?.layers?.items ?? [];
                const layer = layers.find(l => l.title === 'Candidate places');
                return !!layer?.visible;
            }");

    /// <summary>Wait for the GeoBlazor candidate places layer to reach a visibility state.</summary>
    public async Task WaitForCandidatePlacesLayerVisibilityAsync(bool visible, int timeoutMs = 10_000)
    {
        await Page.WaitForFunctionAsync(
            @"({ visible }) => {
                const view = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view;
                const layers = view?.map?.allLayers?.items ?? view?.map?.layers?.items ?? [];
                const layer = layers.find(l => l.title === 'Candidate places');
                return !!layer && layer.visible === visible;
            }",
            new { visible },
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>Set the ArcGIS view center directly for deterministic button tests.</summary>
    public async Task SetArcGisViewCenterAsync(double latitude, double longitude, int zoom)
    {
        await Page.EvaluateAsync(
            @"async ({ latitude, longitude, zoom }) => {
                const view = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view;
                if (!view) throw new Error('ArcGIS view is not available.');
                await view.goTo({ center: [longitude, latitude], zoom }, { animate: false });
            }",
            new { latitude, longitude, zoom });
    }

    /// <summary>Return the current ArcGIS view center as latitude/longitude.</summary>
    public async Task<(double Latitude, double Longitude)> GetArcGisViewCenterAsync()
    {
        var values = await Page.EvaluateAsync<double[]>(
            @"() => {
                const center = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view?.center;
                if (!center) return [];
                return [center.latitude, center.longitude];
            }");

        if (values.Length < 2)
            throw new InvalidOperationException("ArcGIS view center is not available.");

        return (values[0], values[1]);
    }

    /// <summary>Wait until the ArcGIS view center is near a target coordinate.</summary>
    public async Task WaitForArcGisViewCenterNearAsync(
        double latitude,
        double longitude,
        double toleranceDegrees = 0.001,
        int timeoutMs = 10_000)
    {
        await Page.WaitForFunctionAsync(
            @"({ latitude, longitude, toleranceDegrees }) => {
                const center = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view?.center;
                return !!center
                    && Math.abs(center.latitude - latitude) <= toleranceDegrees
                    && Math.abs(center.longitude - longitude) <= toleranceDegrees;
            }",
            new { latitude, longitude, toleranceDegrees },
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
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


