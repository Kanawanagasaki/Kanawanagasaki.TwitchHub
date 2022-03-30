namespace Kanawanagasaki.TwitchHub.Components;

using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using TwitchLib.PubSub;

public partial class TwitchFollowersCounter : ComponentBase
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchAuthService TwAuth { get; set; }

    [Parameter]
    public string ChannelId { get; set; }

    private TwitchPubSub _pubSub;

    private int _count = 0;

    protected override async Task OnInitializedAsync()
    {
        if (ChannelId is null) return;

        TwAuth.AuthenticationChange += () =>
        {
            InvokeAsync(async () =>
            {
                _count = await TwApi.GetFollowersCount(ChannelId);
                StateHasChanged();
            });
        };

        var user = await TwApi.GetUser();
        if (user is null) return;

        _count = await TwApi.GetFollowersCount(ChannelId);

        _pubSub = new TwitchPubSub();
        _pubSub.OnFollow += (obj, ev) =>
        {
            InvokeAsync(async () =>
            {
                _count = await TwApi.GetFollowersCount(ChannelId);
                StateHasChanged();
            });
        };
        _pubSub.ListenToFollows(user.id.ToString());
    }
}