using Microsoft.Playwright;

namespace RuckR.Tests.Fixtures;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    /// <summary>
    /// Provides access to =.
    /// </summary>
    public IPlaywright Playwright { get; private set; } = null!;
    /// <summary>
    /// Provides access to =.
    /// </summary>
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-gpu" }
        });
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Browser != null)
            await Browser.DisposeAsync();
        Playwright?.Dispose();
    }

    /// <summary>
    /// Verifies new Context Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public async Task<IBrowserContext> NewContextAsync(
        bool isMobile = false,
        string? deviceName = null,
        bool grantGeolocation = false,
        double? latitude = null,
        double? longitude = null)
    {
        var options = new BrowserNewContextOptions();

        if (deviceName != null && Playwright.Devices.TryGetValue(deviceName, out var device))
        {
            options.ViewportSize = device.ViewportSize;
            options.UserAgent = device.UserAgent;
            options.IsMobile = device.IsMobile;
            options.HasTouch = device.HasTouch;
        }
        else if (isMobile)
        {
            options.ViewportSize = new ViewportSize { Width = 390, Height = 844 };
            options.IsMobile = true;
            options.HasTouch = true;
        }

        var context = await Browser.NewContextAsync(options);

        if (grantGeolocation)
        {
            await context.GrantPermissionsAsync(new[] { "geolocation" });
            if (latitude.HasValue && longitude.HasValue)
            {
                await context.SetGeolocationAsync(new Geolocation
                {
                    Latitude = (float)latitude.Value,
                    Longitude = (float)longitude.Value
                });
            }
        }

        return context;
    }
}


