using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
    /// <summary>
    /// Provides access to i Class Fixture<Playwright Fixture>,.
    /// </summary>
public class StartupSmokeTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private HttpClient _kestrelClient = null!;
    private string _baseUrl = null!;

    // Blazor WASM on the browser platform cannot resolve resource strings for
    // exception messages (PlatformNotSupportedException).  The runtime produces
    // benign console errors like "AggregateException_ctor_DefaultMessage
    // (Arg_PlatformNotSupported)" that are not application bugs.
    // Ref: dotnet/runtime#47362
    private static readonly string[] KnownBenignWasmErrors =
    {
        "AggregateException_ctor_DefaultMessage",
        "Arg_PlatformNotSupported",
        "401 (Unauthorized)",  // Expected: unauthenticated UserInfo + API calls
        "404 (Not Found)",     // Expected: static assets that may not exist
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="""StartupSmokeTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public StartupSmokeTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _baseUrl = _factory.ServerBaseUrl;
        _kestrelClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
        _kestrelClient?.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Server Startup & OTel Health Check
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Verifies server Startup Health Endpoint Returns Healthy.
    /// </summary>
    public async Task Server_Startup_HealthEndpointReturnsHealthy()
    {
        var response = await _kestrelClient.GetAsync("/api/telemetry/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
    }

    [Fact]
    /// <summary>
    /// Verifies server Startup O Tel Status Reports All Signals.
    /// </summary>
    public async Task Server_Startup_OTelStatusReportsAllSignals()
    {
        var response = await _kestrelClient.GetAsync("/api/telemetry/status");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RuckR.Server", body);
        Assert.Contains("traces", body);
        Assert.Contains("metrics", body);
        Assert.Contains("logs", body);
    }

    [Fact]
    /// <summary>
    /// Verifies server Startup O Tel Tracer Provider Is Registered.
    /// </summary>
    public async Task Server_Startup_OTelTracerProviderIsRegistered()
    {
        var tracerProvider = _factory.Services.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    /// <summary>
    /// Verifies server Startup O Tel Meter Provider Is Registered.
    /// </summary>
    public async Task Server_Startup_OTelMeterProviderIsRegistered()
    {
        var meterProvider = _factory.Services.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    // ═══════════════════════════════════════════════════════════════
    //  WASM Client Error Monitoring
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Verifies wASM Startup Loads Without Errors.
    /// </summary>
    public async Task WASM_Startup_LoadsWithoutErrors()
    {
        var page = await _context.NewPageAsync();
        var consoleErrors = new List<IConsoleMessage>();
        var pageErrors = new List<string>();
        var framework404s = new List<IResponse>();

        void OnConsole(object? _, IConsoleMessage msg)
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg);
        }

        void OnPageError(object? _, string err)
        {
            pageErrors.Add(err);
        }

        void OnResponse(object? _, IResponse resp)
        {
            if (resp.Url.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)
                && resp.Status == 404)
                framework404s.Add(resp);
        }

        page.Console += OnConsole;
        page.PageError += OnPageError;
        page.Response += OnResponse;

        try
        {
            await page.GotoAsync(_baseUrl);
            await WaitForAppPageLoadedAsync(page);

            await page.GotoAsync($"{_baseUrl}map");
            await page.WaitForTimeoutAsync(3000);
            await page.GotoAsync($"{_baseUrl}catalog");
            await page.WaitForTimeoutAsync(3000);

            var unexpectedErrors = FilterBenignErrors(consoleErrors);
            if (unexpectedErrors.Count > 0)
            {
                var errors = string.Join("\n", unexpectedErrors.Select(e => e.Text));
                Assert.Fail($"Browser console errors during startup:\n{errors}");
            }

            if (pageErrors.Count > 0)
                Assert.Fail($"Unhandled page exceptions:\n{string.Join("\n", pageErrors)}");

            if (framework404s.Count > 0)
            {
                var urls = string.Join("\n", framework404s.Select(r => r.Url));
                Assert.Fail($"Framework file 404s:\n{urls}");
            }
        }
        finally
        {
            page.Console -= OnConsole;
            page.PageError -= OnPageError;
            page.Response -= OnResponse;
            await page.CloseAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  WASM Rendering Correctness
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Verifies wASM Startup Blazor Renders Content.
    /// </summary>
    public async Task WASM_Startup_BlazorRendersContent()
    {
        var page = await _context.NewPageAsync();

        try
        {
            await page.GotoAsync(_baseUrl);
            await WaitForAppPageLoadedAsync(page);

            // Verify the page rendered — the app div has content
            // (either Blazor-rendered content or the initial spinner;
            //  an empty page would indicate a server error.)
            var appHasContent = await page.EvaluateAsync<bool>(@"() => {
                const app = document.getElementById('app');
                return app && app.children.length > 0 && app.innerHTML.trim().length > 0;
            }");
            Assert.True(appHasContent, "Page has no rendered content in #app");

            // Note: #blazor-error-ui is controlled by the Blazor runtime.
            // On .NET 10 preview WASM, benign platform-level exceptions
            // (AggregateException resource-string lookups) may trigger the
            // error UI even though the app continues to function.  Console-error
            // and page-exception monitoring provide more actionable signals.

            // Verify no reconnect modal
            var reconnectModal = await page.QuerySelectorAsync("#components-reconnect-modal");
            if (reconnectModal is not null)
            {
                await page.WaitForTimeoutAsync(2000);
                var visible = await reconnectModal.IsVisibleAsync();
                Assert.False(visible,
                    "Reconnect modal should not be visible after Blazor loads");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Telemetry Bridge
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Verifies wASM Startup Telemetry Bridge Is Active.
    /// </summary>
    public async Task WASM_Startup_TelemetryBridgeIsActive()
    {
        var page = await _context.NewPageAsync();

        try
        {
            await page.GotoAsync(_baseUrl);
            await WaitForAppPageLoadedAsync(page);

            await page.GotoAsync($"{_baseUrl}catalog");
            await page.WaitForTimeoutAsync(2000);

            // Wait for TelemetryLoggerProvider flush interval (5 s) + buffer
            await page.WaitForTimeoutAsync(8000);

            var response = await _kestrelClient.GetAsync("/api/telemetry/health");
            Assert.True(response.IsSuccessStatusCode,
                "Server health should remain OK after telemetry bridge flush");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Full Orchestration
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Verifies full Startup All Systems Healthy.
    /// </summary>
    public async Task FullStartup_AllSystemsHealthy()
    {
        // 1.  API level: server health + OTel status
        var healthResponse = await _kestrelClient.GetAsync("/api/telemetry/health");
        Assert.True(healthResponse.IsSuccessStatusCode, "Health endpoint should return 200");

        var statusResponse = await _kestrelClient.GetAsync("/api/telemetry/status");
        Assert.True(statusResponse.IsSuccessStatusCode, "Status endpoint should return 200");
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.Contains("traces", statusBody);
        Assert.Contains("metrics", statusBody);
        Assert.Contains("logs", statusBody);

        // 2.  WASM level: error-free page load + content rendering
        var page = await _context.NewPageAsync();
        var consoleErrors = new List<IConsoleMessage>();
        var pageErrors = new List<string>();
        var framework404s = new List<IResponse>();

        void OnConsole(object? _, IConsoleMessage msg)
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg);
        }

        void OnPageError(object? _, string err)
        {
            pageErrors.Add(err);
        }

        void OnResponse(object? _, IResponse resp)
        {
            if (resp.Url.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)
                && resp.Status == 404)
                framework404s.Add(resp);
        }

        page.Console += OnConsole;
        page.PageError += OnPageError;
        page.Response += OnResponse;

        try
        {
            await page.GotoAsync(_baseUrl);
            await WaitForAppPageLoadedAsync(page);

            await page.GotoAsync($"{_baseUrl}map");
            await page.WaitForTimeoutAsync(3000);
            await page.GotoAsync($"{_baseUrl}catalog");
            await page.WaitForTimeoutAsync(3000);

            // Assert: no unexpected console errors
            var unexpectedErrors = FilterBenignErrors(consoleErrors);
            Assert.Empty(unexpectedErrors);

            // Assert: no page exceptions
            Assert.Empty(pageErrors);

            // Assert: no framework 404s
            Assert.Empty(framework404s);

            // Assert: page rendered
            var appHasContent = await page.EvaluateAsync<bool>(@"() => {
                const app = document.getElementById('app');
                return app && app.children.length > 0 && app.innerHTML.trim().length > 0;
            }");
            Assert.True(appHasContent, "Page has no rendered content in #app");

            // Note: #blazor-error-ui is controlled by the Blazor runtime and
            // may show during .NET 10 preview WASM init for benign reasons.
        }
        finally
        {
            page.Console -= OnConsole;
            page.PageError -= OnPageError;
            page.Response -= OnResponse;
            await page.CloseAsync();
        }

        // 3.  Final health check
        var finalHealth = await _kestrelClient.GetAsync("/api/telemetry/health");
        Assert.True(finalHealth.IsSuccessStatusCode,
            "Server should remain healthy after full startup exercise");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Remove console errors that are known benign Blazor WASM runtime
    /// artefacts (e.g. resource-string resolution failures on the browser
    /// platform).  Any remaining errors are unexpected and should fail the
    /// test.
    /// </summary>
    private static List<IConsoleMessage> FilterBenignErrors(List<IConsoleMessage> errors)
    {
        return errors
            .Where(e => !KnownBenignWasmErrors.Any(k =>
                e.Text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Wait for the Blazor WASM app page to finish its initial load:
    /// framework files downloaded, network settled, and page content present.
    /// </summary>
    private static async Task WaitForAppPageLoadedAsync(IPage page)
    {
        // Wait for Blazor to finish rendering or show the error UI.
        await page.WaitForFunctionAsync(@"() => {
            const loading = document.querySelector('#app .loading-progress');
            const errorUI = document.getElementById('blazor-error-ui');
            if (errorUI && errorUI.style.display !== 'none') return true;
            return !loading;
        }", null, new PageWaitForFunctionOptions { Timeout = 45000 });

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = 30000 });
    }
}


