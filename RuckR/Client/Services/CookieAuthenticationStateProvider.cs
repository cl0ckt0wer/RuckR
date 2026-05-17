using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace RuckR.Client.Services;

/// <summary>
/// AuthenticationStateProvider implementation that checks the server cookie-backed identity endpoint.
/// </summary>
public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private bool _initialized;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    /// <summary>
    /// Creates a new <see cref="CookieAuthenticationStateProvider"/>.
    /// </summary>
    /// <param name="http">HTTP client used to call user identity endpoint.</param>
    public CookieAuthenticationStateProvider(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Resolves the current authentication state from the server cookie.
    /// </summary>
    /// <returns>The current principal wrapped in <see cref="AuthenticationState"/>.</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            try
            {
                // Call a lightweight auth check endpoint on the server
                var response = await _http.GetAsync("Identity/Account/UserInfo");
                if (response.IsSuccessStatusCode)
                {
                    var username = await response.Content.ReadFromJsonAsync<string>();
                    if (!string.IsNullOrEmpty(username))
                    {
                        var identity = new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.Name, username),
                        }, "cookie");
                        _currentUser = new ClaimsPrincipal(identity);
                    }
                }
            }
            catch
            {
                // Not authenticated or server unreachable
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            }
            _initialized = true;
        }

        return new AuthenticationState(_currentUser);
    }

    /// <summary>
    /// Clears cached state and notifies subscribers that authentication state should be recalculated.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        _initialized = false;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
