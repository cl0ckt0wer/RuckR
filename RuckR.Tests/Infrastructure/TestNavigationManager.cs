using Microsoft.AspNetCore.Components;

namespace RuckR.Tests.Infrastructure;

/// <summary>
/// Minimal NavigationManager for DI validation tests that resolves
/// <see cref="SignalRClientService"/> without requiring a full Blazor host.
/// </summary>
public class TestNavigationManager : NavigationManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="""TestNavigationManager"""/> class.
    /// </summary>
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }
}


