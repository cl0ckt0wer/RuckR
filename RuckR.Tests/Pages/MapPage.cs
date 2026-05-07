using Microsoft.Playwright;

namespace RuckR.Tests.Pages;

/// <summary>
/// Page Object for the /map page — the full-screen Leaflet.js interactive map
/// with pitch markers, user location marker, onboarding banner, GPS-disabled
/// state, error state, and pitch detail overlay.
/// </summary>
public class MapPage : BasePage
{
    public MapPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    /// <summary>Navigate to the map page and wait for Blazor + map container.</summary>
    public async Task GoToAsync() => await NavigateToAsync("/map");

    // ── Map loading ────────────────────────────────────────────────────

    /// <summary>
    /// Wait for Leaflet tiles to finish loading. Returns true if tiles appear
    /// within the timeout; false otherwise.
    /// </summary>
    public async Task<bool> WaitForMapLoadedAsync(int timeoutMs = 15_000)
    {
        try
        {
            await Page.WaitForSelectorAsync(".leaflet-tile-loaded", new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Attached
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Check whether the Leaflet map container exists in the DOM.</summary>
    public async Task<bool> IsMapRenderedAsync() => await ExistsAsync(".leaflet-container");

    /// <summary>Check whether the loading spinner is currently visible.</summary>
    public async Task<bool> IsLoadingSpinnerVisibleAsync() => await ExistsAsync(".spinner-border");

    /// <summary>Check whether the error state alert is visible.</summary>
    public async Task<bool> IsErrorStateVisibleAsync() => await ExistsAsync(".alert-danger");

    // ── GPS disabled banner ────────────────────────────────────────────

    /// <summary>
    /// Check whether the "Enable GPS" banner is visible (shown when the
    /// browser denies or blocks geolocation permission).
    /// </summary>
    public async Task<bool> IsGpsDisabledBannerVisibleAsync()
    {
        return await Page.GetByText("Enable GPS").IsVisibleAsync();
    }

    /// <summary>Click the "Enable" button in the GPS disabled banner.</summary>
    public async Task ClickEnableGpsAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable" }).ClickAsync();
    }

    // ── Onboarding banner ──────────────────────────────────────────────

    /// <summary>
    /// Check whether the first-visit onboarding banner is visible.
    /// </summary>
    public async Task<bool> IsOnboardingBannerVisibleAsync()
    {
        return await Page.GetByText("Welcome to RuckR").IsVisibleAsync();
    }

    /// <summary>
    /// Dismiss the onboarding banner by clicking its close button and
    /// waiting for the banner to disappear.
    /// </summary>
    public async Task DismissOnboardingAsync()
    {
        // Scope the close button to the alert that contains the welcome text
        var closeBtn = Page.Locator(".alert:has-text('Welcome to RuckR') .btn-close");
        if (await closeBtn.IsVisibleAsync())
        {
            await closeBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    // ── Error retry ────────────────────────────────────────────────────

    /// <summary>Click the "Retry" button shown in the error state.</summary>
    public async Task ClickRetryAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Retry" }).ClickAsync();
    }

    // ── Pitch markers ─────────────────────────────────────────────────

    /// <summary>Return the total number of pitch marker icons currently on the map.</summary>
    public async Task<int> GetPitchMarkerCountAsync()
    {
        var markers = await Page.Locator(".leaflet-marker-icon").AllAsync();
        return markers.Count;
    }

    /// <summary>
    /// Click the first visible pitch marker on the map, then wait for the
    /// pitch detail overlay to appear.
    /// </summary>
    public async Task ClickFirstPitchMarkerAsync()
    {
        var marker = Page.Locator(".leaflet-marker-icon").First;
        if (await marker.IsVisibleAsync())
        {
            await marker.ClickAsync();
            // Wait for the overlay to slide up
            await Page.WaitForSelectorAsync(".map-overlay", new PageWaitForSelectorOptions
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
        var markers = Page.Locator(".leaflet-marker-icon");
        var count = await markers.CountAsync();
        if (index >= 0 && index < count)
        {
            await markers.Nth(index).ClickAsync();
            await Page.WaitForSelectorAsync(".map-overlay", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            });
        }
    }

    // ── Pitch detail overlay ──────────────────────────────────────────

    /// <summary>Check whether the pitch detail overlay (bottom sheet) is visible.</summary>
    public async Task<bool> IsPitchOverlayVisibleAsync() => await ExistsAsync(".map-overlay");

    /// <summary>Return the selected pitch name from the overlay heading.</summary>
    public async Task<string?> GetPitchOverlayNameAsync()
    {
        var heading = await Page.QuerySelectorAsync(".map-overlay h5");
        return heading is not null ? await heading.TextContentAsync() : null;
    }

    /// <summary>Close the pitch detail overlay by clicking its close button.</summary>
    public async Task ClosePitchOverlayAsync()
    {
        var closeBtn = Page.Locator(".map-overlay .btn-close");
        if (await closeBtn.IsVisibleAsync())
        {
            await closeBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }
    }

    // ── User location marker ───────────────────────────────────────────

    /// <summary>
    /// Check whether the user's location pulse marker is visible on the map.
    /// </summary>
    public async Task<bool> IsUserMarkerVisibleAsync() => await ExistsAsync(".user-location-pulse");

    // ── Map tile / center helpers ──────────────────────────────────────

    /// <summary>
    /// Verify the map has loaded tiles (Leaflet rendered the tile layer).
    /// </summary>
    public async Task<bool> IsCenteredOnLocationAsync()
    {
        var tiles = await Page.Locator(".leaflet-tile-loaded").AllAsync();
        return tiles.Count > 0;
    }
}
