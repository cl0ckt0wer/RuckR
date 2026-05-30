using Bunit;
using RuckR.Client.Shared;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

public class BattleResolutionOverlayTests : TestContext
{
    public BattleResolutionOverlayTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/battle-resolution.module.js");
    }

    [Fact]
    public void Victory_RendersCurrentUserAsYourSide()
    {
        var cut = RenderOverlay(CurrentUsername: "alice@test.com");

        Assert.Contains("Victory", cut.Find("[data-testid='battle-resolution-title']").TextContent);
        Assert.Contains("Captain Alice", cut.Find("[data-testid='battle-resolution-your-player']").TextContent);
        Assert.Contains("Crash Ball", cut.Find("[data-testid='battle-resolution-your-move']").TextContent);
        Assert.Contains("Score 91.5", cut.Find("[data-testid='battle-resolution-your-score']").TextContent);
        Assert.Contains("Captain Bob", cut.Find("[data-testid='battle-resolution-opponent-player']").TextContent);
    }

    [Fact]
    public void Defeat_RendersCurrentUserAsLosingSide()
    {
        var cut = RenderOverlay(CurrentUsername: "bob@test.com");

        Assert.Contains("Defeat", cut.Find("[data-testid='battle-resolution-title']").TextContent);
        Assert.Contains("Captain Bob", cut.Find("[data-testid='battle-resolution-your-player']").TextContent);
        Assert.Contains("Grubber Kick", cut.Find("[data-testid='battle-resolution-your-move']").TextContent);
        Assert.Contains("Score 73.25", cut.Find("[data-testid='battle-resolution-your-score']").TextContent);
        Assert.Contains("Captain Alice", cut.Find("[data-testid='battle-resolution-opponent-player']").TextContent);
    }

    [Fact]
    public void MethodAndScores_AreVisible()
    {
        var cut = RenderOverlay(CurrentUsername: "alice@test.com");

        Assert.Contains("Crash Ball charges down Grubber Kick", cut.Find("[data-testid='battle-resolution-method']").TextContent);
        Assert.Contains("Score 91.5", cut.Markup);
        Assert.Contains("Score 73.25", cut.Markup);
    }

    [Fact]
    public void CloseButton_InvokesDismissCallback()
    {
        var dismissed = false;
        var cut = RenderOverlay(CurrentUsername: "alice@test.com", onDismiss: () => dismissed = true);

        cut.Find("[data-testid='battle-resolution-close']").Click();

        Assert.True(dismissed);
    }

    [Fact]
    public void HistoryButton_InvokesViewHistoryCallback()
    {
        var viewedHistory = false;
        var cut = RenderOverlay(CurrentUsername: "alice@test.com", onViewHistory: () => viewedHistory = true);

        cut.Find("[data-testid='battle-resolution-history']").Click();

        Assert.True(viewedHistory);
    }

    private IRenderedComponent<BattleResolutionOverlay> RenderOverlay(
        string CurrentUsername,
        Action? onDismiss = null,
        Action? onViewHistory = null)
    {
        return Render<BattleResolutionOverlay>(parameters => parameters
            .Add(p => p.Result, Result())
            .Add(p => p.CurrentUsername, CurrentUsername)
            .Add(p => p.OnDismiss, onDismiss ?? (() => { }))
            .Add(p => p.OnViewHistory, onViewHistory ?? (() => { })));
    }

    private static BattleResult Result() =>
        new(
            "alice@test.com",
            "bob@test.com",
            "Captain Alice",
            "Captain Bob",
            "Crash Ball charges down Grubber Kick",
            new DateTime(2026, 5, 30, 14, 0, 0, DateTimeKind.Utc),
            BattleMove.Rock,
            BattleMove.Scissors,
            91.5,
            73.25,
            BattleMove.Rock,
            BattleMove.Scissors,
            91.5,
            73.25);
}
