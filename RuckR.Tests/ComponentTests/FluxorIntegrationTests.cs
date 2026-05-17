using Bunit;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using RuckR.Client.Services;
using RuckR.Client.Store.LocationFeature;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class FluxorIntegrationTests : TestContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""FluxorIntegrationTests"""/> class.
    /// </summary>
    public FluxorIntegrationTests()
    {
        Services.AddFluxor(o =>
        {
            o.ScanAssemblies(typeof(SignalRClientService).Assembly);
        });

        Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    /// <summary>
    /// Verifies fluxor Store Initializes Without Error.
    /// </summary>
    public async Task FluxorStore_InitializesWithoutError()
    {
        var store = Services.GetRequiredService<IStore>();
        Assert.NotNull(store);

        var ex = await Record.ExceptionAsync(() => store.InitializeAsync());
        Assert.Null(ex);
    }

    [Fact]
    /// <summary>
    /// Verifies fluxor Store Has Expected Feature States.
    /// </summary>
    public async Task FluxorStore_HasExpectedFeatureStates()
    {
        var store = Services.GetRequiredService<IStore>();
        await store.InitializeAsync();

        // Fluxor registers features keyed by state type name.
        // Verify at least 5 feature slices exist (Location, Map, Inventory, Battle, Game).
        Assert.True(store.Features.Count >= 5,
            $"Expected at least 5 Fluxor features, got {store.Features.Count}");

        // Verify specific feature keys present via the features dictionary
        var featureKeys = store.Features.Keys.ToHashSet();
        Assert.Contains("RuckR.Client.Store.LocationFeature.LocationState", featureKeys);
        Assert.Contains("RuckR.Client.Store.MapFeature.MapState", featureKeys);
        Assert.Contains("RuckR.Client.Store.InventoryFeature.InventoryState", featureKeys);
        Assert.Contains("RuckR.Client.Store.BattleFeature.BattleState", featureKeys);
        Assert.Contains("RuckR.Client.Store.GameFeature.GameState", featureKeys);
    }

    [Fact]
    /// <summary>
    /// Verifies fluxor Dispatch Does Not Throw.
    /// </summary>
    public async Task FluxorDispatch_DoesNotThrow()
    {
        var store = Services.GetRequiredService<IStore>();
        await store.InitializeAsync();

        var dispatcher = Services.GetRequiredService<IDispatcher>();

        var ex = Record.Exception(() =>
        {
            dispatcher.Dispatch(new UpdatePositionAction(
                Latitude: 51.5074,
                Longitude: -0.1278,
                Accuracy: 10.0));
        });
        Assert.Null(ex);
    }
}


