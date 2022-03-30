namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TwitchApiService
{
    private IConfiguration _conf;
    private TwitchAuthService _twAuth;

    public TwitchApiService(IConfiguration conf, TwitchAuthService twAuth)
    {
        _conf = conf;
        _twAuth = twAuth;
    }

    private TwitchGetUsersResponse _getUserCache = null;
    public async Task<TwitchGetUsersResponse> GetUser()
    {
        if(!_twAuth.IsAuthenticated) return null;
        if(_getUserCache is not null) return _getUserCache;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_twAuth.AccessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users");
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            _getUserCache = obj.data.FirstOrDefault();
            return _getUserCache;
        }
        else return null;
    }

    private ConcurrentDictionary<string, TwitchGetUsersResponse> _getUserByIdCache = new();
    public async Task<TwitchGetUsersResponse> GetUser(string userid, bool ignoreCache = false)
    {
        if(!_twAuth.IsAuthenticated) return null;

        if(!ignoreCache && _getUserByIdCache.TryGetValue(userid, out var cachedUser))
            return cachedUser;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_twAuth.AccessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users?id=" + userid);
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            var ret = obj.data.FirstOrDefault();
            _getUserByIdCache[userid] = ret;
            return ret;
        }
        else return null;
    }
    public async Task<TwitchGetUsersResponse> GetUserByLogin(string login, bool ignoreCache = false)
    {
        if(!_twAuth.IsAuthenticated) return null;

        if(!ignoreCache && _getUserByIdCache.TryGetValue(login, out var cachedUser))
            return cachedUser;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_twAuth.AccessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users?login=" + login);
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetUsersResponse>>(json);
            var ret = obj.data.FirstOrDefault();
            _getUserByIdCache[login] = ret;
            return ret;
        }
        else return null;
    }
    
    private TwitchDataResponse<TwitchGetChatBadgeResponse> _getChannelBadgesCache = null; 
    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>> GetChannelBadges()
    {
        if(_getChannelBadgesCache is not null)
            return _getChannelBadgesCache;

        var user = await GetUser();
        if(user is null) return null;

        var url = $"https://api.twitch.tv/helix/chat/badges?broadcaster_id={user.id}";
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _twAuth.AccessToken);
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync(url);
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _getChannelBadgesCache = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetChatBadgeResponse>>(json);
            return _getChannelBadgesCache;
        }
        else return null;
    }

    private TwitchDataResponse<TwitchGetChatBadgeResponse> _getGlobalBadgesCache = null; 
    public async Task<TwitchDataResponse<TwitchGetChatBadgeResponse>> GetGlobalBadges()
    {
        if(_getGlobalBadgesCache is not null)
            return _getGlobalBadgesCache;
        
        var user = await GetUser();
        if(user is null) return null;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _twAuth.AccessToken);
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/chat/badges/global");
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _getGlobalBadgesCache = JsonConvert.DeserializeObject<TwitchDataResponse<TwitchGetChatBadgeResponse>>(json);
            return _getGlobalBadgesCache;
        }
        else return null;
    }

    public async Task<int> GetFollowersCount(string userid)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_twAuth.AccessToken}");
        http.DefaultRequestHeaders.Add("Client-Id", _conf["Twitch:ClientId"]);
        var response = await http.GetAsync("https://api.twitch.tv/helix/users/follows?to_id=" + userid);
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<TwitchPaginatedDataResponse<object>>(json);
            return data.total;
        }
        else return -1;
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