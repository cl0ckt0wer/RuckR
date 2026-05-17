using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Distance bucket categories for spatial proximity calculations.
    /// </summary>
    public enum DistanceBucket
    {
        Within50m,
        Within100m,
        Within250m,
        Within500m,
        Beyond
    }

    /// <summary>
    /// Immutable geographic position with validation and helper calculations.
    /// </summary>
    public record GeoPosition
    {
        private const double EarthRadiusMeters = 6_371_000.0;

        [Required]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
        public double Latitude { get; init; }

        [Required]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
        public double Longitude { get; init; }

        /// <summary>Optional GPS accuracy in meters.</summary>
        public double? Accuracy { get; init; }

        /// <summary>Capture timestamp for this position.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Calculates the great-circle distance in meters between two GeoPositions
        /// using the Haversine formula.
        /// </summary>
        /// <param name="a">First position.</param>
        /// <param name="b">Second position.</param>
        /// <returns>Distance in meters.</returns>
        public static double HaversineDistance(GeoPosition a, GeoPosition b)
        {
            double lat1Rad = DegreesToRadians(a.Latitude);
            double lat2Rad = DegreesToRadians(b.Latitude);
            double dLatRad = DegreesToRadians(b.Latitude - a.Latitude);
            double dLonRad = DegreesToRadians(b.Longitude - a.Longitude);

            double sinHalfDLat = Math.Sin(dLatRad / 2.0);
            double sinHalfDLon = Math.Sin(dLonRad / 2.0);

            double a_sq = (sinHalfDLat * sinHalfDLat)
                          + (Math.Cos(lat1Rad) * Math.Cos(lat2Rad)
                              * sinHalfDLon * sinHalfDLon);

            double c = 2.0 * Math.Atan2(Math.Sqrt(a_sq), Math.Sqrt(1.0 - a_sq));

            return EarthRadiusMeters * c;
        }

        /// <summary>
        /// Returns the <see cref="DistanceBucket"/> for a given distance in meters.
        /// </summary>
        /// <param name="meters">Distance in meters.</param>
        /// <returns>Closest bucket threshold description.</returns>
        public static DistanceBucket GetDistanceBucket(double meters)
        {
            return meters switch
            {
                <= 50.0 => DistanceBucket.Within50m,
                <= 100.0 => DistanceBucket.Within100m,
                <= 250.0 => DistanceBucket.Within250m,
                <= 500.0 => DistanceBucket.Within500m,
                _ => DistanceBucket.Beyond
            };
        }

        /// <summary>Converts degree values to radians.</summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>Equivalent angle in radians.</returns>
        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
