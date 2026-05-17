using Fluxor;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using RuckR.Client.Services;
using RuckR.Client.Store.LocationFeature;
using RuckR.Shared.Models;
using Xunit;

namespace RuckR.Tests.Unit;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public class GeolocationServiceTests
{
    private readonly Mock<IJSRuntime> _js = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<ILogger<GeolocationService>> _logger = new();

    private GeolocationService CreateService() => new(_js.Object, _dispatcher.Object, _logger.Object);

    private static GeolocationResult Pos(double lat, double lng, double accuracy = 10) => new()
    {
        Coords = new GeolocationCoords
        {
            Latitude = lat,
            Longitude = lng,
            Accuracy = accuracy
        },
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    /// <summary>
    /// Verifies on Position From Js First Position Dispatches Update And Raises Event.
    /// </summary>
    [Fact]
    public void OnPositionFromJs_FirstPosition_DispatchesUpdateAndRaisesEvent()
    {
        var service = CreateService();
        GeoPosition? changed = null;
        service.PositionChanged += p => changed = p;

        service.OnPositionFromJs(Pos(51.5000, -0.1200, 15));

        _dispatcher.Verify(d => d.Dispatch(It.Is<UpdatePositionAction>(a =>
            a.Latitude == 51.5000 && a.Longitude == -0.1200 && a.Accuracy == 15)), Times.Once);
        Assert.NotNull(changed);
    }

    /// <summary>
    /// Verifies on Position From Js Null Island Is Discarded.
    /// </summary>
    [Fact]
    public void OnPositionFromJs_NullIsland_IsDiscarded()
    {
        var service = CreateService();

        service.OnPositionFromJs(Pos(0.0001, -0.0002, 20));

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdatePositionAction>()), Times.Never);
    }

    /// <summary>
    /// Verifies on Position From Js Poor Accuracy After First Fix Is Discarded.
    /// </summary>
    [Fact]
    public void OnPositionFromJs_PoorAccuracyAfterFirstFix_IsDiscarded()
    {
        var service = CreateService();

        service.OnPositionFromJs(Pos(51.5000, -0.1200, 10));
        service.OnPositionFromJs(Pos(51.5010, -0.1210, 999));

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdatePositionAction>()), Times.Once);
    }

    /// <summary>
    /// Verifies on Position From Js Too Small Displacement Is Discarded.
    /// </summary>
    [Fact]
    public void OnPositionFromJs_TooSmallDisplacement_IsDiscarded()
    {
        var service = CreateService();

        service.OnPositionFromJs(Pos(51.500000, -0.120000, 5));
        // ~1-2m movement, below 20m threshold
        service.OnPositionFromJs(Pos(51.500010, -0.120010, 5));

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdatePositionAction>()), Times.Once);
    }

    /// <summary>
    /// Verifies on Position From Js Throttle Drops Rapid Second Update.
    /// </summary>
    [Fact]
    public void OnPositionFromJs_Throttle_DropsRapidSecondUpdate()
    {
        var service = CreateService();

        service.OnPositionFromJs(Pos(51.5000, -0.1200, 10));
        service.OnPositionFromJs(Pos(51.8000, -0.3000, 10)); // within throttle window

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdatePositionAction>()), Times.Once);
    }

    /// <summary>
    /// Verifies on Error From Js Dispatches Location Error Action.
    /// </summary>
    [Fact]
    public void OnErrorFromJs_DispatchesLocationErrorAction()
    {
        var service = CreateService();

        service.OnErrorFromJs(1, "Permission denied");

        _dispatcher.Verify(d => d.Dispatch(It.Is<LocationErrorAction>(a =>
            a.ErrorMessage.Contains("Geolocation error 1", StringComparison.Ordinal))), Times.Once);
    }
}


