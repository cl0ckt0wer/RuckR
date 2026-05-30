using Microsoft.Playwright;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
public class BattleMobileLayoutTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private string _baseUrl = null!;

    public BattleMobileLayoutTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    public Task InitializeAsync()
    {
        _baseUrl = _factory.GetServerAddress();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BattleArena_OnMobile_FitsTabsChallengeListsAndPickSelector()
    {
        var usernameA = $"battle_mobile_a_{Guid.NewGuid():N}@test.com";
        var usernameB = $"battle_mobile_b_{Guid.NewGuid():N}";
        const string password = "TestPass123!";

        await using var context = await _playwright.NewContextAsync(isMobile: true);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(360, 800);

        var registerPage = new RegisterPage(page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(usernameA, password);

        var userAId = await GetUserIdAsync(usernameA);
        var userBId = await _factory.CreateTestUserAsync(usernameB, password);

        var (playerAId, playerBId, pitchId) = await GetBattleSeedIdsAsync();
        await _factory.SeedCollectionAsync(userAId, playerAId, pitchId);
        await _factory.SeedCollectionAsync(userBId, playerBId, pitchId);
        await SeedBattleStatesAsync(userAId, userBId);

        var battlePage = new BattlePage(page, _baseUrl);

        await battlePage.GoToAsync();
        await battlePage.WaitForBattlePageLoadedAsync();
        await AssertMobileTabSelectorFitsAsync(page, "battle arena new tab");
        await AssertNoHorizontalOverflowAsync(page, "battle arena new tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Incoming");
        await page.WaitForSelectorAsync("[data-testid='incoming-challenges'] [data-testid='pending-card']");
        await AssertMobileTabSelectorFitsAsync(page, "incoming challenge tab");
        await AssertNoHorizontalOverflowAsync(page, "incoming challenge tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Outgoing");
        await page.WaitForSelectorAsync("[data-testid='outgoing-challenges'] [data-testid='pending-card']");
        await AssertMobileTabSelectorFitsAsync(page, "outgoing challenge tab");
        await AssertNoHorizontalOverflowAsync(page, "outgoing challenge tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Pick");
        await page.WaitForSelectorAsync("[data-testid='selection-challenges'] [data-testid='accepted-card']");
        await AssertMobileTabSelectorFitsAsync(page, "pick challenge tab");
        await AssertNoHorizontalOverflowAsync(page, "pick challenge tab", "[data-testid='battle-tabs']");

        await page.GetByTestId("pick-selection-btn").ClickAsync();
        await page.WaitForSelectorAsync("[data-testid='selection-modal']");
        await AssertNoHorizontalOverflowAsync(page, "pick selector modal", "[data-testid='selection-modal'] .game-modal");

        await page.CloseAsync();
    }

    private async Task<string> GetUserIdAsync(string username)
    {
        var userId = string.Empty;

        await _factory.ExecuteInDbAsync(db =>
        {
            userId = db.Users.Single(u => u.UserName == username).Id;
            return Task.CompletedTask;
        });

        return userId;
    }

    private async Task<(int PlayerAId, int PlayerBId, int PitchId)> GetBattleSeedIdsAsync()
    {
        var playerAId = 0;
        var playerBId = 0;
        var pitchId = 0;

        await _factory.ExecuteInDbAsync(db =>
        {
            var players = db.Players.OrderBy(p => p.Id).Take(2).ToList();
            Assert.True(players.Count >= 2, "Battle mobile layout test requires at least two seeded recruits.");

            var pitch = db.Pitches.OrderBy(p => p.Id).FirstOrDefault();
            Assert.NotNull(pitch);

            playerAId = players[0].Id;
            playerBId = players[1].Id;
            pitchId = pitch.Id;
            return Task.CompletedTask;
        });

        return (playerAId, playerBId, pitchId);
    }

    private async Task SeedBattleStatesAsync(string userAId, string userBId)
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            var createdAt = DateTime.UtcNow.AddMinutes(-3);
            db.Battles.AddRange(
                new BattleModel
                {
                    ChallengerId = userBId,
                    OpponentId = userAId,
                    Status = BattleStatus.Pending,
                    CreatedAt = createdAt
                },
                new BattleModel
                {
                    ChallengerId = userAId,
                    OpponentId = userBId,
                    Status = BattleStatus.Pending,
                    CreatedAt = createdAt.AddSeconds(10)
                },
                new BattleModel
                {
                    ChallengerId = userAId,
                    OpponentId = userBId,
                    Status = BattleStatus.Accepted,
                    CreatedAt = createdAt.AddSeconds(20),
                    AcceptedAt = createdAt.AddSeconds(40)
                });

            await db.SaveChangesAsync();
        });
    }

    private static async Task AssertNoHorizontalOverflowAsync(IPage page, string state, string targetSelector)
    {
        var metrics = await page.EvaluateAsync<LayoutMetrics>(
            @"selector => {
                const root = document.documentElement;
                const body = document.body;
                const target = document.querySelector(selector);
                const rect = target
                    ? target.getBoundingClientRect()
                    : { left: 0, right: 0, width: 0 };

                return {
                    viewportWidth: window.innerWidth,
                    documentScrollWidth: root.scrollWidth,
                    bodyScrollWidth: body.scrollWidth,
                    targetLeft: rect.left,
                    targetRight: rect.right,
                    targetWidth: rect.width
                };
            }",
            targetSelector);

        var pageScrollWidth = Math.Max(metrics.DocumentScrollWidth, metrics.BodyScrollWidth);
        Assert.True(pageScrollWidth <= metrics.ViewportWidth + 1,
            $"{state} should not force horizontal page overflow. viewport={metrics.ViewportWidth}, scrollWidth={pageScrollWidth}.");
        Assert.True(metrics.TargetLeft >= -1 && metrics.TargetRight <= metrics.ViewportWidth + 1,
            $"{state} target should stay inside the viewport. viewport={metrics.ViewportWidth}, left={metrics.TargetLeft}, right={metrics.TargetRight}, width={metrics.TargetWidth}.");
    }

    private static async Task SelectBattleTabAsync(IPage page, string label)
    {
        var testId = label switch
        {
            "Incoming" => "battle-tab-incoming",
            "Outgoing" => "battle-tab-outgoing",
            "Pick" => "battle-tab-pick",
            _ => "battle-tab-new"
        };

        await page.GetByTestId(testId).ClickAsync();
    }

    private static async Task AssertMobileTabSelectorFitsAsync(IPage page, string state)
    {
        await page.WaitForSelectorAsync("[data-testid='battle-tab-selector']");

        var metrics = await page.EvaluateAsync<TabSelectorMetrics>(
            @"() => {
                const selector = document.querySelector('[data-testid=""battle-tab-selector""]');
                const selectorRect = selector
                    ? selector.getBoundingClientRect()
                    : { left: 0, right: 0, width: 0 };
                const buttons = selector ? Array.from(selector.querySelectorAll('button')) : [];
                const overflowingButtons = buttons.filter(button => {
                    const rect = button.getBoundingClientRect();
                    return rect.left < selectorRect.left - 1
                        || rect.right > selectorRect.right + 1
                        || rect.left < -1
                        || rect.right > window.innerWidth + 1
                        || button.scrollWidth > button.clientWidth + 1;
                });
                const clippedLabels = buttons.filter(button => {
                    const label = button.querySelector('span:first-child');
                    return label && label.scrollWidth > label.clientWidth + 1;
                });
                const mudTabbar = document.querySelector('.battle-tabs .mud-tabs-tabbar, .battle-tabs .mud-tabs-toolbar');

                return {
                    viewportWidth: window.innerWidth,
                    selectorDisplay: selector ? getComputedStyle(selector).display : '',
                    selectorLeft: selectorRect.left,
                    selectorRight: selectorRect.right,
                    selectorWidth: selectorRect.width,
                    buttonCount: buttons.length,
                    overflowingButtonCount: overflowingButtons.length,
                    clippedLabelCount: clippedLabels.length,
                    mudTabbarDisplay: mudTabbar ? getComputedStyle(mudTabbar).display : 'missing'
                };
            }");

        Assert.Equal(4, metrics.ButtonCount);
        Assert.NotEqual("none", metrics.SelectorDisplay);
        Assert.True(metrics.SelectorLeft >= -1 && metrics.SelectorRight <= metrics.ViewportWidth + 1,
            $"{state} mobile tab selector should stay inside the viewport. viewport={metrics.ViewportWidth}, left={metrics.SelectorLeft}, right={metrics.SelectorRight}, width={metrics.SelectorWidth}.");
        Assert.Equal(0, metrics.OverflowingButtonCount);
        Assert.Equal(0, metrics.ClippedLabelCount);
        Assert.Equal("none", metrics.MudTabbarDisplay);
    }

    private sealed class LayoutMetrics
    {
        public double ViewportWidth { get; set; }
        public double DocumentScrollWidth { get; set; }
        public double BodyScrollWidth { get; set; }
        public double TargetLeft { get; set; }
        public double TargetRight { get; set; }
        public double TargetWidth { get; set; }
    }

    private sealed class TabSelectorMetrics
    {
        public double ViewportWidth { get; set; }
        public string SelectorDisplay { get; set; } = string.Empty;
        public double SelectorLeft { get; set; }
        public double SelectorRight { get; set; }
        public double SelectorWidth { get; set; }
        public int ButtonCount { get; set; }
        public int OverflowingButtonCount { get; set; }
        public int ClippedLabelCount { get; set; }
        public string MudTabbarDisplay { get; set; } = string.Empty;
    }
}
