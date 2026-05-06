using NetTopologySuite.Geometries;
using RuckR.Shared.Models;

namespace RuckR.Server.Services
{
    public class PlayerGeneratorService
    {
        private readonly Random _random;
        private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly GeometryFactory _geometryFactory;

        private static readonly string[] FirstNames = new[]
        {
            // Common names
            "James", "Tom", "Will", "Harry", "Dan", "Sam", "Ben", "Jack", "Max", "Joe",
            "Chris", "Matt", "Luke", "Josh", "Alex", "Ryan", "Owen", "Liam", "Noah", "Ethan",
            // Rugby-themed
            "Jonah", "Richie", "Martin", "Jonny", "Bryan", "David", "Sergio", "Kieran", "Siya", "Ardie",
            "Beauden", "Faf", "Antoine", "Maro", "Eben", "Alun", "George", "Jamie", "Finn", "Ngani"
        };

        private static readonly string[] LastNames = new[]
        {
            // Rugby-themed
            "Ruck", "Maul", "Tackle", "Scrum", "Try", "Conversion", "Lineout", "Carter", "McCaw", "Wilkinson",
            "Lomu", "Habana", "Pienaar", "Dupont", "Barrett", "Itoje", "Kolisi", "Savea", "Kolbe", "O'Driscoll",
            // Common names
            "Smith", "Jones", "Williams", "Brown", "Taylor", "Davies", "Evans", "Wilson", "Thomas", "Roberts",
            "Walker", "Wright", "Robinson", "Thompson", "White"
        };

        private static readonly string[] Teams = new[]
        {
            "Ruckfield RFC", "Maulborough", "Scrumpton", "Tacklebury", "Lineout Lions",
            "Try Titans", "Conversion Kings", "Breakdown Bulls", "Rugby Rovers", "Pitch Panthers",
            "Sin-Bin Saints", "Scrum-Half Harriers", "Prop Power RFC", "Fullback Fury", "Winged Warriors"
        };

        private static readonly int PositionCount = Enum.GetValues<PlayerPosition>().Length;

        public PlayerGeneratorService(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        }

        public List<PlayerModel> GeneratePlayers(int count, double centerLat, double centerLng, double spreadRadiusKm = 50.0)
        {
            var players = new List<PlayerModel>(count);
            _usedNames.Clear();

            for (int i = 0; i < count; i++)
            {
                var player = GeneratePlayer(centerLat, centerLng, spreadRadiusKm);
                players.Add(player);
            }

            return players;
        }

        private PlayerModel GeneratePlayer(double centerLat, double centerLng, double spreadRadiusKm)
        {
            string name = GenerateUniqueName();
            PlayerPosition position = (PlayerPosition)_random.Next(PositionCount);
            PlayerRarity rarity = RollRarity();

            var player = new PlayerModel
            {
                Name = name,
                Position = position,
                Team = Teams[_random.Next(Teams.Length)],
                Rarity = rarity,
                SpawnLocation = RandomPointAround(centerLat, centerLng, spreadRadiusKm),
                Bio = string.Empty // populated below
            };

            AssignStats(player, rarity);
            player.Bio = GenerateBio(player);

            return player;
        }

        private string GenerateUniqueName()
        {
            const int maxAttempts = 1000;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                string first = FirstNames[_random.Next(FirstNames.Length)];
                string last = LastNames[_random.Next(LastNames.Length)];
                string full = $"{first} {last}";

                if (_usedNames.Add(full))
                    return full;
            }

            // Fallback: append a numeric suffix if all combinations exhausted
            for (int i = 1; ; i++)
            {
                string first = FirstNames[_random.Next(FirstNames.Length)];
                string last = LastNames[_random.Next(LastNames.Length)];
                string full = $"{first} {last} #{i}";

                if (_usedNames.Add(full))
                    return full;
            }
        }

        private PlayerRarity RollRarity()
        {
            int roll = _random.Next(100);

            return roll switch
            {
                < 50 => PlayerRarity.Common,
                < 75 => PlayerRarity.Uncommon,
                < 90 => PlayerRarity.Rare,
                < 97 => PlayerRarity.Epic,
                _ => PlayerRarity.Legendary
            };
        }

        private void AssignStats(PlayerModel player, PlayerRarity rarity)
        {
            var (speedMin, speedMax, strengthMin, strengthMax, agilityMin, agilityMax, kickingMin, kickingMax) =
                GetPositionStatRanges(player.Position);

            // Apply rarity bonus to stat ranges (Legendary gets higher floor)
            (int rarityFloor, int rarityCeil) = rarity switch
            {
                PlayerRarity.Legendary => (10, 0),   // +10 to min
                PlayerRarity.Epic => (7, 0),          // +7 to min
                PlayerRarity.Rare => (4, 0),          // +4 to min
                PlayerRarity.Uncommon => (2, 0),      // +2 to min
                _ => (0, 0)
            };

            static int ClampStat(int value) => Math.Clamp(value, 1, 99);

            player.Speed    = ClampStat(RollStat(speedMin, speedMax)    + rarityFloor);
            player.Strength = ClampStat(RollStat(strengthMin, strengthMax) + rarityFloor);
            player.Agility  = ClampStat(RollStat(agilityMin, agilityMax)  + rarityFloor);
            player.Kicking  = ClampStat(RollStat(kickingMin, kickingMax)  + rarityFloor);
        }

        private static (int speedMin, int speedMax, int strengthMin, int strengthMax,
                        int agilityMin, int agilityMax, int kickingMin, int kickingMax)
            GetPositionStatRanges(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Prop      => (30, 75,  55, 99,  30, 75,  30, 75),
                PlayerPosition.Hooker    => (30, 75,  50, 90,  40, 80,  30, 75),
                PlayerPosition.Lock      => (30, 70,  60, 99,  30, 70,  30, 70),
                PlayerPosition.Flanker   => (50, 90,  40, 85,  50, 90,  35, 75),
                PlayerPosition.ScrumHalf => (40, 80,  30, 75,  55, 95,  45, 85),
                PlayerPosition.FlyHalf   => (45, 85,  30, 75,  35, 80,  60, 99),
                PlayerPosition.Centre    => (45, 85,  45, 85,  45, 85,  45, 85),
                PlayerPosition.Wing      => (65, 99,  30, 70,  50, 90,  30, 75),
                PlayerPosition.Fullback  => (50, 90,  30, 70,  45, 85,  50, 90),
                _                        => (30, 80,  30, 80,  30, 80,  30, 80)
            };
        }

        private int RollStat(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        private Point RandomPointAround(double centerLat, double centerLng, double maxRadiusKm)
        {
            // Random angle and distance (uniform-by-area: sqrt of random for distance)
            double angle = _random.NextDouble() * 2.0 * Math.PI;
            double distanceKm = Math.Sqrt(_random.NextDouble()) * maxRadiusKm;

            // Approximate conversion: 1° lat ≈ 111.32 km, 1° lng ≈ 111.32 km * cos(lat)
            const double kmPerDegreeLat = 111.32;
            double kmPerDegreeLng = kmPerDegreeLat * Math.Cos(centerLat * Math.PI / 180.0);

            double latOffset = (distanceKm * Math.Cos(angle)) / kmPerDegreeLat;
            double lngOffset = (distanceKm * Math.Sin(angle)) / kmPerDegreeLng;

            double newLat = Math.Clamp(centerLat + latOffset, -90.0, 90.0);
            double newLng = (centerLng + lngOffset + 180.0) % 360.0 - 180.0;

            // NTS Point: X = longitude, Y = latitude
            return _geometryFactory.CreatePoint(new Coordinate(newLng, newLat));
        }

        private static string GenerateBio(PlayerModel player)
        {
            string strengthName = GetStrongestStatName(player);
            return $"{player.Position} known for {strengthName}. Plays for {player.Team}.";
        }

        private static string GetStrongestStatName(PlayerModel player)
        {
            var stats = new (string name, int value)[]
            {
                ("speed", player.Speed),
                ("strength", player.Strength),
                ("agility", player.Agility),
                ("kicking", player.Kicking)
            };

            // Find the stat with the highest value
            var best = stats[0];
            for (int i = 1; i < stats.Length; i++)
            {
                if (stats[i].value > best.value)
                    best = stats[i];
            }

            return best.name;
        }
    }
}
