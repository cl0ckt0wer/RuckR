using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    public RecruitmentController(
        IRecruitmentService recruitmentService,
        ILocationTracker locationTracker,
        UserManager<IdentityUser> userManager)
    {
        _recruitmentService = recruitmentService;
        _locationTracker = locationTracker;
        _userManager = userManager;
    }

    [HttpPost("attempt")]
    public async Task<ActionResult<RecruitmentAttemptResultDto>> Attempt([FromBody] RecruitmentAttemptRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var positionResult = _locationTracker.TryGetPosition(userId, TimeSpan.FromSeconds(60));
        if (positionResult is null)
            return BadRequest("GPS position required. Please enable location services.");

        if (positionResult.Value.Position.Accuracy.HasValue && positionResult.Value.Position.Accuracy.Value > MaxCaptureAccuracyMeters)
            return BadRequest("Improve GPS accuracy (<= 50m) before recruiting.");

        var result = await _recruitmentService.AttemptRecruitmentAsync(userId, request, positionResult.Value.Position);
        return Ok(result);
    }
}
