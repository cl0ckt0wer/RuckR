using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Hubs;
using RuckR.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddSignalR();

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

app.UseBlazorFrameworkFiles();
app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages().WithStaticAssets();
app.MapControllers().WithStaticAssets();
app.MapHub<BattleHub>("/battlehub");
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
