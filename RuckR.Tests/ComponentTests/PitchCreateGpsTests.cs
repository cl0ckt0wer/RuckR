using Bunit;
using RuckR.Client.Pages;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// UI tests for the disabled manual pitch creation route.
/// </summary>
public class PitchCreateGpsTests : TestContext
{
    /// <summary>
    /// Verifies the old manual pitch form is no longer available.
    /// </summary>
    [Fact]
    public void PitchCreate_ShowsDisabledState()
    {
        var cut = Render<PitchCreate>();

        Assert.NotNull(cut.Find("[data-testid='pitch-create-disabled']"));
        Assert.Contains("Manual pitch creation is disabled", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='pitch-submit']"));
    }
}
