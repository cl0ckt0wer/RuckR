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
    private string _username = null!;

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
        _username = $"profileapi_{Guid.NewGuid():N}@test.com";
        var userId = await _factory.CreateTestUserAsync(_username, "TestPass123!");
        _client = _factory.CreateAuthenticatedClient(userId, _username);
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
        Assert.NotEqual(_username, profile!.Name);
    }

    /// <summary>
    /// Verifies put Profile Updates Current Profile.
    /// </summary>
    [Fact]
    public async Task PutProfile_UpdatesCurrentProfile()
    {
        var updated = new UserProfileUpdateRequest
        {
            Name = "Test Scrum Half",
            Biography = "Runs support lines and logs test coverage.",
            Location = "Twickenham",
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

    /// <summary>
    /// Verifies profile updates require a public display name.
    /// </summary>
    [Fact]
    public async Task PutProfile_WithoutDisplayName_ReturnsBadRequest()
    {
        var updateResponse = await _client.PutAsJsonAsync("/api/profile", new UserProfileUpdateRequest
        {
            Name = "",
            Biography = "No display name",
            AvatarUrl = "https://example.com/avatar.png"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }
}


