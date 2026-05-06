using System.Net.Http.Json;
using System.Reflection;
using RuckR.Shared.Models;

namespace RuckR.Client.Services;

public class ApiClientService
{
    private readonly HttpClient _http;

    public ApiClientService(HttpClient http)
    {
        _http = http;
    }

    // Players
    public async Task<List<PlayerModel>> GetPlayersAsync(string? position = null, string? rarity = null, string? name = null)
    {
        var query = BuildQueryString(new { position, rarity, name });
        return await _http.GetFromJsonAsync<List<PlayerModel>>($"players{query}") ?? new();
    }

    public async Task<PlayerModel?> GetPlayerAsync(int id)
    {
        return await _http.GetFromJsonAsync<PlayerModel>($"players/{id}");
    }

    public async Task<List<NearbyPlayerDto>> GetNearbyPlayersAsync(double lat, double lng, double radius = 10_000)
    {
        return await _http.GetFromJsonAsync<List<NearbyPlayerDto>>(
            $"players/nearby?lat={lat}&lng={lng}&radius={radius}") ?? new();
    }

    // Pitches
    public async Task<List<PitchModel>> GetNearbyPitchesAsync(double lat, double lng, double radius = 5_000)
    {
        return await _http.GetFromJsonAsync<List<PitchModel>>(
            $"pitches/nearby?lat={lat}&lng={lng}&radius={radius}") ?? new();
    }

    public async Task<List<PitchModel>> GetPitchesAsync(int page = 1, int pageSize = 20)
    {
        return await _http.GetFromJsonAsync<List<PitchModel>>(
            $"pitches?page={page}&pageSize={pageSize}") ?? new();
    }

    public async Task<PitchModel?> GetPitchAsync(int id)
    {
        return await _http.GetFromJsonAsync<PitchModel>($"pitches/{id}");
    }

    public async Task<PitchModel?> CreatePitchAsync(string name, double latitude, double longitude, string type)
    {
        var response = await _http.PostAsJsonAsync("pitches", new CreatePitchRequest(name, latitude, longitude, type));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PitchModel>();
    }

    // Collection
    public async Task<List<CollectionModel>> GetCollectionAsync()
    {
        return await _http.GetFromJsonAsync<List<CollectionModel>>("collection") ?? new();
    }

    public async Task<CollectionModel?> CapturePlayerAsync(int playerId, int pitchId)
    {
        var response = await _http.PostAsJsonAsync("collection/capture", new CapturePlayerRequest(playerId, pitchId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionModel>();
    }

    public async Task ToggleFavoriteAsync(int collectionId)
    {
        var response = await _http.PostAsync($"collection/{collectionId}/favorite", null);
        response.EnsureSuccessStatusCode();
    }

    // Battles
    public async Task<BattleModel?> SendChallengeAsync(string opponentUsername, int selectedPlayerId)
    {
        var response = await _http.PostAsJsonAsync("battles/challenge", new ChallengeRequest(opponentUsername, selectedPlayerId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BattleModel>();
    }

    public async Task<BattleModel?> AcceptChallengeAsync(int battleId, int selectedPlayerId)
    {
        var response = await _http.PostAsJsonAsync($"battles/{battleId}/accept", new AcceptChallengeRequest(selectedPlayerId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BattleModel>();
    }

    public async Task DeclineChallengeAsync(int battleId)
    {
        var response = await _http.PostAsync($"battles/{battleId}/decline", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<BattleModel>> GetPendingBattlesAsync()
    {
        return await _http.GetFromJsonAsync<List<BattleModel>>("battles/pending") ?? new();
    }

    public async Task<List<BattleModel>> GetBattleHistoryAsync()
    {
        return await _http.GetFromJsonAsync<List<BattleModel>>("battles/history") ?? new();
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
