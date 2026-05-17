using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

[Collection(nameof(TestCollection))]
    /// <summary>
    /// Provides access to :.
    /// </summary>
public class ProfileApiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="""ProfileApiTests"""/> class.
    /// </summary>
    /// <param name="factory">The factory to use.</param>
    public ProfileApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies initialize Async.
    /// </summary>
    public async Task InitializeAsync()
    {
        var username = $"profileapi_{Guid.NewGuid():N}";
        var userId = await _factory.CreateTestUserAsync(username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(userId, username);
    }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    /// <summary>
    /// Verifies get Profile Returns Current Profile.
    /// </summary>
    public async Task GetProfile_ReturnsCurrentProfile()
    {
        var profile = await _client.GetFromJsonAsync<ProfileModel>("/api/profile");

        Assert.NotNull(profile);
        Assert.False(string.IsNullOrWhiteSpace(profile.Email));
    }

    [Fact]
    /// <summary>
    /// Verifies put Profile Updates Current Profile.
    /// </summary>
    public async Task PutProfile_UpdatesCurrentProfile()
    {
        var updated = new ProfileModel
        {
            Name = "Test Scrum Half",
            Biography = "Runs support lines and logs test coverage.",
            Email = $"profile_{Guid.NewGuid():N}@test.com",
            Location = "Twickenham",
            JoinedDate = DateTime.UtcNow.Date,
            AvatarUrl = "https://example.com/avatar.png"
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/profile", updated);
        updateResponse.EnsureSuccessStatusCode();

        var profile = await _client.GetFromJsonAsync<ProfileModel>("/api/profile");

        Assert.NotNull(profile);
        Assert.Equal(updated.Name, profile.Name);
        Assert.Equal(updated.Biography, profile.Biography);
        Assert.Equal(updated.Email, profile.Email);
        Assert.Equal(updated.Location, profile.Location);
        Assert.Equal(updated.AvatarUrl, profile.AvatarUrl);
    }
}


