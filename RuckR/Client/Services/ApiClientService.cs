using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

/// <summary>
/// Provides typed HTTP access to RuckR game-related API endpoints.
/// </summary>
public class ApiClientService
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClientService> _logger;

    /// <summary>
    /// Creates a new API client service.
    /// </summary>
    /// <param name="http">HTTP client configured for the server base URI.</param>
    /// <param name="logger">Logger used for API request diagnostics.</param>
    public ApiClientService(HttpClient http, ILogger<ApiClientService> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Players
    /// <summary>
    /// Gets players optionally filtered by query criteria.
    /// </summary>
    /// <param name="position">Optional in-game position filter.</param>
    /// <param name="rarity">Optional rarity filter.</param>
    /// <param name="name">Optional player name filter.</param>
    /// <returns>List of matching players.</returns>
    public async Task<List<PlayerModel>> GetPlayersAsync(string? position = null, string? rarity = null, string? name = null)
    {
        try
        {
            var query = BuildQueryString(new { position, rarity, name });
            return await _http.GetFromJsonAsync<List<PlayerModel>>($"api/players{query}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch players");
            return new();
        }
    }

    /// <summary>
    /// Gets a player by identifier.
    /// </summary>
    /// <param name="id">Recruit/player-card identifier.</param>
    /// <returns>Recruit details or <c>null</c> when not found.</returns>
    public async Task<PlayerModel?> GetPlayerAsync(int id)
    {
        return await _http.GetFromJsonAsync<PlayerModel>($"api/players/{id}");
    }

    /// <summary>
    /// Gets players near a coordinate.
    /// </summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lng">Longitude.</param>
    /// <param name="radius">Search radius in meters.</param>
    /// <returns>List of players within radius.</returns>
    public async Task<List<NearbyPlayerDto>> GetNearbyPlayersAsync(double lat, double lng, double radius = 10_000)
    {
        try
        {
            _logger.LogDebug("Fetching nearby users and recruits at ({Lat}, {Lng}) radius {Radius}", lat, lng, radius);
            return await _http.GetFromJsonAsync<List<NearbyPlayerDto>>(
                $"api/players/nearby?lat={lat}&lng={lng}&radius={radius}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch nearby users and recruits at ({Lat}, {Lng})", lat, lng);
            return new();
        }
    }

    /// <summary>
    /// Gets nearby challengeable users from recent live GPS state.
    /// </summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lng">Longitude.</param>
    /// <param name="radius">Search radius in meters.</param>
    /// <returns>List of nearby users with recruit counts.</returns>
    public async Task<List<NearbyUserDto>> GetNearbyUsersAsync(double lat, double lng, double radius = 10_000)
    {
        try
        {
            _logger.LogDebug("Fetching nearby users at ({Lat}, {Lng}) radius {Radius}", lat, lng, radius);
            return await _http.GetFromJsonAsync<List<NearbyUserDto>>(
                $"api/users/nearby?lat={lat}&lng={lng}&radius={radius}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch nearby users at ({Lat}, {Lng})", lat, lng);
            return new();
        }
    }

    // Pitches
    /// <summary>
    /// Gets nearby pitches.
    /// </summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lng">Longitude.</param>
    /// <param name="radius">Search radius in meters.</param>
    /// <returns>List of nearby pitches.</returns>
    public async Task<List<PitchModel>> GetNearbyPitchesAsync(double lat, double lng, double radius = 5_000)
    {
        try
        {
            _logger.LogDebug("Fetching nearby pitches at ({Lat}, {Lng}) radius {Radius}", lat, lng, radius);
            return await _http.GetFromJsonAsync<List<PitchModel>>(
                $"api/pitches/nearby?lat={lat}&lng={lng}&radius={radius}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch nearby pitches at ({Lat}, {Lng})", lat, lng);
            return new();
        }
    }

    /// <summary>
    /// Gets candidate pitch locations from discovery providers.
    /// </summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lng">Longitude.</param>
    /// <param name="radius">Search radius in meters.</param>
    /// <returns>Candidate places for creating a pitch.</returns>
    public async Task<List<PitchCandidatePlaceDto>> GetPitchCandidatePlacesAsync(double lat, double lng, double radius = 5_000)
    {
        try
        {
            _logger.LogDebug("Fetching pitch candidate places at ({Lat}, {Lng}) radius {Radius}", lat, lng, radius);
            return await _http.GetFromJsonAsync<List<PitchCandidatePlaceDto>>(
                $"api/pitches/place-candidates?lat={lat}&lng={lng}&radius={radius}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch pitch candidate places at ({Lat}, {Lng})", lat, lng);
            return new();
        }
    }

    /// <summary>
    /// Gets paginated pitches.
    /// </summary>
    /// <param name="page">1-based page index.</param>
    /// <param name="pageSize">Number of rows per page.</param>
    /// <returns>Page of pitches.</returns>
    public async Task<List<PitchModel>> GetPitchesAsync(int page = 1, int pageSize = 20)
    {
        return await _http.GetFromJsonAsync<List<PitchModel>>(
            $"api/pitches?page={page}&pageSize={pageSize}") ?? new();
    }

    /// <summary>
    /// Gets a pitch by identifier.
    /// </summary>
    /// <param name="id">Pitch identifier.</param>
    /// <returns>Pitch details or <c>null</c> when not found.</returns>
    public async Task<PitchModel?> GetPitchAsync(int id)
    {
        return await _http.GetFromJsonAsync<PitchModel>($"api/pitches/{id}");
    }

    /// <summary>
    /// Creates a new pitch from raw fields.
    /// </summary>
    /// <param name="name">Pitch name.</param>
    /// <param name="latitude">Latitude.</param>
    /// <param name="longitude">Longitude.</param>
    /// <param name="type">Pitch type.</param>
    /// <returns>Created pitch if successful.</returns>
    public async Task<PitchModel?> CreatePitchAsync(string name, double latitude, double longitude, string type)
    {
        var response = await _http.PostAsJsonAsync("api/pitches", new CreatePitchRequest(name, latitude, longitude, type));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PitchModel>();
    }

    /// <summary>
    /// Creates a new pitch from a location candidate.
    /// </summary>
    /// <param name="candidate">Candidate place description.</param>
    /// <param name="name">New pitch name.</param>
    /// <param name="type">Pitch type.</param>
    /// <returns>Created pitch if successful.</returns>
    public async Task<PitchModel?> CreatePitchFromCandidateAsync(
        PitchCandidatePlaceDto candidate,
        string name,
        string type)
    {
        var request = new CreatePitchFromCandidateRequest(
            name,
            candidate.PlaceId,
            candidate.Latitude,
            candidate.Longitude,
            type,
            candidate.CategoryLabel,
            candidate.MatchReason,
            candidate.Confidence);

        var response = await _http.PostAsJsonAsync("api/pitches/from-candidate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PitchModel>();
    }

    // Collection
    /// <summary>
    /// Gets the current user collection.
    /// </summary>
    /// <returns>List of collected players.</returns>
    public async Task<List<CollectionModel>> GetCollectionAsync()
    {
        return await _http.GetFromJsonAsync<List<CollectionModel>>("api/collection") ?? new();
    }

    /// <summary>
    /// Captures a player at a pitch.
    /// </summary>
    /// <param name="playerId">Captured recruit/player-card identifier.</param>
    /// <param name="pitchId">Pitch identifier.</param>
    /// <returns>Collection entry, if captured.</returns>
    public async Task<CollectionModel?> CapturePlayerAsync(int playerId, int pitchId)
    {
        var response = await _http.PostAsJsonAsync("api/collection/capture", new CapturePlayerRequest(playerId, pitchId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionModel>();
    }

    /// <summary>
    /// Toggles favorite status for a collection entry.
    /// </summary>
    /// <param name="collectionId">Collection entry identifier.</param>
    public async Task ToggleFavoriteAsync(int collectionId)
    {
        var response = await _http.PostAsync($"api/collection/{collectionId}/favorite", null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets capture eligibility for a pitch.
    /// </summary>
    /// <param name="pitchId">Pitch identifier.</param>
    /// <returns>Capture eligibility details or <c>null</c> on failure.</returns>
    public async Task<CaptureEligibilityDto?> GetCaptureEligibilityAsync(int pitchId)
    {
        try
        {
            return await _http.GetFromJsonAsync<CaptureEligibilityDto>($"api/collection/capture-eligibility/{pitchId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch capture eligibility for pitch {PitchId}", pitchId);
            return null;
        }
    }

    /// <summary>
    /// Gets nearby encounters for map/gameplay overlays.
    /// </summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lng">Longitude.</param>
    /// <param name="radius">Search radius in meters.</param>
    /// <returns>Nearby player encounter DTOs.</returns>
    public async Task<List<PlayerEncounterDto>> GetEncountersAsync(double lat, double lng, double radius = 300)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<PlayerEncounterDto>>(
                $"api/map/encounters?lat={lat}&lng={lng}&radius={radius}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch map encounters at ({Lat}, {Lng})", lat, lng);
            return new();
        }
    }

    /// <summary>
    /// Attempts player recruitment for an encounter.
    /// </summary>
    /// <param name="encounterId">Encounter identifier.</param>
    /// <param name="playerId">Selected recruit/player-card identifier.</param>
    /// <param name="itemKind">Optional item to apply when starting a new recruitment session.</param>
    /// <param name="position">Optional latest browser GPS position to avoid depending on SignalR timing.</param>
    /// <returns>Recruitment result details.</returns>
    public async Task<RecruitmentAttemptResultDto?> AttemptRecruitmentAsync(
        Guid encounterId,
        int playerId,
        RecruitmentItemKind itemKind = RecruitmentItemKind.None,
        GeoPosition? position = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/recruitment/attempt",
                new RecruitmentAttemptRequest(
                    encounterId,
                    playerId,
                    itemKind,
                    position?.Latitude,
                    position?.Longitude,
                    position?.Accuracy));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RecruitmentAttemptResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recruitment attempt failed for encounter {EncounterId}, player {PlayerId}", encounterId, playerId);
            return null;
        }
    }

    /// <summary>
    /// Gets user progression DTO used for profile and progression UI.
    /// </summary>
    /// <returns>Game progression summary.</returns>
    public async Task<GameProgressDto?> GetGameProgressAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<GameProgressDto>("api/recruitment/profile");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch game progression profile");
            return null;
        }
    }

    /// <summary>
    /// Gets the full recruitment profile, including progress, items, and active session.
    /// </summary>
    /// <returns>Recruitment profile summary.</returns>
    public async Task<RecruitmentProfileDto?> GetRecruitmentProfileAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<RecruitmentProfileDto>("api/recruitment/profile");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch recruitment profile");
            return null;
        }
    }

    // Battles
    /// <summary>
    /// Sends a new battle challenge.
    /// </summary>
    /// <param name="opponentUsername">Opponent user name.</param>
    /// <returns>Created battle summary.</returns>
    public async Task<BattleSummaryDto?> SendChallengeAsync(string opponentUsername)
    {
        var response = await _http.PostAsJsonAsync("api/battles/challenge", new ChallengeRequest(opponentUsername));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
    }

    /// <summary>
    /// Accepts a battle challenge.
    /// </summary>
    /// <param name="battleId">Challenge identifier.</param>
    /// <returns>Accepted battle summary.</returns>
    public async Task<BattleSummaryDto?> AcceptChallengeAsync(int battleId)
    {
        var response = await _http.PostAsJsonAsync($"api/battles/{battleId}/accept", new AcceptChallengeRequest());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
    }

    /// <summary>
    /// Submits a recruit and hidden RPSLS move for an accepted battle.
    /// </summary>
    /// <param name="battleId">Battle identifier.</param>
    /// <param name="playerId">Owned recruit/player-card identifier.</param>
    /// <param name="move">Hidden RPSLS move.</param>
    /// <returns>Updated battle summary.</returns>
    public async Task<BattleSummaryDto?> SubmitBattleSelectionAsync(int battleId, int playerId, BattleMove move)
    {
        var response = await _http.PostAsJsonAsync($"api/battles/{battleId}/selection", new BattleSelectionRequest(playerId, move));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BattleSummaryDto>();
    }

    /// <summary>
    /// Declines a battle challenge.
    /// </summary>
    /// <param name="battleId">Challenge identifier.</param>
    public async Task DeclineChallengeAsync(int battleId)
    {
        var response = await _http.PostAsync($"api/battles/{battleId}/decline", null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets pending battles for the current user.
    /// </summary>
    /// <returns>Pending battle list.</returns>
    public async Task<List<BattleSummaryDto>> GetPendingBattlesAsync()
    {
        return await _http.GetFromJsonAsync<List<BattleSummaryDto>>("api/battles/pending") ?? new();
    }

    /// <summary>
    /// Gets completed battle history for the current user.
    /// </summary>
    /// <returns>Battle history list.</returns>
    public async Task<List<BattleSummaryDto>> GetBattleHistoryAsync()
    {
        return await _http.GetFromJsonAsync<List<BattleSummaryDto>>("api/battles/history") ?? new();
    }

    // Helper
    private static string BuildQueryString(object? parameters)
    {
        if (parameters is null)
            return string.Empty;

        var props = parameters.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var parts = new List<string>(props.Length);

        foreach (var prop in props)
        {
            var value = prop.GetValue(parameters);
            if (value is null)
                continue;

            var str = value.ToString();
            if (string.IsNullOrEmpty(str))
                continue;

            parts.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(str)}");
        }

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }
}
