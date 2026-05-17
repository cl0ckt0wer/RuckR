using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace RuckR.Tests.ComponentTests;

    /// <summary>
    /// Provides access to :.
    /// </summary>
public class TestAuthorizationService : IAuthorizationService
{
    private readonly bool _allow;

    /// <summary>
    /// Initializes a new instance of the <see cref="""TestAuthorizationService"""/> class.
    /// </summary>
    /// <param name="allow">The allow to use.</param>
    public TestAuthorizationService(bool allow = true)
    {
        _allow = allow;
    }

    /// <summary>
    /// Verifies authorize Async.
    /// </summary>
    /// <returns>A value indicating the result of this operation.</returns>
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        return Task.FromResult(_allow
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }

    /// <summary>
    /// Verifies authorize Async.
    /// </summary>
    /// <param name="user">The user to use.</param>
    /// <param name="resource">The resource to use.</param>
    /// <param name="policyName">The policyName to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        return Task.FromResult(_allow
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
    }
}


