using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace RuckR.Tests.ComponentTests;

public class TestAuthProvider : AuthenticationStateProvider
{
    private readonly bool _authenticated;
    private readonly string _username;

    public TestAuthProvider(bool authenticated, string? username = null)
    {
        _authenticated = authenticated;
        _username = username ?? "TestUser";
    }

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
