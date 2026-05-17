using RuckR.Shared.Models;

namespace RuckR.Server.Services;
/// <summary>Defines the server-side interface IRecruitmentService.</summary>
public interface IRecruitmentService
{
    Task<IReadOnlyList<PlayerEncounterDto>> GetEncountersAsync(string userId, double lat, double lng, double radiusMeters);

    Task<RecruitmentAttemptResultDto> AttemptRecruitmentAsync(string userId, RecruitmentAttemptRequest request, GeoPosition userPosition);
}

