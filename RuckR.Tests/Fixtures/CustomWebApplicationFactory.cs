using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RuckR.Server.Data;
using RuckR.Server.Hubs;
using RuckR.Server.Services;
using Testcontainers.MsSql;

namespace RuckR.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly MsSqlContainer _dbContainer;
        private readonly TestLocationTracker _locationTracker = new();
        private string _serverAddress = string.Empty;
        private WebApplication? _kestrelApp;

        // Test ArcGIS keys — injected into client appsettings so the map renders in E2E tests.
        private const string TestArcGisApiKey = "test-arcgis-api-key-for-e2e";
        private const string TestArcGisPortalItemId = "test-portal-item-id-for-e2e";
        private const string TestGeoBlazorLicenseKey = "test-geoblazor-license-key-for-e2e";

    public CustomWebApplicationFactory()
    {
        _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(TestSqlPassword.Create())
            .Build();
    }

    public TestLocationTracker LocationTracker => _locationTracker;

    /// <summary>
    /// Returns the Kestrel server's base address (e.g. http://127.0.0.1:51234/) for
    /// Playwright E2E tests.
    /// </summary>
    public string ServerBaseUrl => _serverAddress;

    /// <summary>
    /// Alias for ServerBaseUrl.
    /// </summary>
    public string GetServerAddress() => ServerBaseUrl;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Build the TestServer host (for API tests via CreateClient)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuckRDbContext>();
        await db.Database.MigrateAsync();

        var seedService = scope.ServiceProvider.GetRequiredService<SeedService>();
        await seedService.SeedIfEmptyAsync();

        // Start a real Kestrel server for Playwright E2E tests
        await StartKestrelAsync();
    }

    private async Task StartKestrelAsync()
    {
        if (_kestrelApp != null)
            return;

        // Resolve the Client's wwwroot directory for Blazor WASM _framework files.
        var testAssemblyDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        var clientWwwrootDir = Path.GetFullPath(Path.Combine(testAssemblyDir,
            "..", "..", "..", "..", "RuckR", "Client", "bin", "Debug", "net10.0", "wwwroot"));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = testAssemblyDir,
            WebRootPath = clientWwwrootDir,
            EnvironmentName = Environments.Development
        });

        // ── Configure Kestrel with a random port ──
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxResponseBufferSize = 64 * 1024 * 1024;
        });

        // ── Replicate all service registrations from Program.cs ──

        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
        builder.Services.AddRazorPages();

        // Seed configuration (uses defaults from SeedOptions class)
        builder.Services.Configure<SeedOptions>(_ => { });
        builder.Services.AddSingleton<PlayerGeneratorService>(sp =>
        {
            var seedOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>();
            return new PlayerGeneratorService(seedOptions.Value.SeedValue);
        });
        builder.Services.AddScoped<SeedService>();

        // Replace LocationTracker with test-injectable mock
        builder.Services.AddSingleton<ILocationTracker>(_locationTracker);

        builder.Services.AddScoped<IBattleResolver, BattleResolver>();
        builder.Services.AddScoped<IBattleService, BattleService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IRateLimitService, RateLimitService>();
        builder.Services.AddScoped<IPitchDiscoveryService, PitchDiscoveryService>();
        builder.Services.AddScoped<IRecruitmentService, RecruitmentService>();

        // Replace DbContext with test container
        builder.Services.AddDbContext<RuckRDbContext>(options =>
            options.UseSqlServer(
                _dbContainer.GetConnectionString(),
                x => x.UseNetTopologySuite()));

        builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<RuckRDbContext>();

        builder.Services.AddAuthorization();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Identity/Account/Login";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        builder.Services.AddSignalR();

        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.All;
        });

        // ── OpenTelemetry (mirrors Program.cs) ──
        IOpenTelemetryBuilder otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: "RuckR.Server", serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
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
                .AddSource("RuckR.Server")
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("RuckR.Server.Metrics")
                .AddConsoleExporter())
            .WithLogging();

        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            otel.UseOtlpExporter();
        }

        builder.Services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        // ── Build the app ──
        _kestrelApp = builder.Build();

        // ── Configure middleware pipeline (mirror Program.cs, but for Testing) ──
        _kestrelApp.UseExceptionHandler("/Error");

        _kestrelApp.UseHttpLogging();

        // ── Serve client appsettings with ArcGIS/GeoBlazor key injection (before static files) ──
        _kestrelApp.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/appsettings") && path.EndsWith(".json"))
            {
                var filePath = ResolveClientAppSettingsPath(_kestrelApp.Environment, path);
                if (filePath is not null)
                {
                    var json = await System.IO.File.ReadAllTextAsync(filePath);
                    var root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();

                    root["ArcGISApiKey"] = TestArcGisApiKey;
                    root["ArcGISPortalItemId"] = TestArcGisPortalItemId;

                    var geoBlazor = root["GeoBlazor"] as JsonObject ?? new JsonObject();
                    geoBlazor["LicenseKey"] = TestGeoBlazorLicenseKey;
                    root["GeoBlazor"] = geoBlazor;

                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(root.ToJsonString());
                    return;
                }
            }
            await next();
        });

        _kestrelApp.UseBlazorFrameworkFiles();
        _kestrelApp.MapStaticAssets();

        _kestrelApp.UseRouting();

        _kestrelApp.UseAuthentication();
        _kestrelApp.UseAuthorization();

        _kestrelApp.MapRazorPages().WithStaticAssets();
        _kestrelApp.MapControllers().WithStaticAssets();
        _kestrelApp.MapHub<BattleHub>("/battlehub");
        _kestrelApp.MapGet("/Identity/Account/UserInfo", async (HttpContext context) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Ok(context.User.Identity.Name);
            }
            return Results.Ok(string.Empty);
        });
        _kestrelApp.MapFallbackToFile("index.html");

        // ── Start Kestrel ──
        await _kestrelApp.StartAsync();

        // ── Capture the Kestrel listening address ──
        var server = _kestrelApp.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        var address = addressFeature?.Addresses.FirstOrDefault();
        if (!string.IsNullOrEmpty(address))
        {
            _serverAddress = address.EndsWith('/') ? address : address + "/";
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_kestrelApp != null)
        {
            await _kestrelApp.StopAsync();
            await _kestrelApp.DisposeAsync();
        }
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RuckRDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<RuckRDbContext>(options =>
                options.UseSqlServer(
                    _dbContainer.GetConnectionString(),
                    x => x.UseNetTopologySuite()));

            services.RemoveAll<ILocationTracker>();
            services.AddSingleton<ILocationTracker>(_locationTracker);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                options.DefaultSignInScheme = TestAuthHandler.TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, null);
        });

        builder.UseSetting("ConnectionStrings:RuckRDbContext", _dbContainer.GetConnectionString());

        var serverAssemblyDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        builder.UseContentRoot(serverAssemblyDir);
    }

    public HttpClient CreateAuthenticatedClient(string userId, string? username = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        if (!string.IsNullOrEmpty(username))
            client.DefaultRequestHeaders.Add("X-Test-Username", username);
        return client;
    }

    public async Task<string> CreateTestUserAsync(string username, string password)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = username, Email = $"{username}@test.com" };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded && !result.Errors.All(e => e.Code == "DuplicateUserName"))
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create test user '{username}': {errors}");
        }

        if (!result.Succeeded)
        {
            var existing = await userManager.FindByNameAsync(username);
            if (existing is null)
                throw new InvalidOperationException($"User '{username}' reported as duplicate but not found.");
            return existing.Id;
        }

        return user.Id;
    }

    public async Task SeedCollectionAsync(string userId, int playerId, int pitchId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuckRDbContext>();

        var exists = await db.Collections.AnyAsync(c => c.UserId == userId && c.PlayerId == playerId);
        if (!exists)
        {
            db.Collections.Add(new RuckR.Shared.Models.CollectionModel
            {
                UserId = userId,
                PlayerId = playerId,
                CapturedAtPitchId = pitchId,
                CapturedAt = DateTime.UtcNow,
                IsFavorite = false
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task ExecuteInDbAsync(Func<RuckRDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuckRDbContext>();
        await action(db);
    }

    /// <summary>
    /// Reset all in-memory rate-limit trackers across controllers.
    /// Call between tests to prevent rate-limit state contamination.
    /// </summary>
    public static void ResetAllRateLimits()
    {
        // Rate limits are now DB-backed — no in-memory reset needed.
    }

    /// <summary>
    /// Resolves the physical path of a client appsettings file,
    /// matching the logic in <see cref="Program.ResolveClientAppSettingsPath"/>.
    /// </summary>
    private static string? ResolveClientAppSettingsPath(IWebHostEnvironment env, string requestPath)
    {
        var relativePath = requestPath.TrimStart('/');
        var configuration = env.IsDevelopment() ? "Debug" : "Release";
        var candidateRoots = new[]
        {
            env.WebRootPath,
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "Client", "bin", configuration, "net10.0", "wwwroot")),
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "Client", "wwwroot"))
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
}
