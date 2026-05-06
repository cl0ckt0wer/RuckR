using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public class SeedOptions
    {
        public double DefaultCenterLat { get; set; } = 51.5074;
        public double DefaultCenterLng { get; set; } = -0.1278;
        public int SeedValue { get; set; } = 42;
        public int PlayerCount { get; set; } = 500;
        public double SpreadRadiusKm { get; set; } = 50.0;
    }

    public class SeedService
    {
        private readonly RuckRDbContext _db;
        private readonly PlayerGeneratorService _generator;
        private readonly SeedOptions _options;
        private readonly ILogger<SeedService> _logger;
        private readonly GeometryFactory _geometryFactory;

        public SeedService(
            RuckRDbContext db,
            PlayerGeneratorService generator,
            IOptions<SeedOptions> options,
            ILogger<SeedService> logger)
        {
            _db = db;
            _generator = generator;
            _options = options.Value;
            _logger = logger;
            _geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        }

        public async Task SeedIfEmptyAsync()
        {
            if (await _db.Players.AnyAsync())
            {
                _logger.LogInformation("Players table is not empty — skipping seed data generation.");
                return;
            }

            _logger.LogInformation(
                "Players table is empty — seeding {PlayerCount} players around ({Lat}, {Lng}) with radius {Radius}km.",
                _options.PlayerCount,
                _options.DefaultCenterLat,
                _options.DefaultCenterLng,
                _options.SpreadRadiusKm);

            await SeedDefaultPitchAsync();
            await SeedPlayersAsync();

            _logger.LogInformation("Seed data generation complete.");
        }

        private async Task SeedDefaultPitchAsync()
        {
            // Only create the default pitch if none exist
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

            // Insert in batches to avoid memory pressure with large datasets
            const int batchSize = 100;
            for (int i = 0; i < players.Count; i += batchSize)
            {
                var batch = players.Skip(i).Take(batchSize);
                _db.Players.AddRange(batch);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Seeded {PlayerCount} players.", players.Count);
        }
    }
}
