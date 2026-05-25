using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Services;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

    /// <summary>
    /// Provides access to :.
    /// </summary>
[Collection(nameof(TestCollection))]
public class RecruitmentApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _username = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""RecruitmentApiTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    public RecruitmentApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        _factory.LocationTracker.ClearAll();
        _factory.ParkService.UseDefaultPark();
        _username = $"recruit_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(_username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, _username);
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies get Encounters Returns Recruit Time Based On Rarity.
    /// </summary>
    [Fact]
    public async Task GetEncounters_ReturnsRecruitTimeBasedOnRarity()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.NotEmpty(encounters);

        var first = encounters![0];
        var expected = ExpectedBaseRecruitmentSeconds(first.Rarity);
        Assert.Equal(expected, first.BaseRecruitmentSeconds);
        Assert.Equal(100, first.SuccessChancePercent);
    }

    /// <summary>
    /// Verifies get Encounters Spawns Recruitable Players At Real World Park Locations.
    /// </summary>
    [Fact]
    public async Task GetEncounters_SpawnsRecruitablePlayersAtRealWorldParkLocations()
    {
        var beforeRequestUtc = DateTime.UtcNow;
        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.NotEmpty(encounters);

        Assert.All(encounters!, encounter =>
        {
            Assert.Equal(anchor.lat, encounter.Latitude, precision: 6);
            Assert.Equal(anchor.lng, encounter.Longitude, precision: 6);
            Assert.Equal("Test Real World Park", encounter.ParkName);
            Assert.Equal("test-park", encounter.ParkPlaceId);
            Assert.True(
                encounter.ExpiresAtUtc >= beforeRequestUtc.AddMinutes(115),
                $"Encounter should last close to two hours. Actual expiry: {encounter.ExpiresAtUtc:o}");
            Assert.False(string.IsNullOrWhiteSpace(encounter.Name));
            Assert.False(string.IsNullOrWhiteSpace(encounter.Rarity));
            Assert.InRange(encounter.BaseRecruitmentSeconds, 30, 600);
        });
    }

    /// <summary>
    /// Verifies get Encounters Keeps Active Encounters Stable Across Refreshes.
    /// </summary>
    [Fact]
    public async Task GetEncounters_KeepsActiveEncountersStableAcrossRefreshes()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var firstResponse = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstEncounters = await firstResponse.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(firstEncounters);
        Assert.NotEmpty(firstEncounters);

        var secondResponse = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondEncounters = await secondResponse.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(secondEncounters);

        var secondIds = secondEncounters!.Select(e => e.EncounterId).ToHashSet();
        Assert.All(firstEncounters!, encounter => Assert.Contains(encounter.EncounterId, secondIds));
    }

    /// <summary>
    /// Verifies deterministic demo fixtures can expose a nearby rare encounter without changing spawn rules.
    /// </summary>
    [Fact]
    public async Task GetEncounters_ReturnsSeededRareNearbyDemoEncounter()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        var encounterId = Guid.Parse("301f53a6-761e-4091-963c-1d114f2a0f32");
        int rarePlayerId = 0;

        await _factory.ExecuteInDbAsync(async db =>
        {
            var player = await db.Players.FirstAsync();
            player.Rarity = PlayerRarity.Legendary;
            player.Name = "Brass Boot";
            rarePlayerId = player.Id;

            db.PlayerEncounters.Add(new PlayerEncounterModel
            {
                Id = encounterId,
                UserId = _userId,
                PlayerId = player.Id,
                Latitude = anchor.lat,
                Longitude = anchor.lng,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            });

            await db.SaveChangesAsync();
        });

        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        var seeded = Assert.Single(encounters!, e => e.EncounterId == encounterId);
        Assert.Equal(rarePlayerId, seeded.PlayerId);
        Assert.Equal("Legendary", seeded.Rarity);
        Assert.Equal("Brass Boot", seeded.Name);
        Assert.True(seeded.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(11));
    }

    /// <summary>
    /// Verifies get Encounters No Real World Parks Returns No Recruitable Players.
    /// </summary>
    [Fact]
    public async Task GetEncounters_NoRealWorldParks_ReturnsNoRecruitablePlayers()
    {
        _factory.ParkService.UseNoParks();

        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.Empty(encounters!);
    }

    /// <summary>
    /// Verifies get Encounters Park Away From Player Spawns Recruitable At Park Not Player Location.
    /// </summary>
    [Fact]
    public async Task GetEncounters_ParkAwayFromPlayer_SpawnsRecruitableAtParkNotPlayerLocation()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        var parkLat = anchor.lat + 0.002;
        var parkLng = anchor.lng - 0.002;
        _factory.ParkService.UseParks(new RealWorldPark("frost-park", "Frost Park", parkLat, parkLng, 250));
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.NotEmpty(encounters);

        Assert.All(encounters!, encounter =>
        {
            Assert.Equal(parkLat, encounter.Latitude, precision: 6);
            Assert.Equal(parkLng, encounter.Longitude, precision: 6);
            Assert.Equal("Frost Park", encounter.ParkName);
            Assert.Equal("frost-park", encounter.ParkPlaceId);
        });
    }

    /// <summary>
    /// Verifies attempt Recruitment Expired Encounter Returns Failure.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_ExpiredEncounter_ReturnsFailure()
    {
        var encounter = await CreateEncounterAsync();
        await _factory.ExecuteInDbAsync(async db =>
        {
            var entity = await db.PlayerEncounters.FirstAsync(e => e.Id == encounter.EncounterId);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);
        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Duplicate Recruit Returns Failure.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_DuplicateRecruit_ReturnsFailure()
    {
        await SetUserProfileAsync(level: 100, xp: 9900);

        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);
        var successResult = await RecruitToCompletionAsync(encounter);

        Assert.NotNull(successResult);

        var duplicateEncounterId = Guid.NewGuid();
        await _factory.ExecuteInDbAsync(async db =>
        {
            db.PlayerEncounters.Add(new PlayerEncounterModel
            {
                Id = duplicateEncounterId,
                UserId = _userId,
                PlayerId = encounter!.PlayerId,
                Latitude = encounter.Latitude,
                Longitude = encounter.Longitude,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            });
            await db.SaveChangesAsync();
        });

        var duplicate = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(duplicateEncounterId, encounter!.PlayerId));
        duplicate.EnsureSuccessStatusCode();
        var duplicateResult = await duplicate.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();

        Assert.NotNull(duplicateResult);
        Assert.False(duplicateResult!.Success);
        Assert.Contains("already recruited", duplicateResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Too Far Returns Failure.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_TooFar_ReturnsFailure()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude + 0.05, encounter.Longitude + 0.05);

        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("park", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Within Five Kilometers Starts Recruitment.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WithinFiveKilometers_StartsRecruitment()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude + 0.03, encounter.Longitude);

        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Completed);
        Assert.Contains("Recruitment", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Without Gps Position Returns Bad Request.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WithoutGpsPosition_ReturnsBadRequest()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.ClearPosition(_userId);

        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GPS position required", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Uses Request Gps When SignalR Position Has Not Arrived.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WithRequestGpsAndNoTrackedPosition_StartsRecruitment()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.ClearPosition(_userId);

        var response = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(
                encounter.EncounterId,
                encounter.PlayerId,
                Latitude: encounter.Latitude,
                Longitude: encounter.Longitude,
                Accuracy: 25));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.False(result.Completed);
        Assert.Contains("Recruitment", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Applies Accuracy Gate To Request Gps Fallback.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WithPoorRequestGps_ReturnsBadRequest()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.ClearPosition(_userId);

        var response = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(
                encounter.EncounterId,
                encounter.PlayerId,
                Latitude: encounter.Latitude,
                Longitude: encounter.Longitude,
                Accuracy: 125));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Improve GPS accuracy", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment When Gps Accuracy Is Poor Returns Bad Request.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WhenGpsAccuracyIsPoor_ReturnsBadRequest()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.UpdatePosition(_userId, new GeoPosition
        {
            Latitude = encounter.Latitude,
            Longitude = encounter.Longitude,
            Accuracy = 125,
            Timestamp = DateTime.UtcNow
        });

        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Improve GPS accuracy", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment When Rate Limit Exceeded Returns Too Many Requests.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_WhenRateLimitExceeded_ReturnsTooManyRequests()
    {
        await SetUserProfileAsync(level: 100, xp: 9900);
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);

        HttpResponseMessage lastResponse = new(HttpStatusCode.OK);
        for (var i = 0; i < 65; i++)
        {
            lastResponse = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        var body = await lastResponse.Content.ReadAsStringAsync();
        Assert.Contains("Rate limit exceeded", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Invalid Encounter Returns Failure.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_InvalidEncounter_ReturnsFailure()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(Guid.NewGuid(), 999999));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Starts Timed Session And Uses Local Players And Items.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_StartsTimedSession_UsesLocalPlayersAndItems()
    {
        var otherUserId = await _factory.CreateTestUserAsync($"local_{Guid.NewGuid():N}", "TestPass123!");
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);
        _factory.LocationTracker.SetPosition(otherUserId, encounter.Latitude, encounter.Longitude);

        var response = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId, RecruitmentItemKind.Chips));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.False(result.Completed);
        Assert.True(result.LocalPlayerCount >= 1);
        Assert.Equal(RecruitmentItemKind.Chips, result.ItemKind);
        Assert.Equal(encounter.BaseRecruitmentSeconds, result.BaseDurationSeconds);
        Assert.True(result.RequiredDurationSeconds < result.BaseDurationSeconds);
        Assert.True(result.RemainingSeconds > 0);
        Assert.Contains(result.Boosts!, boost => boost.Label.Contains("local recruiter", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Boosts!, boost => boost.Label.Contains("Chips", StringComparison.OrdinalIgnoreCase));

        await _factory.ExecuteInDbAsync(async db =>
        {
            var chips = await db.UserRecruitmentItems.SingleAsync(i => i.UserId == _userId && i.ItemKind == RecruitmentItemKind.Chips);
            Assert.Equal(2, chips.Quantity);
        });
    }

    /// <summary>
    /// Verifies attempt Recruitment Before Timer Finishes Returns In Progress.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_BeforeTimerFinishes_ReturnsInProgress()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        secondResponse.EnsureSuccessStatusCode();

        var result = await secondResponse.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(result.RemainingSeconds > 0);
        Assert.Contains("progress", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies attempt Recruitment Success Adds Collection Awards Experience And Removes Encounter.
    /// </summary>
    [Fact]
    public async Task AttemptRecruitment_Success_AddsCollection_AwardsExperience_AndRemovesEncounter()
    {
        await SetUserProfileAsync(level: 100, xp: 9900);

        var startXp = 9900;

        var successfulEncounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, successfulEncounter.Latitude, successfulEncounter.Longitude);
        var successResult = await RecruitToCompletionAsync(successfulEncounter);

        Assert.NotNull(successResult);
        Assert.NotNull(successResult!.Collection);

        await _factory.ExecuteInDbAsync(async db =>
        {
            var collected = await db.Collections.FirstOrDefaultAsync(c =>
                c.UserId == _userId && c.PlayerId == successfulEncounter!.PlayerId);
            Assert.NotNull(collected);

            var encounterStillExists = await db.PlayerEncounters.AnyAsync(e => e.Id == successfulEncounter!.EncounterId);
            Assert.False(encounterStillExists);

            var profile = await db.UserGameProfiles.FirstOrDefaultAsync(p => p.UserId == _userId);
            Assert.NotNull(profile);
            Assert.Equal(startXp + 25, profile!.Experience);
            Assert.Equal(100, profile.Level);
        });
    }

    /// <summary>
    /// Verifies get Profile Without Existing Record Returns Default Profile.
    /// </summary>
    [Fact]
    public async Task GetProfile_WithoutExistingRecord_ReturnsDefaultProfile()
    {
        var response = await _client.GetAsync("/api/recruitment/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<GameProgressDto>();
        Assert.NotNull(profile);
        Assert.Equal(1, profile!.Level);
        Assert.Equal(0, profile.Experience);
        Assert.Equal(100, profile.NextLevelExperience);
    }

    /// <summary>
    /// Verifies get Profile Returns Recruitment Items.
    /// </summary>
    [Fact]
    public async Task GetProfile_ReturnsStarterRecruitmentItems()
    {
        var response = await _client.GetAsync("/api/recruitment/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<RecruitmentProfileDto>();
        Assert.NotNull(profile);
        Assert.Contains(profile!.Items, i => i.ItemKind == RecruitmentItemKind.Chips && i.Quantity == 3);
        Assert.Contains(profile.Items, i => i.ItemKind == RecruitmentItemKind.Beer && i.Quantity == 2);
        Assert.Contains(profile.Items, i => i.ItemKind == RecruitmentItemKind.Whiskey && i.Quantity == 1);
        Assert.Null(profile.ActiveRecruitment);
    }

    /// <summary>
    /// Verifies get Profile Includes Active Recruitment Session.
    /// </summary>
    [Fact]
    public async Task GetProfile_WithActiveRecruitment_ReturnsSession()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);

        var startResponse = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId, RecruitmentItemKind.Chips));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var response = await _client.GetAsync("/api/recruitment/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<RecruitmentProfileDto>();
        Assert.NotNull(profile);
        Assert.NotNull(profile!.ActiveRecruitment);
        Assert.Equal(encounter.EncounterId, profile.ActiveRecruitment!.EncounterId);
        Assert.Equal(encounter.PlayerId, profile.ActiveRecruitment.PlayerId);
        Assert.Equal(RecruitmentItemKind.Chips, profile.ActiveRecruitment.ItemKind);
        Assert.True(profile.ActiveRecruitment.RemainingSeconds > 0);
    }

    /// <summary>
    /// Verifies get Profile With Existing Record Returns Calculated Next Level Experience.
    /// </summary>
    [Fact]
    public async Task GetProfile_WithExistingRecord_ReturnsCalculatedNextLevelExperience()
    {
        await SetUserProfileAsync(level: 7, xp: 650);

        var response = await _client.GetAsync("/api/recruitment/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<GameProgressDto>();
        Assert.NotNull(profile);
        Assert.Equal(7, profile!.Level);
        Assert.Equal(650, profile.Experience);
        Assert.Equal(700, profile.NextLevelExperience);
    }

    /// <summary>
    /// Verifies get Profile At Level Cap Returns Current Experience As Next Level Experience.
    /// </summary>
    [Fact]
    public async Task GetProfile_AtLevelCap_ReturnsCurrentExperienceAsNextLevelExperience()
    {
        await SetUserProfileAsync(level: 100, xp: 15000);

        var response = await _client.GetAsync("/api/recruitment/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<GameProgressDto>();
        Assert.NotNull(profile);
        Assert.Equal(100, profile!.Level);
        Assert.Equal(15000, profile.Experience);
        Assert.Equal(15000, profile.NextLevelExperience);
    }

    private async Task<(double lat, double lng)> GetAnyPlayerLocationAsync()
    {
        double lat = 0;
        double lng = 0;
        await _factory.ExecuteInDbAsync(db =>
        {
            var player = db.Players.First(p => p.SpawnLocation != null);
            lat = player.SpawnLocation!.Y;
            lng = player.SpawnLocation.X;
            return Task.CompletedTask;
        });

        return (lat, lng);
    }

    private async Task<PlayerEncounterDto> CreateEncounterAsync()
    {
        var anchor = await GetAnyPlayerLocationAsync();
        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        response.EnsureSuccessStatusCode();
        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.NotEmpty(encounters);
        return encounters![0];
    }

    private async Task SetUserProfileAsync(int level, int xp)
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            var profile = await db.UserGameProfiles.FirstOrDefaultAsync(p => p.UserId == _userId);
            if (profile is null)
            {
                db.UserGameProfiles.Add(new UserGameProfileModel
                {
                    UserId = _userId,
                    Level = level,
                    Experience = xp
                });
            }
            else
            {
                profile.Level = level;
                profile.Experience = xp;
            }

            await db.SaveChangesAsync();
        });
    }

    private async Task<RecruitmentAttemptResultDto?> RecruitToCompletionAsync(PlayerEncounterDto encounter)
    {
        var startResponse = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        startResponse.EnsureSuccessStatusCode();

        await _factory.ExecuteInDbAsync(async db =>
        {
            var entity = await db.PlayerEncounters.FirstAsync(e => e.Id == encounter.EncounterId);
            entity.RecruitmentCompletesAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        });

        var finishResponse = await _client.PostAsJsonAsync(
            "/api/recruitment/attempt",
            new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        finishResponse.EnsureSuccessStatusCode();

        return await finishResponse.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
    }

    private static int ExpectedBaseRecruitmentSeconds(string rarity) => rarity switch
    {
        "Common" => 30,
        "Uncommon" => 60,
        "Rare" => 150,
        "Epic" => 300,
        "Legendary" => 600,
        _ => 60
    };
}


