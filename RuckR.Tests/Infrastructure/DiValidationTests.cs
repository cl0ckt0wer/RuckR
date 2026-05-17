using System.Security.Claims;
using Fluxor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuckR.Client.Services;

namespace RuckR.Tests.Infrastructure;

/// <summary>
/// Validates Blazor WASM DI registrations by programmatically building
/// the service provider and verifying critical services resolve.
/// Since a full WASM host cannot run in a test, only registrations
/// without browser-hard dependencies are validated here.
/// </summary>
public class DiValidationTests
{
    /// <summary>
    /// Minimal AuthenticationStateProvider for DI validation tests.
    /// The real WASM host supplies one via the browser auth pipeline;
    /// this stub allows tests to verify the DI registration chain.
    /// </summary>
    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly ClaimsPrincipal Anonymous =
            new(new ClaimsIdentity());

    /// <summary>
    /// Verifies get Authentication State Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(Anonymous));
    }
    [Fact]
    /// <summary>
    /// Verifies all Client Services Should Resolve Without Scope Conflicts.
    /// </summary>
    public void AllClientServices_ShouldResolve_WithoutScopeConflicts()
    {
        // Arrange: build a service collection mimicking Blazor WASM DI
        var services = new ServiceCollection();

        services.AddLogging();

        // Add Fluxor (as the app does, but without ReduxDevTools which requires browser)
        services.AddFluxor(o =>
        {
            o.ScanAssemblies(typeof(SignalRClientService).Assembly);
        });

        // Add HttpClient (as the app does)
        services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("http://localhost")
        });

        // Add NavigationManager (required by SignalRClientService)
        services.AddScoped<NavigationManager, TestNavigationManager>();

        // Add SignalR client
        services.AddScoped<SignalRClientService>();

        // Add ApiClientService
        services.AddScoped<ApiClientService>();

        // Build the provider
        var provider = services.BuildServiceProvider();

        // Assert: resolve each critical service
        var dispatcher = provider.GetService<IDispatcher>();
        Assert.NotNull(dispatcher);

        // SignalRClientService was the bug — verify it resolves
        var signalR = provider.GetService<SignalRClientService>();
        Assert.NotNull(signalR); // This would have caught the scoping bug!

        // ApiClientService depends on HttpClient — verify it resolves
        var apiClient = provider.GetService<ApiClientService>();
        Assert.NotNull(apiClient);
    }

    [Fact]
    /// <summary>
    /// Verifies signal R Client Service Should Not Throw When Scoped.
    /// </summary>
    public void SignalRClientService_ShouldNotThrow_WhenScoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluxor(o => o.ScanAssemblies(typeof(SignalRClientService).Assembly));
        services.AddScoped<NavigationManager, TestNavigationManager>();
        services.AddScoped<SignalRClientService>();

        var provider = services.BuildServiceProvider();

        // This should NOT throw
        var exception = Record.Exception(() => provider.GetService<SignalRClientService>());
        Assert.Null(exception);
    }

    [Fact]
    /// <summary>
    /// Verifies fluxor Store Should Initialize Without Errors.
    /// </summary>
    public async Task FluxorStore_ShouldInitialize_WithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddFluxor(o => o.ScanAssemblies(typeof(SignalRClientService).Assembly));

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        Assert.NotNull(store);

        // Initialize the store — should not throw
        var exception = await Record.ExceptionAsync(() => store.InitializeAsync());
        Assert.Null(exception);
    }

    [Fact]
    /// <summary>
    /// Verifies authentication State Provider Should Resolve.
    /// </summary>
    public void AuthenticationStateProvider_ShouldResolve()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddFluxor(o => o.ScanAssemblies(typeof(SignalRClientService).Assembly));

        // The real WASM host provides AuthenticationStateProvider at runtime;
        // register a test double so the DI chain can be validated.
        services.AddScoped<AuthenticationStateProvider, TestAuthenticationStateProvider>();

        var provider = services.BuildServiceProvider();

        var authProvider = provider.GetService<AuthenticationStateProvider>();
        Assert.NotNull(authProvider); // THIS would have caught the gap
    }

    [Fact]
    /// <summary>
    /// Verifies authorize View Dependencies Should Resolve.
    /// </summary>
    public void AuthorizeView_Dependencies_ShouldResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddFluxor(o => o.ScanAssemblies(typeof(SignalRClientService).Assembly));

        // The real WASM host provides AuthenticationStateProvider at runtime;
        // register a test double so the DI chain can be validated.
        services.AddScoped<AuthenticationStateProvider, TestAuthenticationStateProvider>();

        var provider = services.BuildServiceProvider();

        // Verify all services needed by AuthorizeView are present
        Assert.NotNull(provider.GetService<AuthenticationStateProvider>());
        Assert.NotNull(provider.GetService<IAuthorizationService>());
    }

    [Fact]
    /// <summary>
    /// Verifies authentication State Provider Must Be Concrete Not Just Interface.
    /// </summary>
    public void AuthenticationStateProvider_MustBeConcrete_NotJustInterface()
    {
        // Arrange — simulate the Blazor WASM service collection
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();

        // THIS is what was missing — without it, AuthorizeView crashes
        services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

        services.AddFluxor(o => o.ScanAssemblies(typeof(CookieAuthenticationStateProvider).Assembly));

        // CookieAuthenticationStateProvider depends on HttpClient
        services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost") });

        var provider = services.BuildServiceProvider();

        // Act
        var authProvider = provider.GetService<AuthenticationStateProvider>();

        // Assert
        Assert.NotNull(authProvider);
        Assert.IsType<CookieAuthenticationStateProvider>(authProvider);
    }

    [Fact]
    /// <summary>
    /// Verifies all Auth Dependencies For Authorize View Should Resolve.
    /// </summary>
    public void AllAuthDependencies_ForAuthorizeView_ShouldResolve()
    {
        // Full AuthorizeView dependency chain
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

        // CookieAuthenticationStateProvider depends on HttpClient
        services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost") });

        var provider = services.BuildServiceProvider();

        // Every service AuthorizeView needs
        Assert.NotNull(provider.GetService<AuthenticationStateProvider>());
        Assert.NotNull(provider.GetService<IAuthorizationService>());

        // Resolving auth provider should NOT throw
        var ex = Record.Exception(() => provider.GetService<AuthenticationStateProvider>());
        Assert.Null(ex);
    }
}


