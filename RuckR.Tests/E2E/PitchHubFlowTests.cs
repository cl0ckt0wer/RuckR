using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using NetTopologySuite.Geometries;
using RuckR.Server.Services;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;
using RuckR.Tests.Pages;

namespace RuckR.Tests.E2E;

/// <summary>
/// Browser coverage for pitch hub interactions across supported pitch types.
/// </summary>
[Collection(nameof(TestCollection))]
public class PitchHubFlowTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PitchHubFlowTests"/> class.
    /// </summary>
    public PitchHubFlowTests(CustomWebApplicationFactory factory, PlaywrightFixture playwright)
    {
        _factory = factory;
        _playwright = playwright;
    }

    /// <summary>
    /// Creates a browser context with GPS permissions enabled.
    /// </summary>
    public async Task InitializeAsync()
    {
        _baseUrl = _factory.ServerBaseUrl;
        _context = await _playwright.NewContextAsync(
            grantGeolocation: true,
            latitude: 51.5074,
            longitude: -0.1278);
        _page = await _context.NewPageAsync();
    }

    /// <summary>
    /// Disposes the browser page and context.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    /// <summary>
    /// Verifies each pitch type can open its hub and enter the recruit board flow.
    /// </summary>
    [Theory]
    [InlineData(PitchType.Standard, "Standard pitch", 51.5700, -0.1278)]
    [InlineData(PitchType.Training, "Training pitch", 51.5074, -0.0300)]
    [InlineData(PitchType.Stadium, "Stadium", 51.4450, -0.1278)]
    public async Task PitchHub_RecruitHere_OpensRecruitBoard_ForEveryPitchType(
        PitchType pitchType,
        string expectedPitchTypeLabel,
        double latitude,
        double longitude)
    {
        var username = $"pitchhub_{pitchType}_{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";
        var registerPage = new RegisterPage(_page, _baseUrl);
        await registerPage.GoToAsync();
        await registerPage.RegisterAsync(username, password);

        var parkPlaceId = $"test-pitch-hub-{pitchType}-{Guid.NewGuid():N}";
        _factory.ParkService.UseParks(new RealWorldPark(
            parkPlaceId,
            $"{expectedPitchTypeLabel} Park",
            latitude,
            longitude,
            0));
        _factory.ParkService.UseNoPitchCandidates();

        var pitchId = await SeedPitchHubAsync(
            pitchType,
            expectedPitchTypeLabel,
            latitude,
            longitude,
            parkPlaceId);

        await _context.SetGeolocationAsync(new Geolocation
        {
            Latitude = (float)latitude,
            Longitude = (float)longitude
        });

        var mapPage = new MapPage(_page, _baseUrl);
        await mapPage.GoToAsync("basemap=empty&mapGraphics=true&autoGps=true&mapDiagnostics=false");

        Assert.True(await mapPage.WaitForMapLoadedAsync(30_000), "Map shell should load.");
        Assert.True(await mapPage.WaitForGeoBlazorSurfaceAsync(45_000), "GeoBlazor should attach a visible ArcGIS drawing surface.");
        await mapPage.WaitForLayerGraphicCountAsync("Player location", 2, 20_000);
        await mapPage.WaitForPitchGraphicByIdAsync(pitchId, 20_000);

        await mapPage.ClickPitchAreaAsync(latitude, longitude, 20_000);
        await mapPage.WaitForPitchHubRecruitReadyAsync(20_000);

        Assert.Equal(expectedPitchTypeLabel, await mapPage.GetPitchTypeTextAsync());
        Assert.True(await mapPage.GetActiveRecruitCountAsync() > 0, "Pitch hub should report active recruits.");

        await mapPage.ClickRecruitHereAsync();
        await mapPage.WaitForRecruitBoardAsync(10_000);
        Assert.False(await mapPage.IsPitchOverlayVisibleAsync(), "Recruit here should close the pitch hub.");

        await mapPage.SelectSpotlightEncounterAsync();
        Assert.Contains("Recruit window", await mapPage.GetSpotlightRecruitStateAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> SeedPitchHubAsync(
        PitchType pitchType,
        string label,
        double latitude,
        double longitude,
        string parkPlaceId)
    {
        var areaKey = $"place:{parkPlaceId.ToLowerInvariant()}";
        var seededPitchId = 0;
        await _factory.ExecuteInDbAsync(async db =>
        {
            var players = await db.Players
                .OrderBy(player => player.Id)
                .Take(8)
                .ToListAsync();
            Assert.True(players.Count >= 8, "The seed catalog should include at least eight recruitable players.");

            var pitch = new PitchModel
            {
                Name = $"{label} Flow {Guid.NewGuid():N}",
                Type = pitchType,
                Location = Point(longitude, latitude),
                Source = "ArcGISPlaces",
                ExternalPlaceId = parkPlaceId,
                SourceCategory = label,
                SourceMatchReason = "Playwright pitch hub flow",
                SourceConfidence = 91,
                CreatedAt = DateTime.UtcNow
            };
            db.Pitches.Add(pitch);
            await db.SaveChangesAsync();
            seededPitchId = pitch.Id;

            var now = DateTime.UtcNow;
            for (var i = 0; i < players.Count; i++)
            {
                var offset = OffsetMeters(latitude, longitude, northMeters: 160 + (i * 30), eastMeters: i % 2 == 0 ? 90 : -90);
                db.PlayerEncounters.Add(new PlayerEncounterModel
                {
                    Id = Guid.NewGuid(),
                    PlayerId = players[i].Id,
                    Latitude = offset.Latitude,
                    Longitude = offset.Longitude,
                    AreaKey = areaKey,
                    ParkPlaceId = parkPlaceId,
                    CreatedAtUtc = now.AddMinutes(-1),
                    ExpiresAtUtc = now.AddMinutes(30),
                    RecruitmentBaseDurationSeconds = 60,
                    RecruitmentRequiredDurationSeconds = 60
                });
            }

            await db.SaveChangesAsync();
        });

        return seededPitchId;
    }

    private static Point Point(double longitude, double latitude) =>
        new(longitude, latitude) { SRID = 4326 };

    private static (double Latitude, double Longitude) OffsetMeters(
        double latitude,
        double longitude,
        double northMeters,
        double eastMeters)
    {
        const double metersPerDegreeLatitude = 111_320.0;
        var metersPerDegreeLongitude = metersPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180.0);
        return (
            latitude + northMeters / metersPerDegreeLatitude,
            longitude + eastMeters / metersPerDegreeLongitude);
    }
}
