namespace Kanawanagasaki.TwitchHub.Services;

using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Kanawanagasaki.TwitchHub.Models;
using System.Reflection;

public class TwitchChatService(TwitchAuthService _twitchAuth, ILogger<TwitchChatService> _logger) : IDisposable
{
    private readonly Dictionary<string, (TwitchClient Client, HashSet<object> Listeners)> _clients = [];

    public TwitchClient GetClient(TwitchAuthModel authModel, object listener, string channel)
    {
        TwitchClient client;

        lock (_clients)
        {
            if (_clients.ContainsKey(authModel.UserId))
            {
                if (!_clients[authModel.UserId].Listeners.Contains(listener))
                    _clients[authModel.UserId].Listeners.Add(listener);
                _clients[authModel.UserId].Client.JoinChannel(channel);
                return _clients[authModel.UserId].Client;
            }

            var credentials = new ConnectionCredentials(authModel.Username, authModel.AccessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                ReconnectionPolicy = null
            };
            var customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, channel);

            client.OnConnected += (_, _) =>
            {
                _logger.LogInformation("[{authModel.Username}] Connected", authModel.Username);
                foreach (var x in client.JoinedChannels)
                    client.JoinChannel(x.Channel);
            };
            client.OnJoinedChannel += (_, ev) => _logger.LogInformation("[{authModel.Username}] Channel {ev.Channel} joined", authModel.Username, ev.Channel);
            client.OnLeftChannel += (_, ev) => _logger.LogWarning("[{authModel.Username}] Channel {ev.Channel} left", authModel.Username, ev.Channel);
            client.OnDisconnected += async (_, _) =>
            {
                _logger.LogInformation("[{authModel.Username}] Disconnected", authModel.Username);
                var resAuth = await _twitchAuth.GetRestoredByUuid(authModel.Uuid);
                if (resAuth is not null)
                {
                    credentials = new(resAuth.Username, resAuth.AccessToken);
                    client.GetType().GetProperty(nameof(client.ConnectionCredentials))?.SetValue(client, credentials);
                }
            };

            client.Connect();

            _clients[authModel.UserId] = (client, new() { listener });
        }

        return client;
    }

    public void Unlisten(TwitchAuthModel authModel, object listener)
    {
        lock (_clients)
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
        lock (_clients)
        {
            foreach (var kv in _clients)
                kv.Value.Client.Disconnect();
            _clients.Clear();
        }
    }
}
