using NetTopologySuite.Geometries;
using RuckR.Shared.Models;

namespace RuckR.Tests.Fixtures;

public static class TestDataFactory
{
    public static (double lat, double lng) CentralLondon = (51.5074, -0.1278);
    public static (double lat, double lng) NearPitch => (51.5075, -0.1277); // ~20m from central

    public static PitchModel CreateTestPitch(string creatorUserId, double lat, double lng)
    {
        return new PitchModel
        {
            Name = "Test Pitch",
            Location = new Point(lng, lat) { SRID = 4326 },
            CreatorUserId = creatorUserId,
            Type = PitchType.Training,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static PlayerModel CreateTestPlayer(double lat, double lng)
    {
        return new PlayerModel
        {
            Name = "Test Player",
            Position = PlayerPosition.FlyHalf,
            Speed = 80,
            Strength = 70,
            Agility = 85,
            Kicking = 90,
            Rarity = PlayerRarity.Rare,
            SpawnLocation = new Point(lng, lat) { SRID = 4326 },
            Bio = "A test rugby player."
        };
    }
}
