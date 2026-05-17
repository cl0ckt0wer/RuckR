using Microsoft.Playwright;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.E2E;

    /// <summary>
    /// Provides access to i Class Fixture<Playwright Fixture>,.
    /// </summary>
[Collection(nameof(TestCollection))]
public class BlazorWasmLoadTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""BlazorWasmLoadTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public BlazorWasmLoadTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
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
    /// Verifies blazor App Initial Load Framework Assets Load Without404.
    /// </summary>
    [Fact]
    public async Task BlazorApp_InitialLoad_FrameworkAssetsLoadWithout404()
    {
        // Collect all 404 responses for _framework/** paths
        var framework404s = new List<IResponse>();

        void OnResponse(object? _, IResponse response)
        {
            if (response.Url.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)
                && response.Status == 404)
            {
                framework404s.Add(response);
            }
        }

        _page.Response += OnResponse;

        try
        {
            // Navigate to the app
            await _page.GotoAsync(_baseUrl);

            // Wait for Blazor WASM to render or show the error UI
            await _page.WaitForFunctionAsync(@"() => {
                const loading = document.querySelector('#app .loading-progress');
                const errorUI = document.getElementById('blazor-error-ui');
                if (errorUI && errorUI.style.display !== 'none') return true;
                return !loading;
            }", null, new PageWaitForFunctionOptions { Timeout = 45000 });

            // Wait for network to settle
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30000 });

            // Assert zero 404 responses for _framework paths
            Assert.Empty(framework404s);
        }
        finally
        {
            _page.Response -= OnResponse;
        }
    }

    /// <summary>
    /// Verifies blazor App Framework Files Are Accessible.
    /// </summary>
    [Fact]
    public async Task BlazorApp_FrameworkFiles_AreAccessible()
    {
        // Navigate first to establish the page context and load all framework files
        var frameworkResponses = new Dictionary<string, int>();

        void OnResponse(object? _, IResponse response)
        {
            if (response.Url.Contains("/_framework/", StringComparison.OrdinalIgnoreCase))
            {
                frameworkResponses[response.Url] = response.Status;
            }
        }

        _page.Response += OnResponse;

        try
        {
            await _page.GotoAsync(_baseUrl);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 45000 });
        }
        finally
        {
            _page.Response -= OnResponse;
        }

        // Verify the fingerprinted .NET 10 Blazor boot script loaded successfully.
        var jsUrl = frameworkResponses.Keys
            .FirstOrDefault(k => k.Contains("/blazor.webassembly.", StringComparison.OrdinalIgnoreCase)
                && k.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(jsUrl);
        Assert.Equal(200, frameworkResponses[jsUrl]);

        // Verify at least one .wasm framework file loaded successfully
        var wasmResponses = frameworkResponses
            .Where(kvp => kvp.Key.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(wasmResponses);
        Assert.All(wasmResponses, wr => Assert.Equal(200, wr.Value));

        // Verify the .NET runtime WASM (dotnet.wasm or dotnet.*.wasm) is among them
        var dotnetWasm = wasmResponses
            .FirstOrDefault(kvp => kvp.Key.Contains("/dotnet.", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(default, dotnetWasm);
    }

    /// <summary>
    /// Verifies blazor App Reload After Build No Cache Corruption.
    /// </summary>
    [Fact]
    public async Task BlazorApp_ReloadAfterBuild_NoCacheCorruption()
    {
        // Initial load — wait for Blazor to render or error UI
        await _page.GotoAsync(_baseUrl);

        await _page.WaitForFunctionAsync(@"() => {
            const loading = document.querySelector('#app .loading-progress');
            const errorUI = document.getElementById('blazor-error-ui');
            if (errorUI && errorUI.style.display !== 'none') return true;
            return !loading;
        }", null, new PageWaitForFunctionOptions { Timeout = 45000 });

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = 30000 });

        var hasContent = await _page.EvaluateAsync<bool>(@"() => {
            const app = document.getElementById('app');
            return app && app.children.length > 0 && app.innerHTML.trim().length > 0;
        }");
        Assert.True(hasContent, "App should render content on initial load");

        // Collect 404s during the reload
        var reload404s = new List<IResponse>();

        void OnResponse(object? _, IResponse response)
        {
            if (response.Url.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)
                && response.Status == 404)
            {
                reload404s.Add(response);
            }
        }

        _page.Response += OnResponse;

        try
        {
            // Reload the page
            await _page.ReloadAsync();

            // Wait for Blazor to re-render after reload
            await _page.WaitForFunctionAsync(@"() => {
                const loading = document.querySelector('#app .loading-progress');
                const errorUI = document.getElementById('blazor-error-ui');
                if (errorUI && errorUI.style.display !== 'none') return true;
                return !loading;
            }", null, new PageWaitForFunctionOptions { Timeout = 45000 });

            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30000 });
        }
        finally
        {
            _page.Response -= OnResponse;
        }

        // Verify the app still renders correctly after reload
        var hasContentAfterReload = await _page.EvaluateAsync<bool>(@"() => {
            const app = document.getElementById('app');
            return app && app.children.length > 0 && app.innerHTML.trim().length > 0;
        }");
        Assert.True(hasContentAfterReload, "App should render content after reload");

        // Assert no 404 errors on _framework paths during reload
        Assert.Empty(reload404s);
    }
}


