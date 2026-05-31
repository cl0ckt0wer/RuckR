namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Verifies the production service worker keeps caching limited to versioned static assets.
/// </summary>
public class ServiceWorkerCachePolicyTests
{
    [Fact]
    public void IndexHtml_RegistersServiceWorker_WithoutClearingRegistrations()
    {
        var indexHtml = ReadClientWwwrootFile("index.html");

        Assert.Contains("navigator.serviceWorker.register('service-worker.js')", indexHtml);
        Assert.DoesNotContain("getRegistrations()", indexHtml);
        Assert.DoesNotContain(".unregister()", indexHtml);
    }

    [Fact]
    public void PublishedServiceWorker_CachesVersionedFrameworkContentCssAndImages()
    {
        var worker = ReadClientWwwrootFile("service-worker.published.js");

        Assert.Contains("self.importScripts('./service-worker-assets.js')", worker);
        Assert.Contains("self.assetsManifest.version", worker);
        Assert.Contains("'/_framework/'", worker);
        Assert.Contains("'/_content/'", worker);
        Assert.Contains("'/css/'", worker);
        Assert.Contains("'/images/'", worker);
        Assert.Contains("cache.addAll", worker);
        Assert.Contains("caches.match", worker);
    }

    [Fact]
    public void PublishedServiceWorker_ExcludesShellConfigBootAuthApiAndQueryUrls()
    {
        var worker = ReadClientWwwrootFile("service-worker.published.js");

        Assert.Contains("index.html", worker);
        Assert.Contains("appsettings", worker);
        Assert.Contains("service-worker-assets.js", worker);
        Assert.Contains("blazor.boot.json", worker);
        Assert.Contains("'/api/'", worker);
        Assert.Contains("'/identity/'", worker);
        Assert.Contains("url.search", worker);
        Assert.Contains("method !== 'GET'", worker);
    }

    [Fact]
    public void ProductionAppSettings_DisablesFullMapDiagnosticsByDefault()
    {
        var appSettings = ReadClientWwwrootFile("appsettings.json");

        Assert.Contains("\"EnableMapDiagnostics\": false", appSettings);
        Assert.Contains("\"EnableMapPerformanceSummary\": true", appSettings);
    }

    private static string ReadClientWwwrootFile(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "RuckR",
            "Client",
            "wwwroot",
            relativePath));

        return File.ReadAllText(path);
    }
}
