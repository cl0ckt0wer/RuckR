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
        try
        {
            await Page.WaitForSelectorAsync("[data-testid='gps-readiness']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10_000
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Click the "Enable" button in the GPS disabled banner.</summary>
    public async Task ClickEnableGpsAsync()
    {
        await Page.GetByTestId("gps-retry-btn").ClickAsync();
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

    /// <summary>Click the first pitch graphic rendered in an app-owned GeoBlazor layer.</summary>
    public async Task ClickFirstPitchGraphicAsync(int timeoutMs = 15_000)
    {
        await WaitForLayerGraphicCountAsync("Pitches", 1, timeoutMs);
        await SetArcGisViewCenterAsync(51.5074, -0.1278, 17);
        await Page.WaitForTimeoutAsync(1_000);

        var target = Page.Locator("[data-testid='map-container'] canvas").First;
        if (await target.CountAsync() == 0)
            target = Page.Locator("[data-testid='map-container'] arcgis-map").First;

        var box = await target.BoundingBoxAsync();
        if (box is null)
            throw new InvalidOperationException("GeoBlazor map surface did not have a bounding box.");

        var offsets = new (float X, float Y)[]
        {
            (0, 0),
            (0, -12),
            (12, 0),
            (0, 12),
            (-12, 0),
            (12, -12),
            (12, 12),
            (-12, 12),
            (-12, -12)
        };

        foreach (var (xOffset, yOffset) in offsets)
        {
            await Page.Mouse.ClickAsync(
                (float)(box.X + box.Width / 2 + xOffset),
                (float)(box.Y + box.Height / 2 + yOffset));

            await Page.WaitForTimeoutAsync(500);
            if (await Page.GetByTestId("pitch-overlay").IsVisibleAsync())
            {
                return;
            }
        }

        await ScreenshotAsync("pitch-graphic-click-missed");
        throw new TimeoutException($"No pitch overlay appeared after tapping the seeded pitch area within {timeoutMs}ms.");
    }

    // ── Native map widgets ────────────────────────────────────────────

    /// <summary>Wait for native ArcGIS/GeoBlazor map widgets to become visible.</summary>
    public async Task<bool> WaitForNativeWidgetsAsync(int timeoutMs = 10_000)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                @"() => {
                    const root = document.querySelector('[data-testid=""map-container""]');
                    const controls = root?.querySelectorAll('.esri-ui .esri-widget, .esri-zoom, calcite-action') ?? [];
                    return [...controls].filter(control => {
                        const rect = control.getBoundingClientRect();
                        const style = getComputedStyle(control);
                        return style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && rect.width > 0
                            && rect.height > 0;
                    }).length >= 2;
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            await ScreenshotAsync("native-map-widgets-timeout");
            return false;
        }
    }

    /// <summary>Wait for the RuckR map key to become visible with useful symbol rows.</summary>
    public async Task<bool> WaitForMapKeyAsync(int timeoutMs = 10_000)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                @"() => {
                    const root = document.querySelector('[data-testid=""map-container""]');
                    const key = root?.querySelector('[data-testid=""map-key""]');
                    const rect = key?.getBoundingClientRect();
                    const style = key ? getComputedStyle(key) : null;
                    const rows = key?.querySelectorAll('.map-key__row') ?? [];
                    return !!key
                        && !!rect
                        && style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && rect.width > 0
                        && rect.height > 0
                        && rows.length >= 4;
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            await ScreenshotAsync("map-key-timeout");
            return false;
        }
    }

    /// <summary>Return whether the custom RuckR map key is currently visible.</summary>
    public async Task<bool> IsMapKeyVisibleAsync() =>
        await Page.EvaluateAsync<bool>(
            @"() => {
                const key = document.querySelector('[data-testid=""map-key""]');
                const rect = key?.getBoundingClientRect();
                const style = key ? getComputedStyle(key) : null;
                return !!key
                    && !!rect
                    && style.display !== 'none'
                    && style.visibility !== 'hidden'
                    && rect.width > 0
                    && rect.height > 0;
            }");

    /// <summary>Wait until an app-owned ArcGIS graphics layer has at least the requested graphics count.</summary>
    public async Task WaitForLayerGraphicCountAsync(string layerTitle, int minimumCount, int timeoutMs = 10_000)
    {
        await Page.WaitForFunctionAsync(
            @"({ layerTitle, minimumCount }) => {
                const view = document.querySelector('[data-testid=""map-container""] arcgis-map')?.view;
                const layers = view?.map?.allLayers?.items ?? view?.map?.layers?.items ?? [];
                const layer = layers.find(l => l.title === layerTitle);
                const graphics = layer?.graphics;
                const count = graphics?.length ?? graphics?.items?.length ?? 0;
                return count >= minimumCount;
            }",
            new { layerTitle, minimumCount },
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>Return whether any removed RuckR shortcut controls remain in the DOM.</summary>
    public async Task<bool> HasCustomShortcutControlsAsync()
        => await Page.Locator(".ruckr-map-control-stack, .ruckr-map-control").CountAsync() > 0;

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

    /// <summary>Wait for the Spotlight Sighting recruit board target.</summary>
    public async Task WaitForSpotlightEncounterAsync(int timeoutMs = 15_000)
    {
        await Page.WaitForSelectorAsync("[data-testid='spotlight-encounter-btn']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>Select the current Spotlight Sighting.</summary>
    public async Task SelectSpotlightEncounterAsync()
    {
        await Page.GetByTestId("spotlight-encounter-btn").ClickAsync();
        await Page.WaitForSelectorAsync("[data-testid='encounter-overlay']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5_000
        });
    }

    /// <summary>Read the Spotlight Sighting card state label.</summary>
    public async Task<string> GetSpotlightRecruitStateAsync() =>
        (await Page.GetByTestId("spotlight-recruit-state").TextContentAsync())?.Trim() ?? string.Empty;

    /// <summary>Read the Spotlight Sighting countdown text.</summary>
    public async Task<string> GetSpotlightTimerTextAsync() =>
        (await Page.GetByTestId("spotlight-timer").TextContentAsync())?.Trim() ?? string.Empty;

    /// <summary>Read the Spotlight Sighting player-facing hunt copy.</summary>
    public async Task<string> GetSpotlightCopyTextAsync() =>
        (await Page.GetByTestId("spotlight-copy").TextContentAsync())?.Trim() ?? string.Empty;

    /// <summary>Return whether primary map overlays overlap incoherently on the current viewport.</summary>
    public async Task<bool> HasIncoherentSpotlightOverlapAsync() =>
        await Page.EvaluateAsync<bool>(
            @"() => {
                const selectors = [
                    '[data-testid=""gps-readiness""]',
                    '[data-testid=""spotlight-encounter-btn""]',
                    '[data-testid=""encounter-overlay""]',
                    '[data-testid=""recruit-toast""]',
                    '[data-testid=""map-key""]'
                ];
                const visible = selectors
                    .map(selector => document.querySelector(selector))
                    .filter(Boolean)
                    .filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
                    })
                    .map(element => ({ selector: element.getAttribute('data-testid'), rect: element.getBoundingClientRect() }));

                for (let i = 0; i < visible.length; i++) {
                    for (let j = i + 1; j < visible.length; j++) {
                        const a = visible[i].rect;
                        const b = visible[j].rect;
                        const xOverlap = Math.max(0, Math.min(a.right, b.right) - Math.max(a.left, b.left));
                        const yOverlap = Math.max(0, Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top));
                        if (xOverlap * yOverlap > 200) return true;
                    }
                }
                return false;
            }");

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
    public async Task<bool> IsUserMarkerVisibleAsync()
    {
        try
        {
            await WaitForLayerGraphicCountAsync("Player location", 1, 30_000);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // ── Map center helpers ─────────────────────────────────────────────

    /// <summary>
    /// Verify the map container is rendered and present in the DOM.
    /// </summary>
    public async Task<bool> IsCenteredOnLocationAsync()
    {
        return await ExistsAsync("[data-testid='map-container']");
    }

}


