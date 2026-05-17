using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side interface IRecruitmentService.</summary>
public interface IRecruitmentService
{
    /// <summary>Gets encounter candidates near the user location.</summary>
    /// <param name="userId">Current user identifier.</param>
    /// <param name="lat">Current latitude in decimal degrees.</param>
    /// <param name="lng">Current longitude in decimal degrees.</param>
    /// <param name="radiusMeters">Search radius in meters.</param>
    /// <returns>Nearby encounter DTOs.</returns>
    Task<IReadOnlyList<PlayerEncounterDto>> GetEncountersAsync(string userId, double lat, double lng, double radiusMeters);

    /// <summary>Attempts to recruit a player encounter for the user.</summary>
    /// <param name="userId">Current user identifier.</param>
    /// <param name="request">Recruitment attempt request payload.</param>
    /// <param name="userPosition">Validated user position used for anti-spoof checks.</param>
    /// <returns>Recruitment outcome details.</returns>
    Task<RecruitmentAttemptResultDto> AttemptRecruitmentAsync(string userId, RecruitmentAttemptRequest request, GeoPosition userPosition);
}

