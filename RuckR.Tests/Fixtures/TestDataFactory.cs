using NetTopologySuite.Geometries;
using RuckR.Shared.Models;

namespace RuckR.Tests.Fixtures;

    /// <summary>
    /// Provides access to class.
    /// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Verifies static.
    /// </summary>
    /// <param name="lat">The lat to use.</param>
    /// <param name="lng">The lng to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
    public static (double lat, double lng) CentralLondon = (51.5074, -0.1278);
    /// <summary>
    /// Verifies static.
    /// </summary>
    /// <param name="lat">The lat to use.</param>
    /// <param name="lng">The lng to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
    public static (double lat, double lng) NearPitch => (51.5075, -0.1277); // ~20m from central

    /// <summary>
    /// Verifies create Test Pitch.
    /// </summary>
    /// <param name="creatorUserId">The creatorUserId to use.</param>
    /// <param name="lat">The lat to use.</param>
    /// <param name="lng">The lng to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
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

    /// <summary>
    /// Verifies create Test Player.
    /// </summary>
    /// <param name="lat">The lat to use.</param>
    /// <param name="lng">The lng to use.</param>
    /// <returns>A value indicating the result of this operation.</returns>
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


