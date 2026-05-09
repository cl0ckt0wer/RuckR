using System.Net;
using System.Text;
using System.Text.Json;
using RuckR.Client.Services;

namespace RuckR.Tests.ComponentTests;

public class CookieAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationStateAsync_WithEmptyJsonString_ReturnsAnonymousUser()
    {
        var provider = CreateProvider(string.Empty);

        var authState = await provider.GetAuthenticationStateAsync();

        Assert.False(authState.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WithUsernameJsonString_ReturnsAuthenticatedUser()
    {
        var provider = CreateProvider("test@example.com");

        var authState = await provider.GetAuthenticationStateAsync();

        Assert.True(authState.User.Identity?.IsAuthenticated);
        Assert.Equal("test@example.com", authState.User.Identity?.Name);
    }

    private static CookieAuthenticationStateProvider CreateProvider(string username)
    {
        var handler = new StubHttpMessageHandler(JsonSerializer.Serialize(username));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        return new CookieAuthenticationStateProvider(httpClient);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;

        public StubHttpMessageHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
