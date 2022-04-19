namespace Kanawanagasaki.TwitchHub.Services;

using System.Net;
using System.Linq;
using System.Threading;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors;
using HtmlAgilityPack.CssSelectors.NetCore;
using Kanawanagasaki.TwitchHub.Data;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Microsoft.JSInterop;
using Kanawanagasaki.TwitchHub.Models;
using System.Collections.Concurrent;

public class TwitchChatService : IDisposable
{
    private ConcurrentDictionary<string, (TwitchClient Client, List<object> Listeners)> _clients = new();

    private ILogger<TwitchChatService> _logger;
    public TwitchChatService(ILogger<TwitchChatService> logger) => _logger = logger;

    public TwitchClient GetClient(TwitchAuthModel authModel, object listener, string channel)
    {
        if(_clients.TryGetValue(authModel.UserId, out var ret))
        {
            if(!ret.Listeners.Contains(listener))
                ret.Listeners.Add(listener);
            return ret.Client;
        }

        ConnectionCredentials credentials = new ConnectionCredentials(authModel.Username, authModel.AccessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new WebSocketClient(clientOptions);
        var client = new TwitchClient(customClient);
        client.Initialize(credentials, channel);

        client.OnConnected += (_, _) => _logger.LogInformation($"[{authModel.Username}] Connected");
        client.OnJoinedChannel += (_, ev) => _logger.LogInformation($"[{authModel.Username}] Channel {ev.Channel} joined");
        client.OnLeftChannel += (_, ev) => _logger.LogWarning($"[{authModel.Username}] Channel {ev.Channel} left");
        client.OnDisconnected += (_, _) => _logger.LogInformation($"[{authModel.Username}] Disconnected");
        
        client.Connect();

        _clients.TryAdd(authModel.UserId, (client, new() { listener }));
        
        return client;
    }

    public void Unlisten(TwitchAuthModel authModel, object listener)
    {
        if(_clients.TryGetValue(authModel.UserId, out var ret))
        {
            if(ret.Listeners.Contains(listener))
                ret.Listeners.Remove(listener);
            
            if(ret.Listeners.Count == 0)
                _clients.TryRemove(authModel.UserId, out _);
        }
    }

    public void Dispose()
    {
        foreach(var kv in _clients)
            kv.Value.Client.Disconnect();
        _clients.Clear();
    }
}
