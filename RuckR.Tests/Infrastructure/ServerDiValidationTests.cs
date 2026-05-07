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
public class ServerDiValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServerDiValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void BattleHub_ShouldResolve()
    {
        using var scope = _factory.Services.CreateScope();
        var hub = ActivatorUtilities.CreateInstance<BattleHub>(scope.ServiceProvider);
        Assert.NotNull(hub);
    }

    [Fact]
    public void BattleResolver_ShouldResolve()
    {
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetService<IBattleResolver>();
        Assert.NotNull(resolver);
    }

    [Fact]
    public void LocationTracker_ShouldBeSingleton()
    {
        var tracker1 = _factory.Services.GetService<ILocationTracker>();
        var tracker2 = _factory.Services.GetService<ILocationTracker>();
        Assert.NotNull(tracker1);
        Assert.Same(tracker1, tracker2);
    }

    [Fact]
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
