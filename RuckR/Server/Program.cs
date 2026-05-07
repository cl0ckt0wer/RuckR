using System.Text.Json.Serialization;
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
.AddEntityFrameworkStores<RuckRDbContext>();

builder.Services.AddAuthorization();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
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
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddSource("RuckR.Server")
        .AddConsoleExporter())  // always on for dev visibility
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("RuckR.Server.Metrics")
        .AddConsoleExporter())  // always on for dev visibility
    .WithLogging();
otel.UseOtlpExporter();     // reads OTEL_EXPORTER_OTLP_ENDPOINT env var

builder.Services.Configure<OpenTelemetry.Logs.OpenTelemetryLoggerOptions>(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});

// Point WebRootPath to Client build output so UseBlazorFrameworkFiles() finds _framework
var clientWwwroot = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    "..", "..", "..", "Client", "bin",
    builder.Environment.IsDevelopment() ? "Debug" : "Release",
    "net10.0", "wwwroot"));
if (Directory.Exists(clientWwwroot))
{
    builder.WebHost.UseWebRoot(clientWwwroot);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseHttpLogging();

app.UseBlazorFrameworkFiles();
app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages().WithStaticAssets();
app.MapControllers().WithStaticAssets();
app.MapHub<BattleHub>("/battlehub");
app.MapGet("/Identity/Account/UserInfo", async (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Ok(context.User.Identity.Name);
    }
    return Results.Unauthorized();
});

app.MapFallbackToFile("index.html");

// Seed data on startup (runs after app starts; skips if Players table is not empty)
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

public partial class Program { }
