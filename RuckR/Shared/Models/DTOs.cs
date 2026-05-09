using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    public sealed record ChallengeRequest(string OpponentUsername, int SelectedPlayerId);

    public sealed record AcceptChallengeRequest(int SelectedPlayerId);

    public sealed record CreatePitchRequest(
        [Required, MaxLength(200)] string Name,
        [Range(-90.0, 90.0)] double Latitude,
        [Range(-180.0, 180.0)] double Longitude,
        [Required] string Type);

    public sealed record ChallengeNotification(
        string ChallengerUsername,
        string PlayerName,
        string PlayerPosition,
        string PlayerRarity,
        int ChallengeId);

    public sealed record BattleResult(
        string WinnerUsername,
        string LoserUsername,
        string WinnerPlayerName,
        string LoserPlayerName,
        string Method,
        DateTime CreatedAt);

    public sealed record NearbyPlayerDto(
        int PlayerId,
        string Name,
        string Position,
        string Rarity,
        double FuzzyDistanceMeters,
        string OwnerUsername);

    public sealed record CapturePlayerRequest(
        [Required] int PlayerId,
        [Required] int PitchId);

    public sealed record CaptureEligibilityDto(
        bool CanCapture,
        string Reason,
        string DistanceBucket,
        double? AccuracyMeters,
        int AvailablePlayerCount);

    public sealed record PlayerEncounterDto(
        Guid EncounterId,
        int PlayerId,
        string Name,
        string Position,
        string Rarity,
        int Level,
        double Latitude,
        double Longitude,
        DateTime ExpiresAtUtc,
        int SuccessChancePercent);

    public sealed record RecruitmentAttemptRequest(
        [Required] Guid EncounterId,
        [Required] int PlayerId);

    public sealed record RecruitmentAttemptResultDto(
        bool Success,
        int SuccessChancePercent,
        string Message,
        CollectionModel? Collection);
}
