using Microsoft.Extensions.DependencyInjection;
using RuckR.Server.Hubs;
using RuckR.Server.Services;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Infrastructure;

/// <summary>
/// Validates Server-side DI registrations using the test
/// <see cref="CustomWebApplicationFactory"/> to ensure all
/// critical services resolve with correct lifetimes.
/// </summary>
[Collection(nameof(TestCollection))]
    /// <summary>
    /// Provides access to class.
    /// </summary>
public class ServerDiValidationTests
{
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="""ServerDiValidationTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    public ServerDiValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    /// <summary>
    /// Verifies battle Hub Should Resolve.
    /// </summary>
    public void BattleHub_ShouldResolve()
    {
        using var scope = _factory.Services.CreateScope();
        var hub = ActivatorUtilities.CreateInstance<BattleHub>(scope.ServiceProvider);
        Assert.NotNull(hub);
    }

    [Fact]
    /// <summary>
    /// Verifies battle Resolver Should Resolve.
    /// </summary>
    public void BattleResolver_ShouldResolve()
    {
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetService<IBattleResolver>();
        Assert.NotNull(resolver);
    }

    [Fact]
    /// <summary>
    /// Verifies location Tracker Should Be Singleton.
    /// </summary>
    public void LocationTracker_ShouldBeSingleton()
    {
        var tracker1 = _factory.Services.GetService<ILocationTracker>();
        var tracker2 = _factory.Services.GetService<ILocationTracker>();
        Assert.NotNull(tracker1);
        Assert.Same(tracker1, tracker2);
    }

    [Fact]
    /// <summary>
    /// Verifies db Context Should Be Scoped.
    /// </summary>
    public void DbContext_ShouldBeScoped()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var db1 = scope1.ServiceProvider.GetService<RuckR.Server.Data.RuckRDbContext>();
        var db2 = scope2.ServiceProvider.GetService<RuckR.Server.Data.RuckRDbContext>();

        Assert.NotNull(db1);
        Assert.NotNull(db2);
        Assert.NotSame(db1, db2);
    }
}


