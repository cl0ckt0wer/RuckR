using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace RuckR.Client.Services;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private bool _initialized;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public CookieAuthenticationStateProvider(HttpClient http)
    {
        _http = http;
    }

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

    public void NotifyAuthenticationStateChanged()
    {
        _initialized = false;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
