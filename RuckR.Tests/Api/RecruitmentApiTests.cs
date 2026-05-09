using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
public class RecruitmentApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _username = null!;

    public RecruitmentApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _username = $"recruit_{Guid.NewGuid():N}";
        _userId = await _factory.CreateTestUserAsync(_username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(_userId, _username);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetEncounters_ReturnsChanceBasedOnPlayerAndUserLevel()
    {
        await _factory.ExecuteInDbAsync(async db =>
        {
            var profile = await db.UserGameProfiles.FirstOrDefaultAsync(p => p.UserId == _userId);
            if (profile is null)
            {
                db.UserGameProfiles.Add(new UserGameProfileModel
                {
                    UserId = _userId,
                    Level = 20,
                    Experience = 1900
                });
            }
            else
            {
                profile.Level = 20;
                profile.Experience = 1900;
            }
            await db.SaveChangesAsync();
        });

        var anchor = await GetAnyPlayerLocationAsync();
        _factory.LocationTracker.SetPosition(_userId, anchor.lat, anchor.lng);

        var response = await _client.GetAsync($"/api/map/encounters?lat={anchor.lat}&lng={anchor.lng}&radius=1000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var encounters = await response.Content.ReadFromJsonAsync<List<PlayerEncounterDto>>();
        Assert.NotNull(encounters);
        Assert.NotEmpty(encounters);

        var first = encounters![0];
        var expected = ExpectedChance(20, first.Level, first.Rarity);
        Assert.Equal(expected, first.SuccessChancePercent);
    }

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

    [Fact]
    public async Task AttemptRecruitment_DuplicateRecruit_ReturnsFailure()
    {
        await SetUserProfileAsync(level: 100, xp: 9900);

        RecruitmentAttemptResultDto? successResult = null;
        PlayerEncounterDto? encounter = null;

        for (var i = 0; i < 20 && successResult is null; i++)
        {
            encounter = await CreateEncounterAsync();
            _factory.LocationTracker.SetPosition(_userId, encounter.Latitude, encounter.Longitude);

            var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
            if (result?.Success == true)
            {
                successResult = result;
            }
        }

        Assert.NotNull(encounter);
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

    [Fact]
    public async Task AttemptRecruitment_TooFar_ReturnsFailure()
    {
        var encounter = await CreateEncounterAsync();
        _factory.LocationTracker.SetPosition(_userId, encounter.Latitude + 0.01, encounter.Longitude + 0.01);

        var response = await _client.PostAsJsonAsync("/api/recruitment/attempt", new RecruitmentAttemptRequest(encounter.EncounterId, encounter.PlayerId));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Move closer", result.Message, StringComparison.OrdinalIgnoreCase);
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

    private static int ExpectedChance(int userLevel, int playerLevel, string rarity)
    {
        var rarityModifier = rarity switch
        {
            "Common" => 10,
            "Uncommon" => 0,
            "Rare" => -10,
            "Epic" => -20,
            "Legendary" => -30,
            _ => 0
        };

        var chance = 65 + ((userLevel - playerLevel) * 3) + rarityModifier;
        return Math.Clamp(chance, 5, 95);
    }
}
