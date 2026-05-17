using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RuckR.Client.Pages;
using RuckR.Client.Services;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class PitchCreateGpsTests : TestContext
{
    /// <summary>
    /// Verifies pitch Create Disables Submit And Shows Error When Gps Unavailable.
    /// </summary>
    [Fact]
    public void PitchCreate_DisablesSubmitAndShowsError_WhenGpsUnavailable()
    {
        Services.AddSingleton<IGeolocationService>(new FakeGeolocationService(null));
        Services.AddSingleton(new ApiClientService(new HttpClient { BaseAddress = new Uri("https://example.test/") }, NullLogger<ApiClientService>.Instance));

        var cut = Render<PitchCreate>();

        cut.WaitForAssertion(() =>
        {
            var submit = cut.Find("[data-testid='pitch-submit']");
            Assert.True(submit.HasAttribute("disabled"));
            Assert.Contains("Only GPS-enabled users can create pitches", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='pitch-latitude']"));
            Assert.Empty(cut.FindAll("[data-testid='pitch-longitude']"));
        });
    }

    /// <summary>
    /// Verifies pitch Create Enables Submit And Shows Read Only Location When Gps Available.
    /// </summary>
    [Fact]
    public void PitchCreate_EnablesSubmitAndShowsReadOnlyLocation_WhenGpsAvailable()
    {
        Services.AddSingleton<IGeolocationService>(new FakeGeolocationService(new GeoPosition
        {
            Latitude = 51.4564,
            Longitude = -0.3416,
            Accuracy = 12,
            Timestamp = DateTime.UtcNow
        }));
        Services.AddSingleton(new ApiClientService(new HttpClient { BaseAddress = new Uri("https://example.test/") }, NullLogger<ApiClientService>.Instance));

        var cut = Render<PitchCreate>();

        cut.WaitForAssertion(() =>
        {
            var submit = cut.Find("[data-testid='pitch-submit']");
            Assert.False(submit.HasAttribute("disabled"));
            Assert.NotNull(cut.Find("[data-testid='gps-indicator']"));
            Assert.Contains("Latitude 51.45640", cut.Markup);
            Assert.Contains("Longitude -0.34160", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='pitch-latitude']"));
            Assert.Empty(cut.FindAll("[data-testid='pitch-longitude']"));
        });
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        private readonly GeoPosition? _position;

    /// <summary>
    /// Verifies fake Geolocation Service.
    /// </summary>
    /// <param name="position">The position to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
        public FakeGeolocationService(GeoPosition? position)
        {
            _position = position;
        }

    /// <summary>
    /// Provides access to position Changed.
    /// </summary>
        public event Action<GeoPosition>? PositionChanged;

    /// <summary>
    /// Verifies get Current Position Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
        public Task<GeoPosition?> GetCurrentPositionAsync() => Task.FromResult(_position);

    /// <summary>
    /// Verifies start Watch Async.
    /// </summary>
        public Task StartWatchAsync()
        {
            if (_position is not null)
            {
                PositionChanged?.Invoke(_position);
            }

            return Task.CompletedTask;
        }

    /// <summary>
    /// Verifies stop Watch.
    /// </summary>
        public void StopWatch()
        {
        }

    /// <summary>
    /// Verifies dispose Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}


