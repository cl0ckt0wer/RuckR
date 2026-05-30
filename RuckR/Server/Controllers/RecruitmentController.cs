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
    private const double MaxRecruitAccuracyMeters = 200.0;

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
    public async Task<ActionResult<RecruitmentProfileDto>> GetProfile()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User identity not found.");

        var now = DateTime.UtcNow;
        var profile = await _db.UserGameProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var level = profile?.Level ?? 1;
        var experience = profile?.Experience ?? 0;
        var nextLevelExperience = level >= 100 ? experience : level * 100;

        await EnsureStarterRecruitmentItemsAsync(userId, now);

        var items = await _db.UserRecruitmentItems
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.ItemKind)
            .Select(i => new RecruitmentItemDto(i.ItemKind, i.Quantity))
            .ToListAsync();

        var active = await _db.PlayerEncounters
            .AsNoTracking()
            .Include(e => e.Player)
            .Include(e => e.Participants)
            .Where(e => e.ExpiresAtUtc > now
                && e.Participants.Any(p => p.UserId == userId)
                && e.RecruitmentStartedAtUtc != null
                && e.RecruitmentCompletesAtUtc != null)
            .OrderBy(e => e.RecruitmentCompletesAtUtc)
            .FirstOrDefaultAsync();

        ActiveRecruitmentSessionDto? activeDto = null;
        if (active?.Player is not null && active.RecruitmentStartedAtUtc is { } startedAt && active.RecruitmentCompletesAtUtc is { } completesAt)
        {
            activeDto = new ActiveRecruitmentSessionDto(
                active.Id,
                active.PlayerId,
                active.Player.Name,
                active.Player.Position.ToString(),
                active.Player.Rarity.ToString(),
                startedAt,
                completesAt,
                active.RecruitmentBaseDurationSeconds,
                active.RecruitmentRequiredDurationSeconds,
                Math.Max(0, (int)Math.Ceiling((completesAt - now).TotalSeconds)),
                active.RecruitmentLocalPlayerCount,
                active.RecruitmentItemKind,
                BuildRecruitmentBoosts(
                    active.RecruitmentBaseDurationSeconds,
                    active.RecruitmentRequiredDurationSeconds,
                    active.RecruitmentLocalPlayerCount,
                    active.RecruitmentItemKind),
                active.Participants.Select(p => p.UserId).Distinct().Count());
        }

        return Ok(new RecruitmentProfileDto(level, experience, nextLevelExperience, items, activeDto));
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
            return BadRequest($"Improve GPS accuracy (<= {MaxRecruitAccuracyMeters:0}m) before recruiting.");

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

    private async Task EnsureStarterRecruitmentItemsAsync(string userId, DateTime now)
    {
        var hasAnyItems = await _db.UserRecruitmentItems.AnyAsync(i => i.UserId == userId);
        if (hasAnyItems)
            return;

        _db.UserRecruitmentItems.AddRange(
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Chips, Quantity = 3, UpdatedAtUtc = now },
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Beer, Quantity = 2, UpdatedAtUtc = now },
            new UserRecruitmentItemModel { UserId = userId, ItemKind = RecruitmentItemKind.Whiskey, Quantity = 1, UpdatedAtUtc = now });
        await _db.SaveChangesAsync();
    }

    private static IReadOnlyList<RecruitmentBoostDto> BuildRecruitmentBoosts(
        int baseSeconds,
        int requiredSeconds,
        int participantCount,
        RecruitmentItemKind itemKind)
    {
        var boosts = new List<RecruitmentBoostDto>();
        var helperCount = Math.Clamp(participantCount - 1, 0, 4);
        var localPercent = helperCount switch
        {
            1 => 15,
            2 => 25,
            3 => 35,
            >= 4 => 45,
            _ => 0
        };

        if (localPercent > 0)
        {
            boosts.Add(new RecruitmentBoostDto($"{participantCount} participants", 0, localPercent));
        }

        if (itemKind != RecruitmentItemKind.None)
        {
            var label = itemKind switch
            {
                RecruitmentItemKind.Chips => "Chips",
                RecruitmentItemKind.Beer => "Beer",
                RecruitmentItemKind.Whiskey => "Whiskey",
                _ => itemKind.ToString()
            };
            boosts.Add(new RecruitmentBoostDto(label, Math.Max(0, baseSeconds - requiredSeconds), 0));
        }

        return boosts;
    }
}

