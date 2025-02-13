namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using Kanawanagasaki.TwitchHub.Models;
using Newtonsoft.Json;

public class TwitchApiService
{
    private IConfiguration _conf;
    private ILogger<TwitchApiService> _logger;

    private HttpClient _http;

    public TwitchApiService(IConfiguration conf, ILogger<TwitchApiService> logger)
    {
        _conf = conf;
        _logger = logger;
        _http = new()
        {
            BaseAddress = new Uri("https://api.twitch.tv")
        };
        _http.DefaultRequestHeaders.Add("Client-Id", conf["Twitch:ClientId"]);
    }

    public async Task<TwitchGetUsersResponse?> GetUser(string accessToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            return obj?.data.FirstOrDefault();
        }
        else return null;
    }

    private ConcurrentDictionary<string, TwitchGetUsersResponse> _getUserByIdCache = new();
    public async Task<TwitchGetUsersResponse?> GetUser(string accessToken, string userid, bool ignoreCache = false)
    {
        if (!ignoreCache && _getUserByIdCache.TryGetValue(userid, out var cachedUser))
            return cachedUser;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users?id=" + userid);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            var ret = obj?.data.FirstOrDefault();
            if (ret is not null)
                _getUserByIdCache[userid] = ret;
            return ret;
        }
        else return null;
    }
    public async Task<TwitchGetUsersResponse?> GetUserByLogin(string accessToken, string login, bool ignoreCache = false)
    {
        if (!ignoreCache && _getUserByIdCache.TryGetValue(login, out var cachedUser))
            return cachedUser;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users?login=" + login);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            var ret = obj?.data.FirstOrDefault();
            if (ret is not null)
                _getUserByIdCache[login] = ret;
            return ret;
        }
        else return null;
    }

    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>?> GetChannelBadges(string accessToken, string channelid)
    {
        var url = $"https://api.twitch.tv/helix/chat/badges?broadcaster_id={channelid}";
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetChatBadgeResponse>>(json);
        }
        else return null;
    }

    private TwitchDataResponse<TwitchGetChatBadgeResponse>? _getGlobalBadgesCache = null;
    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>?> GetGlobalBadges(string accessToken)
    {
        if (_getGlobalBadgesCache is not null)
            return _getGlobalBadgesCache;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/chat/badges/global");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _getGlobalBadgesCache = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetChatBadgeResponse>>(json);
            return _getGlobalBadgesCache;
        }
        else return null;
    }

    public async Task<int> GetFollowersCount(string accessToken, string userid)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        using var response = await http.GetAsync("https://api.twitch.tv/helix/users/follows?to_id=" + userid);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<TwitchPaginatedDataResponse<object>>(json);
            return data?.total ?? -1;
        }
        else return -1;
    }

    public async Task<bool> Timeout(string accessToken, string broadcasterId, string moderatorId, string viewerId, TimeSpan duration, string reason)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        using var response = await http.PostAsJsonAsync($"https://api.twitch.tv/helix/moderation/bans?broadcaster_id={broadcasterId}&moderator_id={moderatorId}",
            new
            {
                data = new
                {
                    user_id = viewerId,
                    duration = (int)duration.TotalSeconds,
                    reason
                }
            });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> EventSubSubscribe(string accessToken, string type, string version, object condition, string wsSessionId)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        using var response = await http.PostAsJsonAsync($"https://api.twitch.tv/helix/eventsub/subscriptions",
            new
            {
                type,
                version,
                condition,
                transport = new
                {
                    method = "websocket",
                    session_id = wsSessionId
                }
            });

        if (!response.IsSuccessStatusCode)
            _logger.LogError("Failed to subscribe to {SubType} - {RetCode}\n{ResponseStr}", type, $"{(int)response.StatusCode} {response.StatusCode}", await response.Content.ReadAsStringAsync());

        return response.IsSuccessStatusCode;
    }

    public Task<TwitchApiResponse<TwitchCustomReward>?> GetCustomRewardsList(TwitchAuthModel auth)
            => Get<TwitchCustomReward>
            (
                auth,
                $"helix/channel_points/custom_rewards?broadcaster_id={auth.UserId}&only_manageable_rewards=true"
            );

    public Task<TwitchApiResponse<TwitchCustomReward>?> CreateCustomReward(TwitchAuthModel auth, CustomRewardReq req)
        => Post<TwitchCustomReward>
        (
            auth,
            $"helix/channel_points/custom_rewards?broadcaster_id=" + auth.UserId,
            req
        );

    public Task<TwitchApiResponse<TwitchCustomReward>?> UpdateCustomReward(TwitchAuthModel auth, string rewardId, CustomRewardReq req)
        => Patch<TwitchCustomReward>
        (
            auth,
            $"helix/channel_points/custom_rewards?broadcaster_id={auth.UserId}&id={rewardId}",
            req
        );

    public Task<int> DeleteCustomReward(TwitchAuthModel auth, string rewardId)
        => Delete(auth, $"helix/channel_points/custom_rewards?broadcaster_id={auth.UserId}&id={rewardId}");

    public Task<TwitchApiResponse<Redemption>?> GetRedemption(TwitchAuthModel auth, string rewardId, string redemptionId)
       => Get<Redemption>
       (
           auth,
           $"helix/channel_points/custom_rewards/redemptions?broadcaster_id={auth.UserId}&reward_id={rewardId}&status=UNFULFILLED&id={redemptionId}"
       );

    public Task<TwitchApiResponse<Redemption>?> GetFirstUnfulfilledRedemption(TwitchAuthModel auth, string rewardId)
        => Get<Redemption>
        (
            auth,
            $"helix/channel_points/custom_rewards/redemptions?broadcaster_id={auth.UserId}&reward_id={rewardId}&status=UNFULFILLED"
        );

    public Task<TwitchApiResponse<Redemption>?> UpdateRedemptionStatus(TwitchAuthModel auth, string rewardId, string redemptionId, ERedemptionStatus status)
        => Patch<Redemption>
        (
            auth,
            $"helix/channel_points/custom_rewards/redemptions?broadcaster_id={auth.UserId}&reward_id={rewardId}&id={redemptionId}",
            new
            {
                status = status switch
                {
                    ERedemptionStatus.FULFILLED => "FULFILLED",
                    ERedemptionStatus.UNFULFILLED => "UNFULFILLED",
                    ERedemptionStatus.CANCELED => "CANCELED",
                    _ => "Bruh, wtf, how this happened?"
                }
            }
        );

    private async Task<TwitchApiResponse<T>?> Get<T>(TwitchAuthModel auth, string uri) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Authorization", "Bearer " + auth.AccessToken);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return new() { StatusCode = (int)response.StatusCode };

        var res = await response.Content.ReadFromJsonAsync<TwitchApiResponse<T>>();
        if (res is null)
            return null;

        res.StatusCode = (int)response.StatusCode;
        return res;
    }

    private async Task<TwitchApiResponse<T>?> Post<T>(TwitchAuthModel auth, string uri, object json) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Authorization", "Bearer " + auth.AccessToken);
        request.Content = JsonContent.Create(json);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return new() { StatusCode = (int)response.StatusCode, ResponseStr = await response.Content.ReadAsStringAsync() };

        var res = await response.Content.ReadFromJsonAsync<TwitchApiResponse<T>>();
        if (res is null)
            return null;

        res.StatusCode = (int)response.StatusCode;
        return res;
    }

    private async Task<TwitchApiResponse<T>?> Patch<T>(TwitchAuthModel auth, string uri, object json) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri);
        request.Headers.Add("Authorization", "Bearer " + auth.AccessToken);
        request.Content = JsonContent.Create(json);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return new() { StatusCode = (int)response.StatusCode };

        var res = await response.Content.ReadFromJsonAsync<TwitchApiResponse<T>>();
        if (res is null)
            return null;

        res.StatusCode = (int)response.StatusCode;
        return res;
    }

    private async Task<int> Delete(TwitchAuthModel auth, string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Add("Authorization", "Bearer " + auth.AccessToken);
        using var response = await _http.SendAsync(request);
        return (int)response.StatusCode;
    }
}

public record TwitchGetChatBadgeVersionResponse(string id, string image_url_1x, string image_url_2x, string image_url_4x);
public record TwitchGetChatBadgeResponse(string set_id, TwitchGetChatBadgeVersionResponse[] versions);
public record TwitchGetUsersResponse(
    string broadcaster_type,
    string description,
    string display_name,
    string id,
    string login,
    string offline_image_url,
    string profile_image_url,
    string type,
    string view_count,
    string email,
    string created_at);

public record TwitchPaginatedDataResponse<T>(int total, T[] data);
public record TwitchDataResponse<T>(T[] data);
public class TwitchApiResponse<T> where T : class
{
    public int StatusCode { get; set; }
    public T[] data { get; init; } = [];
    public string? ResponseStr { get; set; }

    public bool IsSuccessStatusCode => 200 <= StatusCode && StatusCode < 300;
}

public class TwitchCustomReward
{
    public required string id { get; init; }

    public string? broadcaster_id { get; init; }
    public string? broadcaster_login { get; init; }
    public string? broadcaster_name { get; init; }

    public Dictionary<string, string>? image { get; init; }
    public Dictionary<string, string>? default_image { get; init; }

    public string? background_color { get; set; }

    public string? title { get; set; }
    public int cost { get; set; }
    public bool is_enabled { get; set; }
    public bool is_user_input_required { get; set; }
    public string? prompt { get; set; }

    public bool is_paused { get; init; }
    public bool is_in_stock { get; init; }
    public bool should_redemptions_skip_request_queue { get; init; }

    public CustomRewardReq ToReq()
        => new()
        {
            title = title ?? "TITLE IS NULL AAA; " + Random.Shared.Next(1, 10_000),
            cost = cost,
            is_user_input_required = is_user_input_required,
            prompt = prompt,
            is_enabled = is_enabled,
            is_paused = is_paused,
            background_color = background_color,
            is_max_per_stream_enabled = max_per_user_per_stream_setting?.is_enabled ?? false,
            max_per_stream = max_per_stream_setting?.max_per_stream ?? 9999,
            is_max_per_user_per_stream_enabled = max_per_user_per_stream_setting?.is_enabled ?? false,
            max_per_user_per_stream = max_per_user_per_stream_setting?.max_per_user_per_stream ?? 1,
            is_global_cooldown_enabled = global_cooldown_setting?.is_enabled ?? true,
            global_cooldown_seconds = global_cooldown_setting?.global_cooldown_seconds ?? 900,
            should_redemptions_skip_request_queue = false
        };

    public TwitchCustomRewardMaxPerStreamSetting? max_per_stream_setting { get; init; }
    public TwitchCustomRewardMaxPerUserPerStreamSetting? max_per_user_per_stream_setting { get; init; }
    public TwitchCustomRewardGlobalCooldownSetting? global_cooldown_setting { get; init; }
}
// 💀
public record TwitchCustomRewardMaxPerStreamSetting(bool is_enabled, int max_per_stream);
public record TwitchCustomRewardMaxPerUserPerStreamSetting(bool is_enabled, int max_per_user_per_stream);
public record TwitchCustomRewardGlobalCooldownSetting(bool is_enabled, int global_cooldown_seconds);

public class Redemption
{
    public required string id { get; init; }
    public required string user_login { get; init; }
    public required string user_id { get; init; }
    public required string user_name { get; init; }
    public required string user_input { get; init; }
    public required string redeemed_at { get; init; }

    public ERewardType reward_type { get; set; }
    public Guid? bot_auth_uuid { get; set; }
    public string? extra { get; set; }
}

public class CustomRewardReq
{
    public required string title { get; set; }
    public required int cost { get; set; }
    public bool is_user_input_required { get; set; } = false;
    public string? prompt { get; set; }
    public bool is_enabled { get; set; } = true;
    public bool is_paused { get; set; } = false;

    public string? background_color { get; set; }

    public bool is_max_per_stream_enabled { get; set; } = false;
    public int max_per_stream { get; set; } = 9999;

    public bool is_max_per_user_per_stream_enabled { get; set; } = false;
    public int max_per_user_per_stream { get; set; } = 1;

    public bool is_global_cooldown_enabled { get; set; } = false;
    public int global_cooldown_seconds { get; set; } = 900;

    public bool should_redemptions_skip_request_queue { get; set; } = false;
}

public enum ERedemptionStatus
{
    FULFILLED, UNFULFILLED, CANCELED
}