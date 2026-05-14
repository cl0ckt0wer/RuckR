using dymaptic.GeoBlazor.Core;
using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RuckR.Client;
using RuckR.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ── Logging ──
// Built-in WebAssemblyConsoleLoggerProvider maps to browser console.log automatically.
// No AddConsole() — throws PlatformNotSupportedException in .NET 10 WASM.
// TelemetryLoggerProvider (below) ships Warning+ logs to the server.
builder.Logging.SetMinimumLevel(builder.HostEnvironment.IsDevelopment() 
    ? LogLevel.Trace 
    : LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
builder.Logging.AddFilter("RuckR.Client.Services.GeolocationService", LogLevel.Debug);
builder.Logging.AddFilter("RuckR.Client.Pages.PitchCreate", LogLevel.Debug);

// Telemetry bridge — ships Warning+ client logs to server (WASM-safe)
builder.Services.AddSingleton(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    return new TelemetryLoggerProvider(http);
});
builder.Services.AddLogging(logging =>
{
    logging.Services.AddSingleton<ILoggerProvider>(sp =>
        sp.GetRequiredService<TelemetryLoggerProvider>());
});

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

builder.Services.AddFluxor(o => o.ScanAssemblies(typeof(Program).Assembly));

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ApiClientService>();
// builder.Services.AddGeolocationServices(); // Removed — using native JS interop
builder.Services.AddGeoBlazor(builder.Configuration);
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
// IMapService/MapService removed — replaced by GeoBlazor component binding
builder.Services.AddScoped<SignalRClientService>();
builder.Services.AddSingleton<BrowserErrorLogger>();

var host = builder.Build();

// Global unhandled-exception tracer — logs the actual exception type and
// message before the Blazor runtime wraps it in AggregateException, whose
// resource-string formatting fails on browser-wasm (dotnet/runtime#60964).
string buildStamp;
try
{
    var attr = typeof(Program).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "BuildTimestamp");
    buildStamp = attr?.Value ?? "dev";
}
catch { buildStamp = "dev"; }

host.Services.GetRequiredService<ILogger<Program>>().LogWarning(
    "RuckR client starting ({Runtime}) build={Build}",
    System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    buildStamp);
await host.Services.GetRequiredService<BrowserErrorLogger>().InitializeAsync();

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Unhandled client startup exception");
    throw;
}
