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
    private Dictionary<string, (TwitchClient Client, List<object> Listeners)> _clients = new();

    private ILogger<TwitchChatService> _logger;
    public TwitchChatService(ILogger<TwitchChatService> logger) => _logger = logger;

    public TwitchClient GetClient(TwitchAuthModel authModel, object listener, string channel)
    {
        TwitchClient client;

        lock (_clients)
        {
            if (_clients.ContainsKey(authModel.UserId))
            {
                if (!_clients[authModel.UserId].Listeners.Contains(listener))
                    _clients[authModel.UserId].Listeners.Add(listener);
                return _clients[authModel.UserId].Client;
            }

            ConnectionCredentials credentials = new ConnectionCredentials(authModel.Username, authModel.AccessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, channel);

            client.OnConnected += (_, _) => _logger.LogInformation($"[{authModel.Username}] Connected");
            client.OnJoinedChannel += (_, ev) => _logger.LogInformation($"[{authModel.Username}] Channel {ev.Channel} joined");
            client.OnLeftChannel += (_, ev) => _logger.LogWarning($"[{authModel.Username}] Channel {ev.Channel} left");
            client.OnDisconnected += (_, _) => _logger.LogInformation($"[{authModel.Username}] Disconnected");

            client.Connect();

            _clients[authModel.UserId] = (client, new() { listener });
        }

        return client;
    }

    public void Unlisten(TwitchAuthModel authModel, object listener)
    {
        lock(_clients)
        {
            if (_clients.ContainsKey(authModel.UserId))
            {
                if (_clients[authModel.UserId].Listeners.Contains(listener))
                    _clients[authModel.UserId].Listeners.Remove(listener);

                if (_clients[authModel.UserId].Listeners.Count == 0)
                {
                    _clients[authModel.UserId].Client.Disconnect();
                    _clients.Remove(authModel.UserId);
                }
            }
        }
    }

    public void Dispose()
    {
        lock(_clients)
        {
            foreach (var kv in _clients)
                kv.Value.Client.Disconnect();
            _clients.Clear();
        }
    }
}
