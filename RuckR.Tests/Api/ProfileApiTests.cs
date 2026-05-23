using System.Net.Http.Json;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.Api;

    /// <summary>
    /// Provides access to :.
    /// </summary>
[Collection(nameof(TestCollection))]
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

    /// <summary>
    /// Verifies get Profile Returns Current Profile.
    /// </summary>
    [Fact]
    public async Task GetProfile_ReturnsCurrentProfile()
    {
        var profile = await _client.GetFromJsonAsync<UserProfileModel>("/api/profile");

        Assert.NotNull(profile);
        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
    }

    /// <summary>
    /// Verifies put Profile Updates Current Profile.
    /// </summary>
    [Fact]
    public async Task PutProfile_UpdatesCurrentProfile()
    {
        var updated = new UserProfileModel
        {
            UserId = "client-value-is-overwritten-by-controller",
            Name = "Test Scrum Half",
            Biography = "Runs support lines and logs test coverage.",
            Location = "Twickenham",
            JoinedDate = DateTime.UtcNow.Date,
            AvatarUrl = "https://example.com/avatar.png"
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/profile", updated);
        updateResponse.EnsureSuccessStatusCode();

        var profile = await _client.GetFromJsonAsync<UserProfileModel>("/api/profile");

        Assert.NotNull(profile);
        Assert.Equal(updated.Name, profile.Name);
        Assert.Equal(updated.Biography, profile.Biography);
        Assert.Equal(updated.Location, profile.Location);
        Assert.Equal(updated.AvatarUrl, profile.AvatarUrl);
    }
}


