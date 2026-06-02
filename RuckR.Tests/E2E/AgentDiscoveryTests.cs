using System.Net;
using RuckR.Tests.Fixtures;

namespace RuckR.Tests.E2E;

[Collection(nameof(TestCollection))]
public sealed class AgentDiscoveryTests : IDisposable
{
    private readonly HttpClient _client;

    public AgentDiscoveryTests(CustomWebApplicationFactory factory)
    {
        _client = new HttpClient { BaseAddress = new Uri(factory.ServerBaseUrl) };
    }

    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/sitemap.xml")]
    [InlineData("/llms.txt")]
    [InlineData("/agent-info")]
    public async Task PublicDiscoveryResources_ReturnOk(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AgentInfo_ReturnsPlainHtml_NotBlazorShell()
    {
        var response = await _client.GetAsync("/agent-info");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<h1>RuckR Agent Information</h1>", body);
        Assert.Contains("This page is informational only.", body);
        Assert.DoesNotContain("_framework/blazor.webassembly.js", body);
        Assert.DoesNotContain("loading-progress", body);
    }

    [Fact]
    public async Task RobotsTxt_DefinesAiCrawlerPolicyAndSensitivePathBlocks()
    {
        var robots = await _client.GetStringAsync("/robots.txt");

        Assert.Contains("User-agent: GPTBot", robots);
        Assert.Contains("Disallow: /", robots);
        Assert.Contains("User-agent: OAI-SearchBot", robots);
        Assert.Contains("User-agent: ChatGPT-User", robots);
        Assert.Contains("Allow: /", robots);
        Assert.Contains("Disallow: /api/", robots);
        Assert.Contains("Disallow: /Identity/", robots);
        Assert.Contains("Disallow: /jaeger/", robots);
        Assert.Contains("Disallow: /debug-map", robots);
        Assert.Contains("Disallow: /debug-layers", robots);
        Assert.Contains("Sitemap: https://ruckr.exe.xyz/sitemap.xml", robots);
    }

    [Fact]
    public async Task SitemapXml_ListsCanonicalPublicUrls()
    {
        var sitemap = await _client.GetStringAsync("/sitemap.xml");

        Assert.Contains("http://www.sitemaps.org/schemas/sitemap/0.9", sitemap);
        Assert.Contains("<loc>https://ruckr.exe.xyz/</loc>", sitemap);
        Assert.Contains("<loc>https://ruckr.exe.xyz/agent-info</loc>", sitemap);
    }

    [Fact]
    public async Task LlmsTxt_ExplainsPublicBoundariesAndNoActionApi()
    {
        var llms = await _client.GetStringAsync("/llms.txt");

        Assert.Contains("# RuckR", llms);
        Assert.Contains("https://ruckr.exe.xyz/agent-info", llms);
        Assert.Contains("Most gameplay pages require an authenticated user session.", llms);
        Assert.Contains("V1 does not expose an agent action API", llms);
        Assert.Contains("GPS", llms);
        Assert.Contains("recruitment", llms);
        Assert.Contains("battle", llms);
        Assert.Contains("profile", llms);
        Assert.Contains("account actions", llms);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
