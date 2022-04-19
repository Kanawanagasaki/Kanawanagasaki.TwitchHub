namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;

public class EmotesService
{
    private BttvEmote[] _globalBttvEmotes { get; set; } = null;

    public async Task<BttvEmote[]> GetGlobalBttv()
    {
        if(_globalBttvEmotes is not null)
            return _globalBttvEmotes;

        using var http = new HttpClient();
        var response = await http.GetAsync($"https://api.betterttv.net/3/cached/emotes/global");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _globalBttvEmotes = JsonConvert.DeserializeObject<BttvEmote[]>(json);
            return _globalBttvEmotes;
        }
        else return null;
    }

    private ConcurrentDictionary<string, ChannelBttvEmotesResponse> _getChannelBttvCache = new();
    public async Task<ChannelBttvEmotesResponse> GetChannelBttv(string broadcasterId)
    {
        if (_getChannelBttvCache.TryGetValue(broadcasterId, out var cached))
            return cached;

        using var http = new HttpClient();
        var response = await http.GetAsync($"https://api.betterttv.net/3/cached/users/twitch/{broadcasterId}");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ChannelBttvEmotesResponse>(json);
            _getChannelBttvCache[broadcasterId] = result;
            return result;
        }
        else return null;
    }

}

public record BttvEmote(string id, string code, string imageType);
public record ChannelBttvEmotesResponse(BttvEmote[] channelEmotes, BttvEmote[] sharedEmotes);