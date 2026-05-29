using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
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

    /// <summary>
    /// Verifies avatar upload stores a public image and updates the profile.
    /// </summary>
    [Fact]
    public async Task UploadAvatar_ValidPng_SavesProfileAndServesStaticFile()
    {
        var response = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(ValidPngBytes(), "avatar.png", "image/png"));
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<UserProfileModel>();

        Assert.NotNull(profile);
        Assert.StartsWith("/uploads/profile-images/", profile!.AvatarUrl);
        Assert.EndsWith(".png", profile.AvatarUrl);

        var savedProfile = await _client.GetFromJsonAsync<UserProfileModel>("/api/profile");
        Assert.Equal(profile.AvatarUrl, savedProfile!.AvatarUrl);

        var imageResponse = await _client.GetAsync(profile.AvatarUrl);
        imageResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ValidPngBytes(), await imageResponse.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// Verifies all supported upload types are accepted.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValidAvatarUploads))]
    public async Task UploadAvatar_SupportedImageTypes_SaveAvatar(byte[] bytes, string fileName, string contentType, string expectedExtension)
    {
        var response = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(bytes, fileName, contentType));
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<UserProfileModel>();

        Assert.NotNull(profile);
        Assert.EndsWith(expectedExtension, profile!.AvatarUrl);
    }

    /// <summary>
    /// Verifies unauthenticated users cannot upload avatars.
    /// </summary>
    [Fact]
    public async Task UploadAvatar_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/profile/avatar", CreateAvatarForm(ValidPngBytes(), "avatar.png", "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies invalid avatar uploads are rejected.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidAvatarUploads))]
    public async Task UploadAvatar_InvalidFiles_ReturnBadRequest(byte[] bytes, string fileName, string contentType)
    {
        var response = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(bytes, fileName, contentType));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies reuploading replaces the public URL and removes the old local file.
    /// </summary>
    [Fact]
    public async Task UploadAvatar_Reupload_RemovesPreviousLocalFile()
    {
        var firstResponse = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(ValidPngBytes(), "avatar.png", "image/png"));
        firstResponse.EnsureSuccessStatusCode();
        var firstProfile = await firstResponse.Content.ReadFromJsonAsync<UserProfileModel>();

        var secondResponse = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(ValidJpegBytes(), "avatar.jpg", "image/jpeg"));
        secondResponse.EnsureSuccessStatusCode();
        var secondProfile = await secondResponse.Content.ReadFromJsonAsync<UserProfileModel>();

        Assert.NotNull(firstProfile);
        Assert.NotNull(secondProfile);
        Assert.NotEqual(firstProfile!.AvatarUrl, secondProfile!.AvatarUrl);
        Assert.EndsWith(".jpg", secondProfile.AvatarUrl);

        var oldImageResponse = await _client.GetAsync(firstProfile.AvatarUrl);
        Assert.Equal(HttpStatusCode.NotFound, oldImageResponse.StatusCode);
    }

    /// <summary>
    /// Verifies later profile saves keep uploaded local avatar paths valid.
    /// </summary>
    [Fact]
    public async Task PutProfile_WithUploadedAvatarPath_SavesProfile()
    {
        var uploadResponse = await _client.PostAsync("/api/profile/avatar", CreateAvatarForm(ValidPngBytes(), "avatar.png", "image/png"));
        uploadResponse.EnsureSuccessStatusCode();
        var uploadedProfile = await uploadResponse.Content.ReadFromJsonAsync<UserProfileModel>();

        var updateResponse = await _client.PutAsJsonAsync("/api/profile", new UserProfileUpdateRequest
        {
            Name = "Uploaded Avatar Player",
            Biography = "Still editing after upload.",
            Location = "Local pitch",
            AvatarUrl = uploadedProfile!.AvatarUrl
        });

        updateResponse.EnsureSuccessStatusCode();
        var saved = await updateResponse.Content.ReadFromJsonAsync<UserProfileModel>();

        Assert.Equal(uploadedProfile.AvatarUrl, saved!.AvatarUrl);
        Assert.Equal("Uploaded Avatar Player", saved.Name);
    }

    /// <summary>
    /// Provides invalid avatar upload examples.
    /// </summary>
    public static IEnumerable<object[]> InvalidAvatarUploads()
    {
        yield return new object[] { Array.Empty<byte>(), "empty.png", "image/png" };
        yield return new object[] { Enumerable.Repeat((byte)0x89, 2 * 1024 * 1024 + 1).ToArray(), "huge.png", "image/png" };
        yield return new object[] { ValidPngBytes(), "avatar.gif", "image/gif" };
        yield return new object[] { "not really a png"u8.ToArray(), "avatar.png", "image/png" };
        yield return new object[] { ValidPngBytes(), "avatar.jpg", "image/jpeg" };
    }

    /// <summary>
    /// Provides valid avatar upload examples.
    /// </summary>
    public static IEnumerable<object[]> ValidAvatarUploads()
    {
        yield return new object[] { ValidJpegBytes(), "avatar.jpeg", "image/jpeg", ".jpg" };
        yield return new object[] { ValidPngBytes(), "avatar.png", "image/png", ".png" };
        yield return new object[] { ValidWebpBytes(), "avatar.webp", "image/webp", ".webp" };
    }

    private static MultipartFormDataContent CreateAvatarForm(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    private static byte[] ValidPngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52
    ];

    private static byte[] ValidJpegBytes() =>
    [
        0xFF, 0xD8, 0xFF, 0xE0,
        0x00, 0x10, 0x4A, 0x46,
        0x49, 0x46, 0x00, 0x01
    ];

    private static byte[] ValidWebpBytes() =>
    [
        0x52, 0x49, 0x46, 0x46,
        0x0A, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50,
        0x56, 0x50, 0x38, 0x20
    ];
}


