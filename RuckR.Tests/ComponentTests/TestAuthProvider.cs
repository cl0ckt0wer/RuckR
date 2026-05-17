using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class TestAuthProvider : AuthenticationStateProvider
{
    private readonly bool _authenticated;
    private readonly string _username;

    /// <summary>
    /// Initializes a new instance of the <see cref="""TestAuthProvider"""/> class.
    /// </summary>
    /// <param name="authenticated">The authenticated to use.</param>
    /// <param name="username">The username to use.</param>
    public TestAuthProvider(bool authenticated, string? username = null)
    {
        _authenticated = authenticated;
        _username = username ?? "TestUser";
    }

    /// <summary>
    /// Verifies get Authentication State Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_authenticated)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, _username)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }

    /// <summary>
    /// Simulate a state change to trigger re-renders.
    /// </summary>
    public void ChangeAuthenticationState(bool authenticated, string? username = null)
    {
        if (!authenticated)
        {
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        }
        else
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, username ?? "TestUser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
        }
    }
}


