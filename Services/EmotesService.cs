namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;

public class EmotesService : BackgroundService
{
    public BttvEmote[] GlobalBttvEmotes { get; private set; } = Array.Empty<BttvEmote>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        GlobalBttvEmotes = await GetGlobalBttv();
    }

    public async Task<BttvEmote[]> GetGlobalBttv()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync($"https://api.betterttv.net/3/cached/emotes/global");
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BttvEmote[]>(json);
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