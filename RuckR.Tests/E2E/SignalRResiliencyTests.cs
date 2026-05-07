using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

/// <summary>
/// Tests SignalR connection resiliency: disconnect detection,
/// reconnect button appearance, and automatic reconnection.
/// Uses Playwright's network emulation to simulate
/// disconnections and verify the ConnectionStatus shared component.
/// </summary>
public class SignalRResiliencyTests : IClassFixture<CustomWebApplicationFactory>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private string _baseUrl = null!;

    public SignalRResiliencyTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public async Task InitializeAsync()
    {
        _context = await _playwright.NewContextAsync();
        _baseUrl = _factory.ServerBaseUrl;
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    /// <summary>
    /// Verify that after a forced network disconnect, the ConnectionStatus
    /// component transitions to Disconnected and shows the Reconnect button.
    /// </summary>
    [Fact]
    public async Task ConnectionStatus_ShowsDisconnectedAndReconnectButton_AfterNetworkLoss()
    {
        var page = await _context.NewPageAsync();
        var connectionStatus = new ConnectionStatus(page, _baseUrl);

        try
        {
            await page.GotoAsync(_baseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for SignalR to establish the initial connection
            await page.WaitForTimeoutAsync(3000);

            // Force offline — this should trigger SignalR disconnection
            await _context.SetOfflineAsync(true);

            // Wait for the reconnect UI to appear
            await page.WaitForSelectorAsync("[data-testid='reconnect-btn']", new PageWaitForSelectorOptions
            {
                Timeout = 15_000
            });

            // Verify the disconnect state is detected
            Assert.True(await connectionStatus.IsDisconnectedAsync(),
                "ConnectionStatus should report Disconnected after network loss");

            // Bring the network back
            await _context.SetOfflineAsync(false);

            // Wait for reconnection
            await page.WaitForTimeoutAsync(5000);

            // After reconnection, the reconnect button should be gone
            var reconnectBtn = await page.QuerySelectorAsync("[data-testid='reconnect-btn']");
            Assert.Null(reconnectBtn);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Verify the ConnectionStatus component transitions through its
    /// reconnect states (Reconnecting → Connected) when network is
    /// restored.
    /// </summary>
    [Fact]
    public async Task ConnectionStatus_ReconnectsAfterNetworkRestored()
    {
        var page = await _context.NewPageAsync();

        try
        {
            await page.GotoAsync(_baseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(3000);

            // Force offline
            await _context.SetOfflineAsync(true);
            await page.WaitForSelectorAsync("[data-testid='reconnect-btn']", new PageWaitForSelectorOptions
            {
                Timeout = 10_000
            });

            // Restore network
            await _context.SetOfflineAsync(false);

            // Wait for automatic reconnection (retry intervals: 0, 2s, 10s)
            // The first retry at 0ms should trigger immediately
            await page.WaitForTimeoutAsync(8000);

            // After reconnection, status should be Connected
            var connectionStatus = new ConnectionStatus(page, _baseUrl);
            Assert.True(await connectionStatus.IsConnectedAsync(),
                "ConnectionStatus should report Connected after network restored");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Verify the ConnectionStatus component is present and reports
    /// Connected on a healthy page load (baseline test).
    /// </summary>
    [Fact]
    public async Task ConnectionStatus_ReportsConnected_OnHealthyStartup()
    {
        var page = await _context.NewPageAsync();

        try
        {
            await page.GotoAsync($"{_baseUrl}map");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Give SignalR time to connect
            await page.WaitForTimeoutAsync(5000);

            var connectionStatus = new ConnectionStatus(page, _baseUrl);
            Assert.True(await connectionStatus.IsConnectedAsync(),
                "ConnectionStatus should report Connected on healthy startup");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
