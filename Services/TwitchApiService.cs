namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using System.Threading;
using Microsoft.ClearScript;
using Newtonsoft.Json;

public class TwitchApiService
{
    private IConfiguration _conf;

    public TwitchApiService(IConfiguration conf)
    {
        _conf = conf;
    }

    public async Task<TwitchGetUsersResponse> GetUser(string accessToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            return obj.data.FirstOrDefault();
        }
        else return null;
    }

    private ConcurrentDictionary<string, TwitchGetUsersResponse> _getUserByIdCache = new();
    public async Task<TwitchGetUsersResponse> GetUser(string accessToken, string userid, bool ignoreCache = false)
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
            var ret = obj.data.FirstOrDefault();
            _getUserByIdCache[userid] = ret;
            return ret;
        }
        else return null;
    }
    public async Task<TwitchGetUsersResponse> GetUserByLogin(string accessToken, string login, bool ignoreCache = false)
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
            var ret = obj.data.FirstOrDefault();
            _getUserByIdCache[login] = ret;
            return ret;
        }
        else return null;
    }

    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>> GetChannelBadges(string accessToken, string channelid)
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

    private TwitchDataResponse<TwitchGetChatBadgeResponse> _getGlobalBadgesCache = null;
    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>> GetGlobalBadges(string accessToken)
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
            return data.total;
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