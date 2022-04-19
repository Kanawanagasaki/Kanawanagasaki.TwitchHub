namespace Kanawanagasaki.TwitchHub.Components;

using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using TwitchLib.PubSub;

public partial class ChatComponent : ComponentBase, IDisposable
{
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchChatMessagesService TwChat { get; set; }
    [Inject]
    public EmotesService Emotes { get; set; }
    [Inject]
    public IJSRuntime Js { get; set; }
    [Inject]
    public ILogger<ChatComponent> Logger { get; set; }
    [Inject]
    public HelperService Helper { get; set; }
    [Inject]
    public SQLiteContext Db { get; set; }

    [Parameter]
    public string Channel { get; set; }
    private string _channelCache = "";
    private TwitchGetUsersResponse _channelObj;

    [Parameter]
    public TwitchAuthModel AuthModel { get; set; }

    public BttvEmote[] BttvEmotes { get; private set; } = Array.Empty<BttvEmote>();

    public Dictionary<string, string> Badges = new();

    private List<ProcessedChatMessage> _messages = new();
    private List<ChatMessageComponent> _components = new();

    private TwitchPubSub _pubSubClient;

    protected override void OnInitialized()
    {
        TwChat.OnMessage += OnMessage;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;
        if (AuthModel is null) return;
        if (!AuthModel.IsValid) return;

        _channelObj = await TwApi.GetUserByLogin(AuthModel.AccessToken, Channel);
        StateHasChanged();

        if (Channel == _channelCache) return;

        Logger.LogDebug($"Connecting to {Channel} with {AuthModel.Username} account");
        TwChat.Connect(AuthModel, Channel);

        var globalBadges = await TwApi.GetGlobalBadges(AuthModel.AccessToken);
        Badges.Clear();
        if(globalBadges is not null)
        {
            foreach (var badge in globalBadges.data)
            {
                var version = badge.versions.OrderByDescending(v => int.TryParse(v.id, out var id) ? id : 0).First();
                Badges[badge.set_id] = version.image_url_1x;
            }
        }

        if (_channelObj is not null)
        {
            var channelBadges = await TwApi.GetChannelBadges(AuthModel.AccessToken, _channelObj.id);
            foreach (var badge in channelBadges.data)
            {
                var version = badge.versions.OrderByDescending(v => int.TryParse(v.id, out var id) ? id : 0).First();
                Badges[badge.set_id] = version.image_url_1x;
            }

            var res = await Emotes.GetChannelBttv(_channelObj.id);
            if (res is not null)
            {
                if (res.channelEmotes is not null && res.sharedEmotes is not null)
                    BttvEmotes = res.channelEmotes.Concat(res.sharedEmotes).ToArray();
                else if (res.channelEmotes is not null)
                    BttvEmotes = res.channelEmotes;
                else if (res.sharedEmotes is not null)
                    BttvEmotes = res.sharedEmotes;
                else BttvEmotes = Array.Empty<BttvEmote>();
            }
            else BttvEmotes = Array.Empty<BttvEmote>();
        }

        await InitPubSub();

        _channelCache = Channel;
    }

    private async Task InitPubSub()
    {
        if (_pubSubClient is not null)
        {
            _pubSubClient.Disconnect();
            _pubSubClient = null;
        }
        if (_channelObj is null) return;

        var model = await Db.TwitchAuth.FirstOrDefaultAsync(m => m.UserId == _channelObj.id);

        if(model is null || !model.IsValid)
        {
            Logger.LogWarning($"PubSub for {Channel} not connected due to missing authentication model");
            return;
        }

        _pubSubClient = new TwitchPubSub();

        _pubSubClient.OnPubSubServiceConnected += (obj, ev) =>
        {
            Logger.LogInformation($"[{model.Username}] Pubsub connected");
            _pubSubClient.SendTopics(model.AccessToken);
        };
        _pubSubClient.OnFollow += (obj, ev) =>
        {
            Logger.LogInformation($"[{model.Username}] {ev.DisplayName} just followed you! Clap Clap");
        };
        _pubSubClient.OnLog += (obj, ev) =>
        {
            Logger.LogDebug($"[{model.Username}] " + ev.Data);
        };

        _pubSubClient.ListenToFollows(_channelObj.id);

        _pubSubClient.Connect();
    }

    private void OnMessage(ProcessedChatMessage message)
    {
        InvokeAsync(async () =>
        {
            if (!message.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message))
                return;

            if (!string.IsNullOrWhiteSpace(message.Original.ColorHex))
            {
                var hsl = Helper.RgbToHsl(Helper.HexToRgb(message.Original.ColorHex));
                hsl.l += (1 - hsl.l) / 4;
                message.SetColor($"hsl({hsl.h}, {(int)(hsl.s * 100)}%, {(int)(hsl.l * 100)}%)");
            }

            var globalBttv = await Emotes.GetGlobalBttv() ?? Array.Empty<BttvEmote>();
            message.ParseEmotes(globalBttv.Concat(BttvEmotes).ToArray());

            var user = await TwApi.GetUser(AuthModel.AccessToken, message.Original.UserId);
            if(user is null)
            {
                await TwAuth.Restore(AuthModel);
                user = await TwApi.GetUser(AuthModel.AccessToken, message.Original.UserId);
            }

            message.SetUser(user);

            if (message.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Code))
                foreach (var customContent in message.CustomContent)
                    if (customContent is CodeContent codeContent && !codeContent.IsFormatted)
                        await codeContent.Format(Js);

            _messages.Add(message);
            if (_messages.Count > 20) _messages.RemoveAt(0);
            StateHasChanged();

            await Task.Delay(TimeSpan.FromMinutes(1));
            var component = _components.FirstOrDefault(c => c.Message == message);
            if (component is not null)
                await component.AnimateAway();
            _messages.Remove(message);
            StateHasChanged();
        });
    }

    public void RegisterComponent(ChatMessageComponent component)
    {
        lock (_components)
        {
            if (!_components.Contains(component))
                _components.Add(component);
        }
    }

    public void UnregisterComponent(ChatMessageComponent component)
    {
        lock (_components)
        {
            if (_components.Contains(component))
                _components.Remove(component);
        }
    }

    public void Dispose()
    {
        TwChat.OnMessage -= OnMessage;

        if (_pubSubClient is not null)
        {
            _pubSubClient.Disconnect();
            _pubSubClient = null;
        }
    }
}
