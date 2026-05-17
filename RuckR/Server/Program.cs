using System.Net.Http;
using System.Text;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RuckR.Server.Data;
using RuckR.Server.Hubs;
using RuckR.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;

    // exe.dev terminates TLS and forwards to the local Kestrel process. The
    // proxy source can change, so trust forwarded headers at this boundary.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxResponseBufferSize = 64 * 1024 * 1024;
    options.Limits.Http2.MaxStreamsPerConnection = 200;
    options.Limits.Http2.InitialConnectionWindowSize = 256 * 1024;
    options.Limits.Http2.InitialStreamWindowSize = 256 * 1024;
});

// Add services to the container.

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Allow NaN/Infinity double values from NetTopologySuite Point types
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
        // Ignore cycles in NetTopologySuite geometry types (Point has circular references)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddRazorPages();

// Seed configuration
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));
builder.Services.AddSingleton<PlayerGeneratorService>(sp =>
{
    var seedOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>();
    return new PlayerGeneratorService(seedOptions.Value.SeedValue);
});
builder.Services.AddScoped<SeedService>();

builder.Services.AddSingleton<ILocationTracker, LocationTracker>();
builder.Services.AddScoped<IBattleResolver, BattleResolver>();
builder.Services.AddScoped<IBattleService, BattleService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<IPitchDiscoveryService, PitchDiscoveryService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IRealWorldParkService, ArcGisPlacesParkService>();
builder.Services.AddScoped<IRecruitmentService, RecruitmentService>();
builder.Services.AddHostedService<ChallengeCleanupService>();

builder.Services.AddDbContext<RuckRDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RuckRDbContext"),
        x => x.UseNetTopologySuite()
    )
);

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // MVP: no email confirmation
})
.AddDefaultUI()
.AddEntityFrameworkStores<RuckRDbContext>();

builder.Services.AddAuthorization();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Events.OnRedirectToLogin = context =>
         {
             if (context.Request.Path.StartsWithSegments("/api"))
             {
                 context.Response.StatusCode = 401;
                 return Task.CompletedTask;
             }
             // Normalize absolute ReturnUrl to relative path when behind a reverse proxy.
             var redirectUri = context.RedirectUri;
             if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var absUri))
             {
                 redirectUri = absUri.PathAndQuery;
             }
             context.Response.Redirect(redirectUri);
             return Task.CompletedTask;
         };
});

builder.Services.AddSignalR();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
});

// ── OpenTelemetry ──
IOpenTelemetryBuilder otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "RuckR.Server", serviceVersion: "1.0.0"))
        .WithTracing(tracing => {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/healthz") &&
                    !httpContext.Request.Path.StartsWithSegments("/_framework");
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("RuckR.Server");
        if (builder.Environment.IsDevelopment())
            tracing.AddConsoleExporter();
        })
    .WithMetrics(metrics => {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("RuckR.Server.Metrics");
        if (builder.Environment.IsDevelopment())
            metrics.AddConsoleExporter();
        })
    .WithLogging();
otel.UseOtlpExporter();     // reads OTEL_EXPORTER_OTLP_ENDPOINT env var

builder.Services.Configure<OpenTelemetry.Logs.OpenTelemetryLoggerOptions>(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});

// ── GeoBlazor/ArcGIS key bridge ──
// GeoBlazor reads IConfiguration on the client side.
// In hosted Blazor WASM, the client loads wwwroot/appsettings*.json at runtime.
// This middleware injects only the explicit map keys from env vars into the
// JSON response, so the keys are never committed to source control.
var arcGisApiKey = GetEnvironmentSecret("ArcGISApiKey", "ARC_GIS_API_KEY");
var arcGisPortalItemId = GetEnvironmentSecret("ArcGISPortalItemId", "ARC_GIS_PORTAL_ITEM_ID");
var geoBlazorLicenseKey = GetEnvironmentSecret(
    "GeoBlazor__LicenseKey",
    "GeoBlazor:LicenseKey",
    "GeoBlazor__RegistrationKey",
    "GeoBlazor:RegistrationKey",
    "GEOBLAZOR_LICENSE_KEY",
    "GEOBLAZOR_REGISTRATION_KEY",
    "GEOBLAZOR_API");

if (!string.IsNullOrWhiteSpace(arcGisApiKey) || !string.IsNullOrWhiteSpace(arcGisPortalItemId) || !string.IsNullOrWhiteSpace(geoBlazorLicenseKey))
{
    builder.Logging.AddFilter("GeoBlazorConfiguration", LogLevel.Debug);
}

var app = builder.Build();

app.UseForwardedHeaders();

// Serve client appsettings with GeoBlazor/ArcGIS key injection (before static files).
if (!string.IsNullOrWhiteSpace(arcGisApiKey) || !string.IsNullOrWhiteSpace(arcGisPortalItemId) || !string.IsNullOrWhiteSpace(geoBlazorLicenseKey))
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/appsettings") && path.EndsWith(".json"))
        {
            var filePath = ResolveClientAppSettingsPath(app.Environment, path);
            if (filePath is not null)
            {
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();

                if (!string.IsNullOrWhiteSpace(arcGisApiKey))
                {
                    root["ArcGISApiKey"] = arcGisApiKey;
                }

                if (!string.IsNullOrWhiteSpace(arcGisPortalItemId))
                {
                    root["ArcGISPortalItemId"] = arcGisPortalItemId;
                }

                if (!string.IsNullOrWhiteSpace(geoBlazorLicenseKey))
                {
                    var geoBlazor = root["GeoBlazor"] as JsonObject ?? new JsonObject();
                    geoBlazor["LicenseKey"] = geoBlazorLicenseKey;
                    geoBlazor["RegistrationKey"] = geoBlazorLicenseKey;
                    root["GeoBlazor"] = geoBlazor;
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
                await context.Response.WriteAsync(root.ToJsonString());
                return;
            }
        }
        await next();
    });
}

static string? ResolveClientAppSettingsPath(IWebHostEnvironment environment, string requestPath)
{
    var relativePath = requestPath.TrimStart('/');
    var configuration = environment.IsDevelopment() ? "Debug" : "Release";
    var candidateRoots = new[]
    {
        environment.WebRootPath,
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Client", "bin", configuration, "net10.0", "wwwroot")),
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Client", "wwwroot"))
    };

    foreach (var root in candidateRoots)
    {
        if (string.IsNullOrWhiteSpace(root))
            continue;

        var path = Path.Combine(root, relativePath);
        if (System.IO.File.Exists(path))
            return path;
    }

    return null;
}

static string? GetEnvironmentSecret(params string[] names)
{
    foreach (var name in names)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return null;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpLogging();

// ── Jaeger Reverse Proxy ──
// The SPA fallback (MapFallbackToFile) catches all unmatched paths,
// so we intercept /jaeger/* before Blazor serves the SPA shell.
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/jaeger"), proxy =>
{
    var target = "http://localhost:16686";
    var client = new HttpClient();

    proxy.Run(async ctx =>
    {
        var path = ctx.Request.Path.Value ?? "";
        var query = ctx.Request.QueryString.Value ?? "";
        var targetUri = $"{target}{path}{query}";

        var request = new HttpRequestMessage();
        request.RequestUri = new Uri(targetUri);
        request.Method = new HttpMethod(ctx.Request.Method);

        // Copy request headers (excluding hop-by-hop and content-length)
        foreach (var header in ctx.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Host = "localhost:16686";
            }
            else if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                     && !header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                     && !header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Forward request body for non-GET requests
        if (ctx.Request.ContentLength > 0)
        {
            var stream = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(stream);
            stream.Position = 0;
            request.Content = new StreamContent(stream);
        }

        try
        {
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

            ctx.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
            {
                if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            // Copy response body
            await response.Content.CopyToAsync(ctx.Response.Body);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsync($"Jaeger proxy error: {ex.Message}");
        }
    });
});

app.Use(async (context, next) =>
{
    if (IsSpaShellRequest(context.Request))
    {
        context.Response.OnStarting(() =>
        {
            ApplyNoStoreHeaders(context.Response.Headers);
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseWhen(ctx => ctx.Request.Path.Equals("/css/app.css", StringComparison.OrdinalIgnoreCase), appCss =>
{
    appCss.Run(async context =>
    {
        var appCssPath = Path.Combine(app.Environment.WebRootPath, "css", "app.css");
        if (!File.Exists(appCssPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        ApplyNoStoreHeaders(context.Response.Headers);
        context.Response.ContentType = "text/css; charset=utf-8";

        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.SendFileAsync(appCssPath);
    });
});

app.UseBlazorFrameworkFiles();

app.UseWhen(ctx => ctx.Request.Path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase), serviceWorker =>
{
    serviceWorker.Use(async (context, next) =>
    {
        ApplyNoStoreHeaders(context.Response.Headers);
        await next();
    });
});

// Keep boot/runtime manifests fresh, but let fingerprinted WASM assets use the
// static-asset pipeline's immutable caching and precompressed responses.
app.UseWhen(ctx => IsBlazorBootResource(ctx.Request.Path), framework =>
{
    framework.Use(async (context, next) =>
    {
        ApplyNoStoreHeaders(context.Response.Headers);
        await next();
    });
});

    app.MapStaticAssets();

app.UseRouting();

app.UseCookiePolicy();

// Security headers — set CSP server-side so browsers respect it for all responses
// including the /.well-known/webauthn probe that Safari fires on every page.
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval' 'unsafe-eval' 'unsafe-inline' https://js.arcgis.com; " +
        "worker-src 'self' blob:; " +
        "style-src 'self' 'unsafe-inline' https:; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self' wss: https:; " +
        "font-src 'self' data: https://js.arcgis.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages().WithStaticAssets();
app.MapControllers().WithStaticAssets();
app.MapHub<BattleHub>("/battlehub");

// Respond 204 to Safari's WebAuthn domain-probe to avoid CSP console warnings.
app.MapGet("/.well-known/webauthn", () => Results.StatusCode(204));

app.MapGet("/Identity/Account/UserInfo", async (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Ok(context.User.Identity.Name);
    }
    return Results.Ok(string.Empty);
});

app.MapFallbackToFile("index.html");

// Apply EF Core migrations, then seed data on startup.
// Use a SQL-native app lock to prevent concurrent migrations during rolling deploys.
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RuckRDbContext>();
    var conn = dbContext.Database.GetDbConnection();
    await conn.OpenAsync();
    try
    {
        await using var acquire = conn.CreateCommand();
        acquire.CommandText = "DECLARE @res int; EXEC @res = sp_getapplock @Resource='RuckR:EFMigrate', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=30000; SELECT @res;";
        var acquireResult = Convert.ToInt32(await acquire.ExecuteScalarAsync());
        if (acquireResult < 0)
        {
            throw new InvalidOperationException($"Unable to acquire migration lock (sp_getapplock result: {acquireResult}).");
        }

        await dbContext.Database.MigrateAsync();

        await using var release = conn.CreateCommand();
        release.CommandText = "EXEC sp_releaseapplock @Resource='RuckR:EFMigrate', @LockOwner='Session';";
        await release.ExecuteNonQueryAsync();
    }
    finally
    {
        await conn.CloseAsync();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database migration failed");
}

try
{
    using var scope = app.Services.CreateScope();
    var seedService = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seedService.SeedIfEmptyAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Seed data generation skipped — database may not be available yet.");
}

app.Run();

static bool IsBlazorBootResource(PathString path)
{
    return path.Equals("/_framework/blazor.boot.json", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/_framework/blazor.webassembly.js", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/_framework/dotnet.js", StringComparison.OrdinalIgnoreCase);
}

static bool IsSpaShellRequest(HttpRequest request)
{
    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        return false;

    var path = request.Path.Value ?? "";
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/jaeger", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/.well-known", StringComparison.OrdinalIgnoreCase)
        || Path.HasExtension(path))
    {
        return false;
    }

    return request.Headers.Accept.Any(value =>
        value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
}

static void ApplyNoStoreHeaders(IHeaderDictionary headers)
{
    headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    headers["Pragma"] = "no-cache";
    headers["Expires"] = "0";
}
/// <summary>Defines the server-side class Program.</summary>
public partial class Program { }

