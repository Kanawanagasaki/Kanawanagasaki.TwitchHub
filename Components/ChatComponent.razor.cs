namespace Kanawanagasaki.TwitchHub.Components;

using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;

public partial class ChatComponent : ComponentBase, IDisposable
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchChatService TwChat { get; set; }
    [Inject]
    public EmotesService Emotes { get; set; }
    [Inject]
    public ILogger<ChatComponent> Logger { get; set; }

    [Parameter]
    public string Channel { get; set; }
    private string _channelCache = "";
    private TwitchGetUsersResponse _channelObj;

    private List<ProcessedChatMessage> _messages = new();
    private List<ChatMessageComponent> _components = new();

    protected override async Task OnInitializedAsync()
    {
        TwChat.OnMessage += OnMessage;

        await TwApi.GetChannelBadges();
        await TwApi.GetGlobalBadges();
        await Emotes.GetGlobalBttv();
    }

    protected override async Task OnParametersSetAsync()
    {
        if(string.IsNullOrWhiteSpace(Channel)) return;
        
        _channelObj = await TwApi.GetUserByLogin(Channel);
        StateHasChanged();
        if (_channelObj is not null)
            await Emotes.GetChannelBttv(_channelObj.id);

        if (Channel == _channelCache) return;

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