using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;
using RuckR.Tests.Fixtures;
using Testcontainers.MsSql;

namespace RuckR.Tests.Api;

public class SeedServiceTests
{
    [Fact]
    public async Task SeedIfEmptyAsync_WithoutSeedPassword_DoesNotCreateSeedUsers()
    {
        var previousPassword = Environment.GetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD");
        Environment.SetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD", null);

        await using var scope = await CreateSeedScopeAsync();
        try
        {
            await scope.SeedService.SeedIfEmptyAsync();

            var userCount = await scope.Db.Users.CountAsync();
            Assert.Equal(0, userCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD", previousPassword);
        }
    }

    [Fact]
    public async Task SeedIfEmptyAsync_WithSeedPassword_FillsCollectionsAndItemsIdempotently()
    {
        var previousPassword = Environment.GetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD");
        Environment.SetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD", $"Seed-{Guid.NewGuid():N}aA1!");

        await using var scope = await CreateSeedScopeAsync();
        try
        {
            await scope.SeedService.SeedIfEmptyAsync();
            await scope.SeedService.SeedIfEmptyAsync();

            var seedUsers = await scope.Db.Users
                .Where(u => u.Email != null && u.Email.EndsWith("@ruckr.game"))
                .OrderBy(u => u.Email)
                .ToListAsync();

            Assert.Equal(10, seedUsers.Count);
            foreach (var user in seedUsers)
            {
                var collection = await scope.Db.Collections
                    .Where(c => c.UserId == user.Id)
                    .Select(c => c.PlayerId)
                    .ToListAsync();
                Assert.Equal(5, collection.Count);
                Assert.Equal(5, collection.Distinct().Count());

                var items = await scope.Db.UserRecruitmentItems
                    .Where(i => i.UserId == user.Id)
                    .ToListAsync();
                Assert.Contains(items, i => i.ItemKind == RecruitmentItemKind.Chips && i.Quantity >= 3);
                Assert.Contains(items, i => i.ItemKind == RecruitmentItemKind.Beer && i.Quantity >= 2);
                Assert.Contains(items, i => i.ItemKind == RecruitmentItemKind.Whiskey && i.Quantity >= 1);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD", previousPassword);
        }
    }

    private static async Task<SeedTestScope> CreateSeedScopeAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ruckr-seed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var sourceSeedFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RuckR", "Server", "seed-users.json"));
        File.Copy(sourceSeedFile, Path.Combine(tempRoot, "seed-users.json"));

        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword(TestSqlPassword.Create())
            .Build();
        await container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<SeedOptions>(options =>
        {
            options.PlayerCount = 60;
            options.SpreadRadiusKm = 5;
        });
        services.AddDbContext<RuckRDbContext>(options =>
            options.UseSqlServer(container.GetConnectionString(), x => x.UseNetTopologySuite()));
        services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
        })
            .AddEntityFrameworkStores<RuckRDbContext>();
        services.AddSingleton<PlayerGeneratorService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SeedOptions>>();
            return new PlayerGeneratorService(options.Value.SeedValue);
        });
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(tempRoot));
        services.AddScoped<SeedService>();

        var provider = services.BuildServiceProvider();
        var serviceScope = provider.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<RuckRDbContext>();
        await db.Database.MigrateAsync();

        return new SeedTestScope(
            container,
            provider,
            serviceScope,
            db,
            serviceScope.ServiceProvider.GetRequiredService<SeedService>(),
            tempRoot);
    }

    private sealed class SeedTestScope(
        MsSqlContainer container,
        ServiceProvider provider,
        IServiceScope serviceScope,
        RuckRDbContext db,
        SeedService seedService,
        string tempRoot) : IAsyncDisposable
    {
        public RuckRDbContext Db { get; } = db;

        public SeedService SeedService { get; } = seedService;

        public async ValueTask DisposeAsync()
        {
            serviceScope.Dispose();
            await provider.DisposeAsync();
            await container.DisposeAsync();
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "RuckR.Tests";

        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
