using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ILogger<ProfileController> _logger;
        private static ProfileModel _profile = new()
        {
            Name = "Your Name",
            Biography = "Write something about yourself...",
            Email = "you@example.com",
            Location = "Earth",
            JoinedDate = DateTime.Today,
            AvatarUrl = ""
        };

        public ProfileController(ILogger<ProfileController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<ProfileModel> Get()
        {
            _logger.LogInformation("GET /Profile called");
            return _profile;
        }

        [HttpPut]
        public IActionResult Put([FromBody] ProfileModel updatedProfile)
        {
            _logger.LogInformation("PUT /Profile called");
            _profile = updatedProfile;
            return Ok();
        }
    }
}
