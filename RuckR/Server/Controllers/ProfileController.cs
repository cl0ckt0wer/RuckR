using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RuckR.Server.Services;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ProfileController> _logger;

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
