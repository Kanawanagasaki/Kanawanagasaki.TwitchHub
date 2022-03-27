namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Components;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwitchLib.Client.Models;

public partial class Index : ComponentBase, IDisposable
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public TwitchChatService TwChat { get; set; }
    [Inject]
    public EmotesService Emotes { get; set; }
    [Inject]
    public NavigationManager NavMgr { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }
    private string _channelCache = "";

    private List<ProcessedChatMessage> _messages = new();
    private List<ChatMessageComponent> _components = new();

    protected override void OnInitialized()
    {
        TwChat.OnMessage += message =>
        {
            if (!message.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message))
                return;

            _messages.Add(message);
            if (_messages.Count > 20) _messages.RemoveAt(0);
            InvokeAsync(StateHasChanged);

            Task.Run(async () => await InvokeAsync(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                var component = _components.FirstOrDefault(c => c.Message == message);
                if (component is not null)
                    await component.AnimateAway();
                _messages.Remove(message);
                StateHasChanged();
            }));
        };

        TwAuth.AuthenticationChange += () => InvokeAsync(StateHasChanged);
    }

    protected override async Task OnInitializedAsync()
    {
        await TwApi.GetChannelBadges();
        await TwApi.GetGlobalBadges();
        await Emotes.GetGlobalBttv();
    }

    protected override void OnParametersSet()
    {
        if (Channel != _channelCache)
        {
            if (!string.IsNullOrWhiteSpace(Channel))
                TwChat.JoinChannel(Channel);
            if (!string.IsNullOrWhiteSpace(_channelCache))
                TwChat.LeaveChannel(_channelCache);

            _channelCache = Channel;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Channel))
        {
            var user = await TwApi.GetUserByLogin(Channel);
            if (user is not null)
                await Emotes.GetChannelBttv(user.id);
        }
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
    }
}