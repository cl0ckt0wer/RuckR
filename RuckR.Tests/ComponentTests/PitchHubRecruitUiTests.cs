using System.Reflection;
using Bunit;
using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NetTopologySuite.Geometries;
using RuckR.Client.Pages;
using RuckR.Client.Services;
using RuckR.Client.Store.GameFeature;
using RuckR.Client.Store.LocationFeature;
using RuckR.Client.Store.MapFeature;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// UI tests for the pitch hub recruitment entry point.
/// </summary>
public class PitchHubRecruitUiTests : IAsyncLifetime
{
    private readonly BunitContext _context = new();
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly TestState<MapState> _mapState = new(new MapState
    {
        VisibleEncounters = new[]
        {
            new PlayerEncounterDto(
                Guid.Parse("f8d3e239-07c1-471a-9e20-04e7d1ce2c01"),
                PlayerId: 12,
                Name: "Scrum Spark",
                Position: "Flanker",
                Rarity: "Rare",
                Level: 7,
                Latitude: 51.508894,
                Longitude: -0.126352,
                ExpiresAtUtc: DateTime.UtcNow.AddMinutes(20),
                SuccessChancePercent: 70,
                BaseRecruitmentSeconds: 120,
                ParkName: "Test Pitch")
        }
    });
    private readonly TestState<LocationState> _locationState = new(new LocationState
    {
        UserLatitude = 51.508894,
        UserLongitude = -0.126352,
        AccuracyMeters = 20,
        IsWatching = true
    });

    /// <summary>
    /// Initializes a new instance of the <see cref="PitchHubRecruitUiTests"/> class.
    /// </summary>
    public PitchHubRecruitUiTests()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddMudServices();
        _context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Map:EnableMapDiagnostics"] = "false",
                ["Map:EnableAutoGpsWatch"] = "false",
                ["Map:EnableGameGraphics"] = "false"
            })
            .Build());
        _context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthProvider(authenticated: true, username: "ruckr1@ruckr.game"));
        _context.Services.AddSingleton<IGeolocationService>(new FakeGeolocationService());
        _context.Services.AddSingleton(new ApiClientService(new HttpClient { BaseAddress = new Uri("https://example.test/") }, NullLogger<ApiClientService>.Instance));
        _context.Services.AddSingleton<IDispatcher>(_dispatcher);
        _context.Services.AddSingleton<IState<MapState>>(_mapState);
        _context.Services.AddSingleton<IState<LocationState>>(_locationState);
        _context.Services.AddSingleton<IState<GameState>>(new TestState<GameState>(new GameState()));
        _context.Services.AddSingleton(sp => new SignalRClientService(
            sp.GetRequiredService<IDispatcher>(),
            sp.GetRequiredService<IState<GameState>>(),
            sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(),
            NullLogger<SignalRClientService>.Instance));
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync() => await _context.DisposeAsync();

    /// <summary>
    /// Verifies clicking Recruit here closes the pitch hub and switches to recruit-board mode.
    /// </summary>
    [Fact]
    public void RecruitHere_WhenPitchHasActiveRecruits_ClosesPitchHubAndOpensRecruitBoardMode()
    {
        var cut = _context.Render<GameMap>();
        ShowPitchHub(cut, activeRecruitCount: 2);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='pitch-overlay']"));
            Assert.Equal("2", cut.Find("[data-testid='active-recruit-count']").TextContent.Trim());
            Assert.False(cut.Find("[data-testid='capture-players-btn']").HasAttribute("disabled"));
        });

        cut.Find("[data-testid='capture-players-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='pitch-overlay']"));
            Assert.True(GetPrivate<bool>(cut.Instance, "_showRecruitBoard"));
            Assert.Null(GetPrivate<PitchModel?>(cut.Instance, "_selectedPitch"));
            Assert.Null(GetPrivate<PitchHubDto?>(cut.Instance, "_pitchHub"));
            Assert.Contains(_dispatcher.DispatchedActions, action => action is ClearSelectionAction);
        });
    }

    /// <summary>
    /// Verifies Recruit here is disabled when the hub has no active recruits.
    /// </summary>
    [Fact]
    public void RecruitHere_WhenPitchHasNoActiveRecruits_IsDisabled()
    {
        var cut = _context.Render<GameMap>();
        ShowPitchHub(cut, activeRecruitCount: 0);

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("[data-testid='capture-players-btn']");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Contains("No local activity is active yet.", cut.Find("[data-testid='capture-status']").TextContent);
        });
    }

    private static void ShowPitchHub(IRenderedComponent<GameMap> cut, int activeRecruitCount)
    {
        SetPrivate(cut.Instance, "_isAuthenticated", true);
        SetPrivate(cut.Instance, "_selectedPitch", new PitchModel
        {
            Id = 99,
            Name = "Test Pitch Hub",
            Type = PitchType.Standard,
            Source = "ArcGISPlaces",
            SourceConfidence = 88,
            Latitude = 51.508894,
            Longitude = -0.126352,
            Location = new Point(-0.126352, 51.508894) { SRID = 4326 }
        });
        SetPrivate(cut.Instance, "_pitchHub", new PitchHubDto(
            PitchId: 99,
            Name: "Test Pitch Hub",
            Type: "Standard",
            Latitude: 51.508894,
            Longitude: -0.126352,
            Source: "ArcGISPlaces",
            SourceConfidence: 88,
            DistanceMeters: 12,
            DistanceBucket: "Within50m",
            CanInteract: true,
            Reason: "ELIGIBLE",
            ActiveRecruitCount: activeRecruitCount,
            ChallengeableUserCount: 0));
        SetPrivate(cut.Instance, "_showRecruitBoard", false);

        cut.Render();
    }

    private static void SetPrivate<T>(GameMap instance, string fieldName, T value)
    {
        var field = typeof(GameMap).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private static T GetPrivate<T>(GameMap instance, string fieldName)
    {
        var field = typeof(GameMap).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(instance)!;
    }

    private sealed class TestState<TState>(TState value) : IState<TState>
    {
        public TState Value { get; private set; } = value;

        public event EventHandler? StateChanged;

        public void SetValue(TState value)
        {
            Value = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> DispatchedActions { get; } = new();

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            DispatchedActions.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public event Action<GeoPosition>? PositionChanged;

        public Task<GeoPosition?> GetCurrentPositionAsync() => Task.FromResult<GeoPosition?>(null);

        public Task StartWatchAsync() => Task.CompletedTask;

        public void StopWatch()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Push(GeoPosition position) => PositionChanged?.Invoke(position);
    }
}
