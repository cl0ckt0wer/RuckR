using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using RuckR.Server.Data;
using RuckR.Shared.Models;

namespace RuckR.Server.Controllers
{
    /// <summary>Privacy and consent endpoints for user data handling.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    /// <summary>Defines the server-side class PrivacyController.</summary>
    public class PrivacyController : ControllerBase
    {
        private readonly RuckRDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
    /// <summary>Initializes a new instance of <see cref="PrivacyController"/>.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="userManager">The identity user manager.</param>
    public PrivacyController(RuckRDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>
        /// POST /api/privacy/consent — records explicit user consent for GPS and data processing.
        /// Must be called before GPS tracking begins.
        /// </summary>
        [HttpPost("consent")]
        /// <summary>Record explicit user consent for telemetry or GPS processing.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The operation result.</returns>
        public async Task<IActionResult> GiveConsent([FromBody] ConsentRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Purpose))
                return BadRequest("Purpose is required.");

            var consent = new UserConsent
            {
                UserId = userId,
                Purpose = request.Purpose.Trim(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _db.UserConsents.Add(consent);
            await _db.SaveChangesAsync();

            return Ok(new { consent.Id, consent.ConsentGivenAtUtc });
        }

        /// <summary>
        /// GET /api/privacy/consent — checks whether the user has consented to the given purpose.
        /// </summary>
        [HttpGet("consent")]
        /// <summary>Check whether the user has consented for a specific purpose.</summary>
        /// <param name="purpose">The purpose.</param>
        /// <returns>The operation result.</returns>
        public async Task<IActionResult> HasConsent([FromQuery] string purpose)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(purpose))
                return BadRequest("Purpose is required.");

            var hasConsent = await _db.UserConsents
                .AnyAsync(c => c.UserId == userId
                    && c.Purpose == purpose.Trim()
                    && c.ConsentGivenAtUtc > DateTime.UtcNow.AddYears(-1));

            return Ok(new { hasConsent });
        }

        /// <summary>
        /// DELETE /api/me/data — permanently deletes all user data (GDPR Article 17 right to erasure).
        /// Cascades across collections, battles, encounters, profiles, consents, and rate limits.
        /// </summary>
        [HttpDelete("me/data")]
        /// <summary>Delete all data associated with the authenticated user.</summary>
        /// <returns>The operation result.</returns>
        public async Task<IActionResult> DeleteMyData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return NotFound();

            // Delete game data that references the user's player entities
            var collections = _db.Collections.Where(c => c.UserId == userId);
            _db.Collections.RemoveRange(collections);

            var encounters = _db.PlayerEncounters.Where(e => e.UserId == userId);
            _db.PlayerEncounters.RemoveRange(encounters);

            var profiles = _db.UserProfiles.Where(p => p.UserId == userId);
            _db.UserProfiles.RemoveRange(profiles);

            var gameProfiles = _db.UserGameProfiles.Where(p => p.UserId == userId);
            _db.UserGameProfiles.RemoveRange(gameProfiles);

            var rateLimits = _db.RateLimitRecords.Where(r => r.UserId == userId);
            _db.RateLimitRecords.RemoveRange(rateLimits);

            var consents = _db.UserConsents.Where(c => c.UserId == userId);
            _db.UserConsents.RemoveRange(consents);

            // Battles — null out IDs rather than delete to preserve opponent's history
            var userBattles = _db.Battles.Where(b => b.ChallengerId == userId || b.OpponentId == userId);
            foreach (var battle in userBattles)
            {
                if (battle.ChallengerId == userId) battle.ChallengerId = string.Empty;
                if (battle.OpponentId == userId) battle.OpponentId = string.Empty;
            }

            // Delete Identity user last (FK constraints)
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors });

            await _db.SaveChangesAsync();

            return Ok(new { deleted = true, timestamp = DateTime.UtcNow });
        }
    }
    /// <summary>Defines the server-side record ConsentRequest.</summary>
    public sealed record ConsentRequest([Required, MaxLength(100)] string Purpose);
}
