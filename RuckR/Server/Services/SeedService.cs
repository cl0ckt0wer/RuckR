using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    /// <summary>Defines the server-side class SeedOptions.</summary>
    public class SeedOptions
    {
        /// <summary>Gets or sets the D ef au lt Ce nt er La t.</summary>
        public double DefaultCenterLat { get; set; } = 51.5074;
        /// <summary>Gets or sets the D ef au lt Ce nt er Ln g.</summary>
        public double DefaultCenterLng { get; set; } = -0.1278;
        /// <summary>Gets or sets the S ee dV al ue.</summary>
        public int SeedValue { get; set; } = 42;
        /// <summary>Gets or sets the P la ye rC ou nt.</summary>
        public int PlayerCount { get; set; } = 500;
        /// <summary>Gets or sets the S pr ea dR ad iu sK m.</summary>
        public double SpreadRadiusKm { get; set; } = 50.0;
    }
    /// <summary>Defines the server-side class SeedService.</summary>
    public class SeedService
    {
        private readonly RuckRDbContext _db;
        private readonly PlayerGeneratorService _generator;
        private readonly SeedOptions _options;
        private readonly ILogger<SeedService> _logger;
        private readonly GeometryFactory _geometryFactory;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _env;
        /// <summary>Initializes a new instance of SeedService.</summary>
        /// <param name="db">The db.</param>
        /// <param name="generator">The generator.</param>
        /// <param name="options">The options.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="userManager">The usermanager.</param>
        /// <param name="env">The env.</param>
        public SeedService(
            RuckRDbContext db,
            PlayerGeneratorService generator,
            IOptions<SeedOptions> options,
            ILogger<SeedService> logger,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment env)
        {
            _db = db;
            _generator = generator;
            _options = options.Value;
            _logger = logger;
            _userManager = userManager;
            _env = env;
            _geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        }
        /// <summary>S ee dI fE mp ty As yn c.</summary>
        /// <returns>The operation result.</returns>
        public async Task SeedIfEmptyAsync()
        {
            // Always seed the default pitch if none exist.
            await SeedDefaultPitchAsync();

            if (await _db.Players.AnyAsync())
            {
                _logger.LogInformation("Players table is not empty — skipping player seed data generation.");
            }
            else
            {
                _logger.LogInformation(
                    "Players table is empty — seeding {PlayerCount} players around ({Lat}, {Lng}) with radius {Radius}km.",
                    _options.PlayerCount,
                    _options.DefaultCenterLat,
                    _options.DefaultCenterLng,
                    _options.SpreadRadiusKm);

                await SeedPlayersAsync();

                _logger.LogInformation("Player seed data generation complete.");
            }

            await SeedUsersAsync();
            await SeedCollectionsAsync();
        }

        private async Task SeedDefaultPitchAsync()
        {
            if (await _db.Pitches.AnyAsync())
                return;

            var pitch = new PitchModel
            {
                Name = "RuckR Training Ground",
                Location = _geometryFactory.CreatePoint(
                    new Coordinate(_options.DefaultCenterLng, _options.DefaultCenterLat)),
                CreatorUserId = "system",
                Type = PitchType.Training,
                CreatedAt = DateTime.UtcNow
            };

            _db.Pitches.Add(pitch);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Created default pitch: {PitchName}", pitch.Name);
        }

        private async Task SeedPlayersAsync()
        {
            var players = _generator.GeneratePlayers(
                _options.PlayerCount,
                _options.DefaultCenterLat,
                _options.DefaultCenterLng,
                _options.SpreadRadiusKm);

            const int batchSize = 100;
            for (int i = 0; i < players.Count; i += batchSize)
            {
                var batch = players.Skip(i).Take(batchSize);
                _db.Players.AddRange(batch);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Seeded {PlayerCount} players.", players.Count);
        }

        private async Task SeedUsersAsync()
        {
            var seedFile = Path.Combine(_env.ContentRootPath, "seed-users.json");
            if (!File.Exists(seedFile))
            {
                _logger.LogWarning("seed-users.json not found at {Path} — skipping user seeding.", seedFile);
                return;
            }

            var json = await File.ReadAllTextAsync(seedFile);
            var config = JsonSerializer.Deserialize<SeedUsersConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.Users is null || config.Users.Count == 0)
            {
                _logger.LogWarning("seed-users.json contains no users — skipping user seeding.");
                return;
            }

            var seedPassword = Environment.GetEnvironmentVariable("RUCKR_SEED_USER_PASSWORD");
            if (string.IsNullOrWhiteSpace(seedPassword))
            {
                _logger.LogInformation("RUCKR_SEED_USER_PASSWORD is not set — skipping seed user creation.");
                return;
            }

            _logger.LogInformation("Seeding {Count} users from seed-users.json.", config.Users.Count);

            foreach (var u in config.Users)
            {
                var existing = await _userManager.FindByEmailAsync(u.Email);
                if (existing is not null)
                    continue;

                var user = new IdentityUser
                {
                    UserName = u.Email,
                    Email = u.Email
                };

                var result = await _userManager.CreateAsync(user, seedPassword);
                if (result.Succeeded)
                {
                    _logger.LogInformation("  Created seed user: {Username}", u.Username);
                }
                else
                {
                    _logger.LogWarning("  Failed to create seed user {Username}: {Errors}",
                        u.Username, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            _logger.LogInformation(
                "Seed users ready. Use any of the {Count} accounts configured in seed-users.json: {Usernames}",
                config.Users.Count,
                string.Join(", ", config.Users.Select(u => u.Username)));
        }

        private async Task SeedCollectionsAsync()
        {
            var userCount = await _db.Users.CountAsync();
            if (userCount == 0)
                return;

            var seedFile = Path.Combine(_env.ContentRootPath, "seed-users.json");
            if (!File.Exists(seedFile))
                return;

            var json = await File.ReadAllTextAsync(seedFile);
            var config = JsonSerializer.Deserialize<SeedUsersConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.Users is null)
                return;

            var sizePerUser = Math.Clamp(config.CollectionSizePerUser, 1, 20);

            var playerIds = await _db.Players.Select(p => p.Id).ToListAsync();
            if (playerIds.Count == 0)
                return;

            var pitchIds = await _db.Pitches.Select(p => p.Id).ToListAsync();
            var rng = new Random(_options.SeedValue);

            foreach (var u in config.Users)
            {
                var user = await _userManager.FindByNameAsync(u.Email);
                if (user is null)
                    continue;

                var existingCollectionCount = await _db.Collections.CountAsync(c => c.UserId == user.Id);
                if (existingCollectionCount >= sizePerUser)
                    continue;

                var remaining = sizePerUser - existingCollectionCount;
                var assignedPlayers = playerIds.OrderBy(_ => rng.Next()).Take(remaining);

                foreach (var playerId in assignedPlayers)
                {
                    var pitchId = pitchIds.Count > 0 ? pitchIds[rng.Next(pitchIds.Count)] : (int?)null;

                    _db.Collections.Add(new CollectionModel
                    {
                        UserId = user.Id,
                        PlayerId = playerId,
                        CapturedAtPitchId = pitchId,
                        CapturedAt = DateTime.UtcNow.AddDays(-rng.Next(30)),
                        IsFavorite = rng.Next(4) == 0
                    });
                }

                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Seeded collections for {Count} users ({Size} players each).",
                config.Users.Count, sizePerUser);
        }

        private sealed record SeedUsersConfig(
            int CollectionSizePerUser,
            List<SeedUser> Users);

        private sealed record SeedUser(string Username, string Email);
    }
}

