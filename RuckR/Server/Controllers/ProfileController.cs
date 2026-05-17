using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>API for reading and updating the authenticated user's profile.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    /// <summary>Defines the server-side class ProfileController.</summary>
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ProfileController> _logger;
    /// <summary>Initializes a new instance of <see cref="ProfileController"/>.</summary>
    /// <param name="profileService">The profile service.</param>
    /// <param name="userManager">The identity user manager.</param>
    /// <param name="logger">The logger.</param>
    public ProfileController(
            IProfileService profileService,
            UserManager<IdentityUser> userManager,
            ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
    /// <summary>Get the current user's profile.</summary>
    /// <returns>The operation result.</returns>
    public async Task<ActionResult<UserProfileModel>> Get()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            _logger.LogInformation("GET /Profile called for user {UserId}", userId);

            var profile = await _profileService.GetProfileAsync(userId);
            if (profile is null)
                return NotFound();

            return Ok(profile);
        }

        [HttpPut]
    /// <summary>Update or create the current user's profile.</summary>
    /// <param name="updatedProfile">The profile values to persist.</param>
    /// <returns>The operation result.</returns>
    public async Task<IActionResult> Put([FromBody] UserProfileModel updatedProfile)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            _logger.LogInformation("PUT /Profile called for user {UserId}", userId);

            updatedProfile.UserId = userId;
            var result = await _profileService.CreateOrUpdateProfileAsync(userId, updatedProfile);
            return Ok(result);
        }
    }
}

