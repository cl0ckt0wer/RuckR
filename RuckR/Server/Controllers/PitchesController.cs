using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API endpoints for pitch listing, discovery, and creation.</summary>
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>Defines the server-side class PitchesController.</summary>
    public class PitchesController : ControllerBase
    {
        private const int MaxPitchesPerUserPerDay = 5;
        private const double CandidateDuplicateDistanceMeters = 100.0;
        private const int AutoPromoteConfidenceThreshold = 74;
        private const double PitchInteractionMeters = 5000.0;
        private const double MaxPitchInteractionAccuracyMeters = 200.0;

        private static readonly TimeSpan RecentPositionMaxAge = TimeSpan.FromSeconds(60);

        private static readonly GeometryFactory _geometryFactory =
            NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private readonly RuckRDbContext _db;
        private readonly IRateLimitService _rateLimitService;
        private readonly IRealWorldParkService _parkService;
        private readonly IPitchDiscoveryService _pitchDiscoveryService;
        private readonly ILocationTracker _locationTracker;
    /// <summary>Initializes a new instance of <see cref="PitchesController"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    /// <param name="parkService">The park discovery service.</param>
    /// <param name="pitchDiscoveryService">The Places-backed pitch discovery service.</param>
    /// <param name="locationTracker">The live location tracker.</param>
    public PitchesController(
            RuckRDbContext db,
            IRateLimitService rateLimitService,
            IRealWorldParkService parkService,
            IPitchDiscoveryService pitchDiscoveryService,
            ILocationTracker locationTracker)
        {
            _db = db;
            _rateLimitService = rateLimitService;
            _parkService = parkService;
            _pitchDiscoveryService = pitchDiscoveryService;
            _locationTracker = locationTracker;
        }

        /// <summary>
        /// GET /pitches — returns a paginated list of all pitches.
        /// </summary>
        /// <summary>Get a paginated list of pitches.</summary>
        /// <param name="page">The page.</param>
        /// <param name="pageSize">The pagesize.</param>
        /// <returns>The operation result.</returns>
        [HttpGet]
        public async Task<ActionResult<List<PitchModel>>> GetPitches(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

var pitches = await _db.Pitches
                 .Where(p => !(p.Location.Y == 0 && p.Location.X == 0)
                           && !(p.Location.Y == -1 && p.Location.X == -1))
                 .OrderBy(p => p.CreatedAt)
                 .Skip((page - 1) * pageSize)
                 .Take(pageSize)
                 .ToListAsync();

             foreach (var p in pitches)
             {
                 p.Latitude = p.Location.Y;
                 p.Longitude = p.Location.X;
             }

             return Ok(pitches);
         }

         /// <summary>
         /// GET /pitches/nearby — proximity search for pitches within a radius.
         /// </summary>
         /// <summary>Get pitches within a proximity radius.</summary>
         /// <param name="lat">The lat.</param>
         /// <param name="lng">The lng.</param>
         /// <param name="radius">The radius.</param>
         /// <returns>The operation result.</returns>
         [HttpGet("nearby")]
         public async Task<ActionResult<List<PitchModel>>> GetNearbyPitches(
             [FromQuery] double lat,
             [FromQuery] double lng,
             [FromQuery] double radius = 5000)
         {
             if (lat < -90 || lat > 90)
                 return BadRequest("Latitude must be between -90 and 90 degrees.");
             if (lng < -180 || lng > 180)
                 return BadRequest("Longitude must be between -180 and 180 degrees.");
             if (radius <= 0 || radius > 50_000)
                 return BadRequest("Radius must be between 1 and 50000 meters.");

             var pitches = await _pitchDiscoveryService.EnsureNearbyPitchesAsync(
                 GetCurrentUserId(),
                 User.Identity?.Name,
                 lat,
                 lng,
                 radius,
                 HttpContext.RequestAborted);

            return Ok(pitches);
        }

        /// <summary>
        /// GET /pitches/place-candidates — ArcGIS Places that look suitable for pitch creation.
        /// </summary>
         /// <summary>Find nearby real-world pitch candidate places.</summary>
        /// <param name="lat">The lat.</param>
        /// <param name="lng">The lng.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>The operation result.</returns>
        [HttpGet("place-candidates")]
        public async Task<ActionResult<IReadOnlyList<PitchCandidatePlaceDto>>> GetPitchCandidatePlaces(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 5000)
        {
            if (lat < -90 || lat > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (lng < -180 || lng > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (radius <= 0 || radius > 10_000)
                return BadRequest("Radius must be between 1 and 10000 meters.");

            var candidates = await _parkService.FindNearbyPitchCandidatePlacesAsync(lat, lng, radius, HttpContext.RequestAborted);
            var candidatePlaceIds = candidates
                .Select(candidate => candidate.PlaceId)
                .Where(placeId => !string.IsNullOrWhiteSpace(placeId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingPlaceIds = candidatePlaceIds.Count == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : (await _db.Pitches
                    .Where(p => p.ExternalPlaceId != null && candidatePlaceIds.Contains(p.ExternalPlaceId))
                    .Select(p => p.ExternalPlaceId!)
                    .ToListAsync(HttpContext.RequestAborted))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var reviewCandidates = candidates
                .Where(candidate => candidate.Confidence < AutoPromoteConfidenceThreshold)
                .Where(candidate => !existingPlaceIds.Contains(candidate.PlaceId))
                .ToList();

            return Ok(reviewCandidates);
        }

        /// <summary>
        /// GET /pitches/{id} — returns a single pitch by id.
        /// </summary>
        /// <summary>Get a pitch by identifier.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The operation result.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<PitchModel>> GetPitch(int id)
        {
            var pitch = await _db.Pitches.FindAsync(id);

            if (pitch is null)
                return NotFound();

            return Ok(pitch);
        }

        /// <summary>
        /// GET /pitches/{id}/hub — returns interaction status and live hub counts for a pitch.
        /// </summary>
        [HttpGet("{id:int}/hub")]
        [Authorize]
        public async Task<ActionResult<PitchHubDto>> GetPitchHub(
            int id,
            [FromQuery] double? lat = null,
            [FromQuery] double? lng = null,
            [FromQuery] double? accuracy = null)
        {
            if (lat is < -90 or > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (lng is < -180 or > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (lat.HasValue != lng.HasValue)
                return BadRequest("Latitude and longitude must be provided together.");

            var pitch = await _db.Pitches.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (pitch is null)
                return NotFound($"Pitch with id {id} not found.");

            PopulateCoordinates(pitch);
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            var position = ResolveUserPosition(userId, lat, lng, accuracy);
            var pitchPosition = new GeoPosition
            {
                Latitude = pitch.Latitude,
                Longitude = pitch.Longitude
            };
            var distanceMeters = position is null
                ? -1
                : GeoPosition.HaversineDistance(position, pitchPosition);

            var reason = ResolveHubReason(position, distanceMeters);
            var activeRecruitCount = await CountActiveRecruitsNearPitchAsync(pitchPosition);
            var challengeableUserCount = await CountChallengeableUsersNearPitchAsync(userId, pitchPosition);

            return Ok(new PitchHubDto(
                pitch.Id,
                pitch.Name,
                pitch.Type.ToString(),
                pitch.Latitude,
                pitch.Longitude,
                pitch.Source,
                pitch.SourceConfidence,
                distanceMeters,
                distanceMeters < 0 ? nameof(DistanceBucket.Beyond) : GeoPosition.GetDistanceBucket(distanceMeters).ToString(),
                reason == "ELIGIBLE",
                reason,
                activeRecruitCount,
                challengeableUserCount));
        }

        /// <summary>
        /// POST /pitches — creates a new pitch. Requires authentication.
        /// Rate-limited to 5 pitches per user per day.
        /// Pitch names must be globally unique.
        /// </summary>
        /// <summary>Create a new manual pitch.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The operation result.</returns>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<PitchModel>> CreatePitch([FromBody] CreatePitchRequest request)
        {
            // Validate lat/lng ranges
            if (request.Latitude < -90 || request.Latitude > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (request.Longitude < -180 || request.Longitude > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (IsNullIsland(request.Latitude, request.Longitude))
                return BadRequest("Latitude and longitude cannot both be zero. Provide a real location.");
            if (IsSentinelLocation(request.Latitude, request.Longitude))
                return BadRequest("Location appears to be a placeholder (-1,-1). Provide a real location.");

            // Validate PitchType
            if (!Enum.TryParse<PitchType>(request.Type, ignoreCase: true, out var pitchType))
                return BadRequest($"Invalid pitch type '{request.Type}'. Valid types: Standard, Training, Stadium.");

            // Get current user (requires auth)
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            // --- Rate limiting ---
            if (!await _rateLimitService.IsAllowedAsync(userId, "create_pitch", MaxPitchesPerUserPerDay, TimeSpan.FromHours(24)))
                return StatusCode(429, "Rate limit exceeded. You can create up to 5 pitches per 24 hours.");

            var requestPoint = _geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            // --- Global name uniqueness check ---
            var duplicateExists = await _db.Pitches.AnyAsync(p => p.Name == request.Name);

            if (duplicateExists)
                return Conflict($"A pitch named '{request.Name}' already exists.");

            // --- Create and save ---
            var pitch = new PitchModel
            {
                Name = request.Name,
                Location = requestPoint,
                CreatorUserId = userId,
                Type = pitchType,
                Source = "Manual",
                CreatedAt = DateTime.UtcNow
            };

            _db.Pitches.Add(pitch);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPitch), new { id = pitch.Id }, pitch);
        }

        private GeoPosition? ResolveUserPosition(
            string userId,
            double? lat,
            double? lng,
            double? accuracy)
        {
            if (lat.HasValue && lng.HasValue)
            {
                var position = new GeoPosition
                {
                    Latitude = lat.Value,
                    Longitude = lng.Value,
                    Accuracy = accuracy,
                    Timestamp = DateTime.UtcNow
                };
                _locationTracker.UpdatePosition(userId, position);
                return position;
            }

            return _locationTracker.TryGetPosition(userId, RecentPositionMaxAge)?.Position;
        }

        private static string ResolveHubReason(GeoPosition? position, double distanceMeters)
        {
            if (position is null)
                return "GPS_REQUIRED";

            if (position.Accuracy.HasValue && position.Accuracy.Value > MaxPitchInteractionAccuracyMeters)
                return "GPS_INACCURATE";

            if (distanceMeters > PitchInteractionMeters)
                return "TOO_FAR";

            return "ELIGIBLE";
        }

        private async Task<int> CountActiveRecruitsNearPitchAsync(GeoPosition pitchPosition)
        {
            var now = DateTime.UtcNow;
            var encounters = await _db.PlayerEncounters
                .AsNoTracking()
                .Where(encounter => encounter.ExpiresAtUtc > now)
                .Select(encounter => new { encounter.Latitude, encounter.Longitude })
                .ToListAsync();

            return encounters.Count(encounter =>
                GeoPosition.HaversineDistance(
                    pitchPosition,
                    new GeoPosition
                    {
                        Latitude = encounter.Latitude,
                        Longitude = encounter.Longitude
                    }) <= PitchInteractionMeters);
        }

        private async Task<int> CountChallengeableUsersNearPitchAsync(string currentUserId, GeoPosition pitchPosition)
        {
            var nearbyPositions = _locationTracker
                .GetRecentPositions(RecentPositionMaxAge)
                .Where(entry => entry.Key != currentUserId)
                .Select(entry => new
                {
                    UserId = entry.Key,
                    DistanceMeters = GeoPosition.HaversineDistance(pitchPosition, entry.Value.Position)
                })
                .Where(entry => entry.DistanceMeters <= PitchInteractionMeters)
                .ToList();

            if (nearbyPositions.Count == 0)
                return 0;

            var nearbyUserIds = nearbyPositions.Select(entry => entry.UserId).ToList();
            var validUserIds = await _db.Users
                .Where(user => nearbyUserIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToListAsync();
            var recruitCounts = await _db.Collections
                .Where(collection => validUserIds.Contains(collection.UserId))
                .GroupBy(collection => collection.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.UserId, group => group.Count);

            return nearbyPositions.Count(entry =>
                recruitCounts.TryGetValue(entry.UserId, out var recruitCount)
                && recruitCount > 0);
        }

        private static void PopulateCoordinates(PitchModel pitch)
        {
            pitch.Latitude = pitch.Location.Y;
            pitch.Longitude = pitch.Location.X;
        }

        /// <summary>
        /// POST /pitches/from-candidate — converts a reviewed ArcGIS place candidate into a real pitch.
        /// </summary>
        /// <summary>Create a pitch from a discovered candidate.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The operation result.</returns>
        [HttpPost("from-candidate")]
        [Authorize]
        public async Task<ActionResult<PitchModel>> CreatePitchFromCandidate([FromBody] CreatePitchFromCandidateRequest request)
        {
            if (request.Latitude < -90 || request.Latitude > 90)
                return BadRequest("Latitude must be between -90 and 90 degrees.");
            if (request.Longitude < -180 || request.Longitude > 180)
                return BadRequest("Longitude must be between -180 and 180 degrees.");
            if (IsNullIsland(request.Latitude, request.Longitude))
                return BadRequest("Latitude and longitude cannot both be zero. Provide a real location.");
            if (IsSentinelLocation(request.Latitude, request.Longitude))
                return BadRequest("Location appears to be a placeholder (-1,-1). Provide a real location.");
            if (string.IsNullOrWhiteSpace(request.PlaceId))
                return BadRequest("PlaceId is required.");

            if (!Enum.TryParse<PitchType>(request.Type, ignoreCase: true, out var pitchType))
                return BadRequest($"Invalid pitch type '{request.Type}'. Valid types: Standard, Training, Stadium.");

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            if (!await _rateLimitService.IsAllowedAsync(userId, "create_pitch", MaxPitchesPerUserPerDay, TimeSpan.FromHours(24)))
                return StatusCode(429, "Rate limit exceeded. You can create up to 5 pitches per 24 hours.");

            var pitchName = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(pitchName))
                return BadRequest("Pitch name is required.");

            var duplicatePlaceExists = await _db.Pitches.AnyAsync(p => p.ExternalPlaceId == request.PlaceId);
            if (duplicatePlaceExists)
                return Conflict("This place has already been converted into a pitch.");

            var duplicateNameExists = await _db.Pitches.AnyAsync(p => p.Name == pitchName);
            if (duplicateNameExists)
                return Conflict($"A pitch named '{pitchName}' already exists.");

            var requestPoint = _geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));
            var nearbyDuplicateExists = await _db.Pitches.AnyAsync(p =>
                p.Location.IsWithinDistance(requestPoint, CandidateDuplicateDistanceMeters));
            if (nearbyDuplicateExists)
                return Conflict($"A pitch already exists within {CandidateDuplicateDistanceMeters:0} meters of this place.");

            var pitch = new PitchModel
            {
                Name = pitchName,
                Location = requestPoint,
                CreatorUserId = userId,
                Type = pitchType,
                Source = "ArcGISPlaces",
                ExternalPlaceId = request.PlaceId.Trim(),
                SourceCategory = request.CategoryLabel?.Trim(),
                SourceMatchReason = request.MatchReason?.Trim(),
                SourceConfidence = request.Confidence,
                CreatedAt = DateTime.UtcNow
            };

            _db.Pitches.Add(pitch);
            await _db.SaveChangesAsync();

            pitch.Latitude = pitch.Location.Y;
            pitch.Longitude = pitch.Location.X;

            return CreatedAtAction(nameof(GetPitch), new { id = pitch.Id }, pitch);
        }

        /// <summary>
        /// Extracts the current user's ID from the authenticated principal.
        /// </summary>
        private string GetCurrentUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private static bool IsNullIsland(double lat, double lng) =>
            Math.Abs(lat) < 0.05 && Math.Abs(lng) < 0.05;

        private static bool IsSentinelLocation(double lat, double lng) =>
            Math.Abs(lat - (-1)) < 0.01 && Math.Abs(lng - (-1)) < 0.01;
    }
}

