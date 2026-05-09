using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MapController : ControllerBase
{
    private readonly IRecruitmentService _recruitmentService;
    private readonly UserManager<IdentityUser> _userManager;

    public MapController(IRecruitmentService recruitmentService, UserManager<IdentityUser> userManager)
    {
        _recruitmentService = recruitmentService;
        _userManager = userManager;
    }

    [HttpGet("encounters")]
    public async Task<ActionResult<IReadOnlyList<PlayerEncounterDto>>> GetEncounters(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius = 300)
    {
        if (lat < -90 || lat > 90)
            return BadRequest("Latitude must be between -90 and 90 degrees.");
        if (lng < -180 || lng > 180)
            return BadRequest("Longitude must be between -180 and 180 degrees.");
        if (radius <= 0 || radius > 5000)
            return BadRequest("Radius must be between 1 and 5000 meters.");

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var encounters = await _recruitmentService.GetEncountersAsync(userId, lat, lng, radius);
        return Ok(encounters);
    }
}
