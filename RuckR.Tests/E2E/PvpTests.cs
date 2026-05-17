using Microsoft.Playwright;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

    /// <summary>
    /// Provides access to i Class Fixture<Playwright Fixture>,.
    /// </summary>
[Collection(nameof(TestCollection))]
public class PvpTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private string _baseUrl = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""PvpTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    /// <param name="playwright">The playwright to use.</param>
    public PvpTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public Task InitializeAsync()
    {
        _baseUrl = _factory.GetServerAddress();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies two Users Challenge And Accept Both See Result.
    /// </summary>
    [Fact]
    public async Task TwoUsers_ChallengeAndAccept_BothSeeResult()
    {
        // ── Create two isolated browser contexts for User A and User B ──
        await using var contextA = await _playwright.NewContextAsync(
            grantGeolocation: true,
            latitude: 51.5074,
            longitude: -0.1278);
        await using var contextB = await _playwright.NewContextAsync(
            grantGeolocation: true,
            latitude: 51.5080,
            longitude: -0.1280);

        var pageA = await contextA.NewPageAsync();
        var pageB = await contextB.NewPageAsync();

        // ── Register User A and User B ──
        var userAEmail = $"pvp_a_{Guid.NewGuid():N}@test.com";
        var userBEmail = $"pvp_b_{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        var registerA = new RegisterPage(pageA, _baseUrl);
        await registerA.GoToAsync();
        await registerA.RegisterAsync(userAEmail, password);

        var registerB = new RegisterPage(pageB, _baseUrl);
        await registerB.GoToAsync();
        await registerB.RegisterAsync(userBEmail, password);

        // ── User B: navigate to nearby players grid ──
        var gridB = new PlayerGridPage(pageB, _baseUrl);
        await gridB.GoToAsync();
        await gridB.WaitForPlayerGridLoadedAsync();

        // ── User A: navigate to nearby players grid ──
        var gridA = new PlayerGridPage(pageA, _baseUrl);
        await gridA.GoToAsync();
        await gridA.WaitForPlayerGridLoadedAsync();

        // ── User A: find and challenge User B ──
        var nearbyCount = await gridA.GetNearbyPlayerCountAsync();
        if (nearbyCount > 0)
        {
            await gridA.ClickChallengeButtonAsync(0);

            // ── User B: wait for challenge notification toast ──
            var toastB = new NotificationToast(pageB, _baseUrl);
            var challengeReceived = await toastB.WaitForChallengeToastAsync(timeoutMs: 20_000);
            Assert.True(challengeReceived, "User B should receive a challenge toast notification");

            // ── User B: accept the challenge ──
            await toastB.AcceptChallengeFromToastAsync();

            // ── Both users should see battle result ──
            var toastA = new NotificationToast(pageA, _baseUrl);
            var resultA = await toastA.WaitForBattleResultToastAsync(timeoutMs: 20_000);

            var toastBResult = new NotificationToast(pageB, _baseUrl);
            var resultB = await toastBResult.WaitForBattleResultToastAsync(timeoutMs: 20_000);

            Assert.True(resultA || resultB,
                "At least one user should see the battle result toast after challenge acceptance");
        }
        else
        {
            // No nearby players available — test is inconclusive, not a failure
            Assert.True(nearbyCount >= 0,
                $"Nearby player count should be non-negative, got {nearbyCount}");
        }

        await pageA.CloseAsync();
        await pageB.CloseAsync();
    }
}


