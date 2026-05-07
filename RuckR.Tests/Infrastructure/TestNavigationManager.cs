using Microsoft.AspNetCore.Components;

namespace RuckR.Tests.Infrastructure;

/// <summary>
/// Minimal NavigationManager for DI validation tests that resolves
/// <see cref="SignalRClientService"/> without requiring a full Blazor host.
/// </summary>
public class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }
}
