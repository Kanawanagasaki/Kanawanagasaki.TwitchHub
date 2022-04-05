namespace Kanawanagasaki.TwitchHub.Components;

using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class ChatComponent : ComponentBase, IDisposable
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchChatService TwChat { get; set; }
    [Inject]
    public EmotesService Emotes { get; set; }
    [Inject]
    public IJSRuntime Js { get; set; }
    [Inject]
    public ILogger<ChatComponent> Logger { get; set; }
    [Inject]
    public HelperService Helper { get; set; }

    [Parameter]
    public string Channel { get; set; }
    private string _channelCache = "";
    private TwitchGetUsersResponse _channelObj;

    public BttvEmote[] BttvEmotes { get; private set; } = Array.Empty<BttvEmote>();

    public Dictionary<string, string> Badges = new();

    private List<ProcessedChatMessage> _messages = new();
    private List<ChatMessageComponent> _components = new();

    protected override async Task OnInitializedAsync()
    {
        TwChat.OnMessage += OnMessage;

        var channelBadges = await TwApi.GetChannelBadges();
        var globalBadges = await TwApi.GetGlobalBadges();
        foreach (var badge in channelBadges.data.Concat(globalBadges.data))
        {
            var version = badge.versions.OrderByDescending(v => int.TryParse(v.id, out var id) ? id : 0).First();
            Badges[badge.set_id] = version.image_url_1x;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;

        _channelObj = await TwApi.GetUserByLogin(Channel);
        StateHasChanged();

        if (Channel == _channelCache) return;
        if (_channelObj is not null)
        {
            var res = await Emotes.GetChannelBttv(_channelObj.id);
            BttvEmotes = res.channelEmotes.Concat(res.sharedEmotes).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(Channel))
            TwChat.JoinChannel(Channel);
        if (!string.IsNullOrWhiteSpace(_channelCache))
            TwChat.LeaveChannel(_channelCache);

        _channelCache = Channel;
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

            message.ParseEmotes(Emotes.GlobalBttvEmotes.Concat(BttvEmotes).ToArray());

            var user = await TwApi.GetUser(message.Original.UserId);
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
        if (!string.IsNullOrWhiteSpace(Channel))
            TwChat.LeaveChannel(Channel);
        TwChat.OnMessage -= OnMessage;
    }
}