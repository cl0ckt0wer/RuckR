using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace RuckR.Tests.ComponentTests;

public class TestAuthorizationService : IAuthorizationService
{
    private readonly bool _allow;

    public TestAuthorizationService(bool allow = true)
    {
        _allow = allow;
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        return Task.FromResult(_allow
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        return Task.FromResult(_allow
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }
}
