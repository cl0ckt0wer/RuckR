using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using RuckR.Client.Pages;
using RuckR.Client.Services;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// UI tests for profile image upload.
/// </summary>
public class ProfilePageTests : TestContext
{
    private static readonly byte[] ProcessedWebpBytes =
    [
        0x52, 0x49, 0x46, 0x46,
        0x0A, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50,
        0x56, 0x50, 0x38, 0x20
    ];

    private readonly ProfileHttpHandler _handler = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilePageTests"/> class.
    /// </summary>
    public ProfilePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/profile-avatar.module.js");
        module.Setup<ProcessedProfileAvatar>("resizeSelectedProfileAvatar", _ => true)
            .SetResult(new ProcessedProfileAvatar
            {
                DataUrl = $"data:image/webp;base64,{Convert.ToBase64String(ProcessedWebpBytes)}",
                ContentType = "image/webp",
                FileName = "avatar.webp",
                OriginalSize = ValidPngBytes().Length,
                OutputSize = ProcessedWebpBytes.Length,
                OriginalWidth = 1600,
                OriginalHeight = 1200,
                Width = 768,
                Height = 576,
                WasResized = true
            });
        Services.AddMudServices();
        Services.AddSingleton(new ApiClientService(
            new HttpClient(_handler) { BaseAddress = new Uri("https://example.test/") },
            NullLogger<ApiClientService>.Instance));
    }

    /// <summary>
    /// Verifies profile edit mode exposes the upload control.
    /// </summary>
    [Fact]
    public void ProfilePage_EditMode_ShowsAvatarUploadControl()
    {
        var cut = Render<Profile>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='profile-edit-btn']")));
        cut.Find("[data-testid='profile-edit-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='profile-avatar-upload-panel']"));
            Assert.NotNull(cut.Find("[data-testid='profile-avatar-file-input']"));
            Assert.Empty(cut.FindAll("[data-testid='profile-avatar-url-input']"));
        });
    }

    /// <summary>
    /// Verifies a successful avatar upload refreshes the visible preview.
    /// </summary>
    [Fact]
    public void ProfilePage_AvatarUpload_RefreshesDisplayedAvatar()
    {
        var cut = Render<Profile>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='profile-edit-btn']").Click());
        var input = cut.FindComponent<InputFile>();
        input.UploadFiles(InputFileContent.CreateFromBinary(ValidPngBytes(), "avatar.png", contentType: "image/png"));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='profile-avatar-upload-btn']"));
            Assert.Equal($"data:image/webp;base64,{Convert.ToBase64String(ProcessedWebpBytes)}", cut.Find("[data-testid='profile-avatar-preview']").GetAttribute("src"));
            Assert.Contains("768 x 576", cut.Find("[data-testid='profile-avatar-processing-summary']").TextContent);
        });

        cut.Find("[data-testid='profile-avatar-upload-btn']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(_handler.SawAvatarUpload);
            Assert.Contains("filename=avatar.webp", _handler.LastAvatarUploadBody);
            Assert.Contains("Content-Type: image/webp", _handler.LastAvatarUploadBody);
            Assert.Equal("/uploads/profile-images/test/avatar.png", cut.Find("[data-testid='profile-avatar-preview']").GetAttribute("src"));
            Assert.Contains("Profile picture uploaded.", cut.Find("[data-testid='profile-save-message']").TextContent);
        });
    }

    private static byte[] ValidPngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52
    ];

    private sealed class ProfileHttpHandler : HttpMessageHandler
    {
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private UserProfileModel _profile = new()
        {
            UserId = "test-user",
            Name = "Test Player",
            JoinedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        public bool SawAvatarUpload { get; private set; }
        public string LastAvatarUploadBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Get && path == "/api/profile")
                return await JsonResponse(_profile);

            if (request.Method == HttpMethod.Post && path == "/api/profile/avatar")
            {
                SawAvatarUpload = true;
                LastAvatarUploadBody = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                _profile = new UserProfileModel
                {
                    UserId = _profile.UserId,
                    Name = _profile.Name,
                    Biography = _profile.Biography,
                    Location = _profile.Location,
                    AvatarUrl = "/uploads/profile-images/test/avatar.png",
                    JoinedDate = _profile.JoinedDate
                };

                return await JsonResponse(_profile);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            };
        }

        private Task<HttpResponseMessage> JsonResponse(UserProfileModel profile)
        {
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
