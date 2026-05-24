using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers;

/// <summary>API endpoints for encounter generation and recruitment attempts.</summary>
[ApiController]
[Route("api/[controller]")]
/// <summary>Defines the server-side class RecruitmentController.</summary>
[Authorize]
public class RecruitmentController : ControllerBase
{
    private const double MaxRecruitAccuracyMeters = 100.0;

    private readonly IRecruitmentService _recruitmentService;
    private readonly ILocationTracker _locationTracker;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RuckRDbContext _db;
    private readonly IRateLimitService _rateLimitService;
    /// <summary>Initializes a new instance of <see cref="RecruitmentController"/>.</summary>
    /// <param name="recruitmentService">The recruitment service.</param>
    /// <param name="locationTracker">The location tracker.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="db">The database context.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    public RecruitmentController(
        IRecruitmentService recruitmentService,
        ILocationTracker locationTracker,
        UserManager<IdentityUser> userManager,
        RuckRDbContext db,
        IRateLimitService rateLimitService)
    {
        _recruitmentService = recruitmentService;
        _locationTracker = locationTracker;
        _userManager = userManager;
        _db = db;
        _rateLimitService = rateLimitService;
    }

    /// <summary>Get the current user's game progress profile.</summary>
    /// <returns>The operation result.</returns>
    [HttpGet("profile")]
    public async Task<ActionResult<GameProgressDto>> GetProfile()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var profile = await _db.UserGameProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            return Ok(new GameProgressDto(Level: 1, Experience: 0, NextLevelExperience: 100));
        }

        var nextLevelExperience = profile.Level >= 100 ? profile.Experience : profile.Level * 100;
        return Ok(new GameProgressDto(profile.Level, profile.Experience, nextLevelExperience));
    }

    /// <summary>Attempt to recruit a player from an encounter.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("attempt")]
    public async Task<ActionResult<RecruitmentAttemptResultDto>> Attempt([FromBody] RecruitmentAttemptRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var allowed = await _rateLimitService.IsAllowedAsync(userId, "recruitment_attempt", 60, TimeSpan.FromMinutes(1));
        if (!allowed)
            return StatusCode(429, "Rate limit exceeded for recruitment attempts.");

        var userPosition = ResolveAttemptPosition(userId, request);
        if (userPosition is null)
            return BadRequest("GPS position required. Please enable location services.");

        if (userPosition.Accuracy.HasValue && userPosition.Accuracy.Value > MaxRecruitAccuracyMeters)
            return BadRequest("Improve GPS accuracy (<= 100m) before recruiting.");

        var result = await _recruitmentService.AttemptRecruitmentAsync(userId, request, userPosition);
        return Ok(result);
    }

    private GeoPosition? ResolveAttemptPosition(string userId, RecruitmentAttemptRequest request)
    {
        var positionResult = _locationTracker.TryGetPosition(userId, TimeSpan.FromSeconds(60));
        if (positionResult is not null)
        {
            return positionResult.Value.Position;
        }

        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
        {
            return null;
        }

        if (request.Latitude.Value is < -90 or > 90 || request.Longitude.Value is < -180 or > 180)
        {
            return null;
        }

        var requestPosition = new GeoPosition
        {
            Latitude = request.Latitude.Value,
            Longitude = request.Longitude.Value,
            Accuracy = request.Accuracy,
            Timestamp = DateTime.UtcNow
        };
        _locationTracker.UpdatePosition(userId, requestPosition);
        return requestPosition;
    }
}

