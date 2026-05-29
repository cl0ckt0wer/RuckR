using System;
using System.ComponentModel.DataAnnotations;

namespace RuckR.Shared.Models
{
    /// <summary>
    /// Request payload used to challenge another user.
    /// </summary>
    /// <param name="OpponentUsername">Username of the challenged user.</param>
    /// <param name="IdempotencyKey">Optional idempotency key for retriable requests.</param>
    public sealed record ChallengeRequest(
        string OpponentUsername,
        [MaxLength(36)] string? IdempotencyKey = null);

    /// <summary>
    /// Request payload used to accept a battle challenge.
    /// </summary>
    public sealed record AcceptChallengeRequest;

    /// <summary>
    /// Request payload used to submit a recruit and hidden rugby play for an accepted battle.
    /// </summary>
    /// <param name="PlayerId">Owned recruit/player-card id selected for the battle.</param>
    /// <param name="Move">Hidden rugby play selected by the current user.</param>
    public sealed record BattleSelectionRequest(int PlayerId, BattleMove Move);

    /// <summary>
    /// Recruit/player-card details embedded in battle summaries.
    /// </summary>
    public sealed record BattlePlayerSummaryDto(
        int PlayerId,
        string Name,
        string Position,
        string Rarity,
        int Level,
        int Speed,
        int Strength,
        int Agility,
        int Kicking);

    /// <summary>
    /// Battle details shaped for user-facing challenge, result, and history views.
    /// </summary>
    public sealed record BattleSummaryDto(
        int Id,
        BattleStatus Status,
        string ChallengerId,
        string ChallengerUsername,
        string OpponentId,
        string OpponentUsername,
        int? ChallengerPlayerId,
        int? OpponentPlayerId,
        BattlePlayerSummaryDto? ChallengerPlayer,
        BattlePlayerSummaryDto? OpponentPlayer,
        string? WinnerId,
        string? WinnerUsername,
        BattleResult? Result,
        DateTime CreatedAt,
        DateTime? ResolvedAt,
        [MaxLength(36)] string? IdempotencyKey = null,
        DateTime? AcceptedAt = null,
        bool ChallengerSubmitted = false,
        bool OpponentSubmitted = false,
        BattleMove? ChallengerMove = null,
        BattleMove? OpponentMove = null,
        DateTime? ChallengerSubmittedAt = null,
        DateTime? OpponentSubmittedAt = null,
        double? ChallengerScore = null,
        double? OpponentScore = null);

    /// <summary>
    /// Request payload used to create a pitch manually.
    /// </summary>
    /// <param name="Name">Pitch display name.</param>
    /// <param name="Latitude">Pitch latitude in degrees.</param>
    /// <param name="Longitude">Pitch longitude in degrees.</param>
    /// <param name="Type">Pitch type enum string value.</param>
    public sealed record CreatePitchRequest(
        [Required, MaxLength(200)] string Name,
        [Range(-90.0, 90.0)] double Latitude,
        [Range(-180.0, 180.0)] double Longitude,
        [Required] string Type);

    /// <summary>
    /// Place candidate information returned from discovery providers.
    /// </summary>
    /// <param name="PlaceId">Provider place identifier.</param>
    /// <param name="Name">Place name.</param>
    /// <param name="Latitude">Latitude in degrees.</param>
    /// <param name="Longitude">Longitude in degrees.</param>
    /// <param name="DistanceMeters">Distance from request location in meters.</param>
    /// <param name="CategoryLabel">Category label from provider.</param>
    /// <param name="RecommendedPitchType">Suggested pitch type.</param>
    /// <param name="MatchReason">Reason this place was suggested.</param>
    /// <param name="Confidence">Provider confidence score.</param>
    public sealed record PitchCandidatePlaceDto(
        string PlaceId,
        string Name,
        double Latitude,
        double Longitude,
        double DistanceMeters,
        string CategoryLabel,
        string RecommendedPitchType,
        string MatchReason,
        int Confidence);

    /// <summary>
    /// Player-facing hub state for an interactable pitch.
    /// </summary>
    public sealed record PitchHubDto(
        int PitchId,
        string Name,
        string Type,
        double Latitude,
        double Longitude,
        string Source,
        int? SourceConfidence,
        double DistanceMeters,
        string DistanceBucket,
        bool CanInteract,
        string Reason,
        int ActiveRecruitCount,
        int ChallengeableUserCount);

    /// <summary>
    /// Request payload used to create a pitch from a discovered candidate.
    /// </summary>
    /// <param name="Name">Pitch display name.</param>
    /// <param name="PlaceId">Provider place identifier.</param>
    /// <param name="Latitude">Latitude in degrees.</param>
    /// <param name="Longitude">Longitude in degrees.</param>
    /// <param name="Type">Pitch type value.</param>
    /// <param name="CategoryLabel">Optional category label.</param>
    /// <param name="MatchReason">Optional place match reason.</param>
    /// <param name="Confidence">Source confidence score.</param>
    public sealed record CreatePitchFromCandidateRequest(
        [Required, MaxLength(200)] string Name,
        [Required] string PlaceId,
        [Range(-90.0, 90.0)] double Latitude,
        [Range(-180.0, 180.0)] double Longitude,
        [Required] string Type,
        [MaxLength(200)] string? CategoryLabel,
        [MaxLength(200)] string? MatchReason,
        [Range(0, 100)] int Confidence);

    /// <summary>
    /// Notification payload for a battle challenge.
    /// </summary>
    /// <param name="ChallengerUsername">Name of challenger.</param>
    /// <param name="ChallengeId">Challenge identifier.</param>
    public sealed record ChallengeNotification(
        string ChallengerUsername,
        int ChallengeId);

    /// <summary>
    /// Battle outcome details returned after completion.
    /// </summary>
    /// <param name="WinnerUsername">Username of the winner.</param>
    /// <param name="LoserUsername">Username of the loser.</param>
    /// <param name="WinnerPlayerName">Winning recruit name.</param>
    /// <param name="LoserPlayerName">Losing recruit name.</param>
    /// <param name="Method">How the battle concluded.</param>
    /// <param name="CreatedAt">Result creation timestamp.</param>
    /// <param name="WinnerMove">Winning hidden rugby play.</param>
    /// <param name="LoserMove">Losing hidden rugby play.</param>
    /// <param name="WinnerScore">Winning score after recruit and play bonuses.</param>
    /// <param name="LoserScore">Losing score after recruit and play bonuses.</param>
    /// <param name="ChallengerMove">Challenger hidden rugby play.</param>
    /// <param name="OpponentMove">Opponent hidden rugby play.</param>
    /// <param name="ChallengerScore">Challenger score after recruit and play bonuses.</param>
    /// <param name="OpponentScore">Opponent score after recruit and play bonuses.</param>
    public sealed record BattleResult(
        string WinnerUsername,
        string LoserUsername,
        string WinnerPlayerName,
        string LoserPlayerName,
        string Method,
        DateTime CreatedAt,
        BattleMove? WinnerMove = null,
        BattleMove? LoserMove = null,
        double? WinnerScore = null,
        double? LoserScore = null,
        BattleMove? ChallengerMove = null,
        BattleMove? OpponentMove = null,
        double? ChallengerScore = null,
        double? OpponentScore = null);

    /// <summary>
    /// Nearby user listing item used by battle challenge target discovery.
    /// </summary>
    public sealed record NearbyUserDto(
        string UserId,
        string Username,
        string DisplayName,
        string? AvatarUrl,
        string? Biography,
        DistanceBucket DistanceBucket,
        int LastSeenSecondsAgo,
        int RecruitCount);

    /// <summary>
    /// Nearby recruit listing item used by discovery endpoints.
    /// </summary>
    /// <param name="PlayerId">Recruit/player-card identifier.</param>
    /// <param name="Name">Display name.</param>
    /// <param name="Position">Recruit rugby position.</param>
    /// <param name="Rarity">Recruit rarity.</param>
    /// <param name="DistanceBucket">Distance bucket classification.</param>
    /// <param name="OwnerUsername">User/account username when already captured.</param>
    /// <param name="Source">How this recruit was found.</param>
    public sealed record NearbyPlayerDto(
        int PlayerId,
        string Name,
        string Position,
        string Rarity,
        DistanceBucket DistanceBucket,
        string? OwnerUsername,
        NearbyRecruitSource Source = NearbyRecruitSource.WildSpawn);

    /// <summary>
    /// Request payload to capture a recruit/player-card.
    /// </summary>
    /// <param name="PlayerId">Recruit/player-card identifier to capture.</param>
    /// <param name="PitchId">Pitch used for capture attempt.</param>
    public sealed record CapturePlayerRequest(
        [Required] int PlayerId,
        [Required] int PitchId);

    /// <summary>
    /// Eligibility response for capture requests.
    /// </summary>
    /// <param name="CanCapture">Whether capture is currently allowed.</param>
    /// <param name="Reason">Failure or success reason.</param>
    /// <param name="DistanceBucket">Current distance bucket from pitch/player.</param>
    /// <param name="AccuracyMeters">Optional GPS accuracy in meters.</param>
    /// <param name="AvailablePlayerCount">Remaining nearby recruits available to catch.</param>
    public sealed record CaptureEligibilityDto(
        bool CanCapture,
        string Reason,
        string DistanceBucket,
        double? AccuracyMeters,
        int AvailablePlayerCount);

    /// <summary>
    /// Data for an active encounter.
    /// </summary>
    /// <param name="EncounterId">Encounter identifier.</param>
    /// <param name="PlayerId">Recruit/player-card identifier.</param>
    /// <param name="Name">Recruit name.</param>
    /// <param name="Position">Recruit position.</param>
    /// <param name="Rarity">Recruit rarity.</param>
    /// <param name="Level">Recruit level.</param>
    /// <param name="Latitude">Encounter latitude.</param>
    /// <param name="Longitude">Encounter longitude.</param>
    /// <param name="ExpiresAtUtc">Expiration timestamp.</param>
    /// <param name="SuccessChancePercent">Legacy success display value retained for older clients.</param>
    /// <param name="BaseRecruitmentSeconds">Base time required to recruit before social/item reductions.</param>
    /// <param name="ParkName">Optional park name.</param>
    /// <param name="ParkPlaceId">Optional external place identifier.</param>
    /// <param name="ParticipantCount">Number of users who explicitly joined the shared recruitment.</param>
    /// <param name="CurrentUserJoined">Whether the current user joined this shared recruitment.</param>
    /// <param name="GroupItemKind">The one group item applied to the shared timer.</param>
    /// <param name="RecruitmentStartedAtUtc">UTC time when the shared timer started.</param>
    /// <param name="RecruitmentCompletesAtUtc">UTC time when the shared timer can be claimed.</param>
    /// <param name="RequiredRecruitmentSeconds">Current shared timer duration after participant and item reductions.</param>
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
        int SuccessChancePercent,
        int BaseRecruitmentSeconds = 0,
        string? ParkName = null,
        string? ParkPlaceId = null,
        int ParticipantCount = 0,
        bool CurrentUserJoined = false,
        RecruitmentItemKind GroupItemKind = RecruitmentItemKind.None,
        DateTime? RecruitmentStartedAtUtc = null,
        DateTime? RecruitmentCompletesAtUtc = null,
        int RequiredRecruitmentSeconds = 0);

    /// <summary>
    /// Request payload for a recruitment attempt.
    /// </summary>
    /// <param name="EncounterId">Encounter identifier.</param>
    /// <param name="PlayerId">Recruit/player-card identifier being recruited.</param>
    /// <param name="ItemKind">Optional item to apply when starting a timed recruitment session.</param>
    /// <param name="Latitude">Optional current user latitude used when no recent SignalR position is available.</param>
    /// <param name="Longitude">Optional current user longitude used when no recent SignalR position is available.</param>
    /// <param name="Accuracy">Optional current GPS accuracy in meters.</param>
    public sealed record RecruitmentAttemptRequest(
        [Required] Guid EncounterId,
        [Required] int PlayerId,
        RecruitmentItemKind ItemKind = RecruitmentItemKind.None,
        double? Latitude = null,
        double? Longitude = null,
        double? Accuracy = null);

    /// <summary>
    /// One time reducer applied to an active recruitment session.
    /// </summary>
    /// <param name="Label">Player-facing reducer label.</param>
    /// <param name="SecondsSaved">Approximate seconds saved by this reducer.</param>
    /// <param name="PercentReduction">Percentage reduction applied by this reducer.</param>
    public sealed record RecruitmentBoostDto(
        string Label,
        int SecondsSaved,
        int PercentReduction);

    /// <summary>
    /// Recruitment item quantity for the current user.
    /// </summary>
    public sealed record RecruitmentItemDto(
        RecruitmentItemKind ItemKind,
        int Quantity);

    /// <summary>
    /// Current timed recruitment session for the signed-in user.
    /// </summary>
    public sealed record ActiveRecruitmentSessionDto(
        Guid EncounterId,
        int PlayerId,
        string PlayerName,
        string Position,
        string Rarity,
        DateTime StartedAtUtc,
        DateTime CompletesAtUtc,
        int BaseDurationSeconds,
        int RequiredDurationSeconds,
        int RemainingSeconds,
        int LocalPlayerCount,
        RecruitmentItemKind ItemKind,
        IReadOnlyList<RecruitmentBoostDto>? Boosts,
        int ParticipantCount = 0);

    /// <summary>
    /// Recruitment result payload.
    /// </summary>
    /// <param name="Success">Whether recruitment succeeded.</param>
    /// <param name="SuccessChancePercent">Legacy success display value retained for older clients.</param>
    /// <param name="Message">Outcome message.</param>
    /// <param name="Collection">Collection entry when recruitment completes successfully.</param>
    /// <param name="Completed">Whether the timed recruitment session is complete.</param>
    /// <param name="BaseDurationSeconds">Base duration before social and item reducers.</param>
    /// <param name="RequiredDurationSeconds">Actual duration after social and item reducers.</param>
    /// <param name="RemainingSeconds">Seconds remaining before completion is allowed.</param>
    /// <param name="CompletesAtUtc">Server-owned completion timestamp.</param>
    /// <param name="LocalPlayerCount">Nearby recruiters counted for the session.</param>
    /// <param name="ItemKind">Item consumed for this session.</param>
    /// <param name="Boosts">Time reducers applied to this session.</param>
    /// <param name="ParticipantCount">Number of joined users in this shared recruitment.</param>
    /// <param name="CurrentUserJoined">Whether the current user joined this shared recruitment.</param>
    /// <param name="AwardedUserCount">Number of participants awarded when the shared recruitment completed.</param>
    public sealed record RecruitmentAttemptResultDto(
        bool Success,
        int SuccessChancePercent,
        string Message,
        CollectionModel? Collection,
        bool Completed = false,
        int BaseDurationSeconds = 0,
        int RequiredDurationSeconds = 0,
        int RemainingSeconds = 0,
        DateTime? CompletesAtUtc = null,
        int LocalPlayerCount = 0,
        RecruitmentItemKind ItemKind = RecruitmentItemKind.None,
        IReadOnlyList<RecruitmentBoostDto>? Boosts = null,
        int ParticipantCount = 0,
        bool CurrentUserJoined = false,
        int AwardedUserCount = 0);

    /// <summary>
    /// Lightweight representation of player progression.
    /// </summary>
    /// <param name="Level">Current level.</param>
    /// <param name="Experience">Current experience points.</param>
    /// <param name="NextLevelExperience">Threshold for next level.</param>
    public sealed record GameProgressDto(
        int Level,
        int Experience,
        int NextLevelExperience);

    /// <summary>
    /// Current user recruitment profile, including progress, item inventory, and active session.
    /// </summary>
    public sealed record RecruitmentProfileDto(
        int Level,
        int Experience,
        int NextLevelExperience,
        IReadOnlyList<RecruitmentItemDto> Items,
        ActiveRecruitmentSessionDto? ActiveRecruitment);
}
