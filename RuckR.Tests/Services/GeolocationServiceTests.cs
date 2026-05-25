using RuckR.Client.Services;
using RuckR.Client.Store.LocationFeature;

namespace RuckR.Tests.Services;

public class GeolocationServiceTests
{
    [Theory]
    [InlineData("granted", GeolocationPermissionStatus.Granted)]
    [InlineData("prompt", GeolocationPermissionStatus.Prompt)]
    [InlineData("denied", GeolocationPermissionStatus.Denied)]
    [InlineData("unavailable", GeolocationPermissionStatus.Unavailable)]
    [InlineData("something-else", GeolocationPermissionStatus.Unknown)]
    [InlineData(null, GeolocationPermissionStatus.Unknown)]
    public void NormalizePermissionState_MapsBrowserValues(string? browserValue, GeolocationPermissionStatus expected)
    {
        Assert.Equal(expected, GeolocationService.NormalizePermissionState(browserValue));
    }

    [Theory]
    [InlineData(1, GeolocationPermissionStatus.Denied)]
    [InlineData(0, GeolocationPermissionStatus.Unavailable)]
    [InlineData(2, GeolocationPermissionStatus.Unknown)]
    [InlineData(3, GeolocationPermissionStatus.Unknown)]
    public void PermissionStatusFromErrorCode_MapsBrowserCodes(int code, GeolocationPermissionStatus expected)
    {
        Assert.Equal(expected, GeolocationService.PermissionStatusFromErrorCode(code));
    }

    [Fact]
    public void BuildLocationErrorMessage_ForPermissionDenied_IsActionable()
    {
        var message = GeolocationService.BuildLocationErrorMessage(1, "User denied Geolocation");

        Assert.Contains("Location permission is off", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("site settings", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry GPS", message, StringComparison.OrdinalIgnoreCase);
    }
}
