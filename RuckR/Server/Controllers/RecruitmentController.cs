using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuckR.Server.Data;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecruitmentController : ControllerBase
{
    private const double MaxCaptureAccuracyMeters = 50.0;

    private readonly IRecruitmentService _recruitmentService;
    private readonly ILocationTracker _locationTracker;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RuckRDbContext _db;
    private readonly IRateLimitService _rateLimitService;

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

    [HttpPost("attempt")]
    public async Task<ActionResult<RecruitmentAttemptResultDto>> Attempt([FromBody] RecruitmentAttemptRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var allowed = await _rateLimitService.IsAllowedAsync(userId, "recruitment_attempt", 60, TimeSpan.FromMinutes(1));
        if (!allowed)
            return StatusCode(429, "Rate limit exceeded for recruitment attempts.");

        var positionResult = _locationTracker.TryGetPosition(userId, TimeSpan.FromSeconds(60));
        if (positionResult is null)
            return BadRequest("GPS position required. Please enable location services.");

        if (positionResult.Value.Position.Accuracy.HasValue && positionResult.Value.Position.Accuracy.Value > MaxCaptureAccuracyMeters)
            return BadRequest("Improve GPS accuracy (<= 50m) before recruiting.");

        var result = await _recruitmentService.AttemptRecruitmentAsync(userId, request, positionResult.Value.Position);
        return Ok(result);
    }
}
