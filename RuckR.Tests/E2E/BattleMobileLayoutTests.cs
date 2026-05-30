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
        await AssertNoHorizontalOverflowAsync(page, "battle arena new tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Incoming");
        await page.WaitForSelectorAsync("[data-testid='incoming-challenges'] [data-testid='pending-card']");
        await AssertNoHorizontalOverflowAsync(page, "incoming challenge tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Outgoing");
        await page.WaitForSelectorAsync("[data-testid='outgoing-challenges'] [data-testid='pending-card']");
        await AssertNoHorizontalOverflowAsync(page, "outgoing challenge tab", "[data-testid='battle-tabs']");

        await SelectBattleTabAsync(page, "Pick");
        await page.WaitForSelectorAsync("[data-testid='selection-challenges'] [data-testid='accepted-card']");
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
        await page.Locator($".battle-tabs .mud-tab:has-text('{label}')").First.ClickAsync();
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
}
