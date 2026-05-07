using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuckR.Client.Shared;

namespace RuckR.Tests.ComponentTests;

public class NavMenuTests : TestContext
{
    public NavMenuTests()
    {
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
        Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService());
    }

    [Fact]
    public void NavMenu_RendersAllNavLinks_WhenUnauthenticated()
    {
        var authProvider = new TestAuthProvider(authenticated: false);
        Services.AddSingleton<AuthenticationStateProvider>(authProvider);
        // Replace the always-success auth with one that denies,
        // so AuthorizeView renders the Login link instead of Logout.
        Services.AddSingleton<IAuthorizationService>(new TestAuthorizationService(allow: false));

        var cut = RenderComponent<NavMenu>();

        var navLinks = cut.FindAll("[data-testid^='nav-']");
        Assert.True(navLinks.Count >= 7, $"Expected at least 7 nav links, got {navLinks.Count}");

        var loginLink = cut.Find("[data-testid='nav-login']");
        Assert.NotNull(loginLink);
        Assert.Contains("Login", loginLink.TextContent);
    }

    [Fact]
    public void NavMenu_RendersLogout_WhenAuthenticated()
    {
        var authProvider = new TestAuthProvider(authenticated: true, username: "TestPlayer");
        Services.AddSingleton<AuthenticationStateProvider>(authProvider);

        var cut = RenderComponent<NavMenu>();

        var logoutLink = cut.Find("[data-testid='nav-logout']");
        Assert.NotNull(logoutLink);
        Assert.Contains("Logout", logoutLink.TextContent);

        var usernameSpan = cut.Find(".nav-link-text");
        Assert.Contains("TestPlayer", usernameSpan.TextContent);
    }

    [Fact]
    public void NavMenu_ContainsAllExpectedRoutes()
    {
        var authProvider = new TestAuthProvider(authenticated: true, username: "TestPlayer");
        Services.AddSingleton<AuthenticationStateProvider>(authProvider);

        var cut = RenderComponent<NavMenu>();

        string[] expectedTestIds =
        {
            "nav-map", "nav-catalog", "nav-collection",
            "nav-nearby", "nav-battles", "nav-history", "nav-create-pitch"
        };

        foreach (var testId in expectedTestIds)
        {
            var element = cut.Find($"[data-testid='{testId}']");
            Assert.NotNull(element);
        }
    }
}
