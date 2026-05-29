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
    public class ProfileController : ControllerBase
    {
        private const long MaxAvatarRequestBytes = (2 * 1024 * 1024) + (64 * 1024);

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

        /// <summary>Get the current user's profile.</summary>
        /// <returns>The operation result.</returns>
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

        /// <summary>Update or create the current user's profile.</summary>
        /// <param name="updatedProfile">The profile values to persist.</param>
        /// <returns>The operation result.</returns>
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] UserProfileUpdateRequest updatedProfile)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            _logger.LogInformation("PUT /Profile called for user {UserId}", userId);

            var result = await _profileService.CreateOrUpdateProfileAsync(userId, updatedProfile);
            return Ok(result);
        }

        /// <summary>Upload a profile picture for the current user.</summary>
        /// <param name="file">Multipart image file.</param>
        /// <returns>The updated public profile.</returns>
        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxAvatarRequestBytes)]
        public async Task<ActionResult<UserProfileModel>> UploadAvatar(IFormFile? file)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized("User identity not found.");

            if (file is null)
                return BadRequest("Profile picture file is required.");

            try
            {
                var result = await _profileService.UploadAvatarAsync(userId, file, HttpContext.RequestAborted);
                return Ok(result);
            }
            catch (ProfileAvatarValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

