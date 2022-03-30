namespace Kanawanagasaki.TwitchHub.Components;

using System.Threading.Tasks;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using TwitchLib.PubSub;

public partial class TwitchFollowersCounter : ComponentBase
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public ILogger<TwitchFollowersCounter> Logger { get; set; }

    [Parameter]
    public string ChannelId { get; set; }
    [Parameter]
    public int? Goal { get; set; }

    private int _count = 0;

    protected override void OnInitialized()
    {
        TwAuth.AuthenticationChange += () =>
        {
            InvokeAsync(async () =>
            {
                if(!string.IsNullOrEmpty(ChannelId))
                    _count = await TwApi.GetFollowersCount(ChannelId);
                StateHasChanged();
            });
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        while(true)
        {
            if(!string.IsNullOrEmpty(ChannelId) && TwAuth.IsAuthenticated)
            {
                _count = await TwApi.GetFollowersCount(ChannelId);
                StateHasChanged();
            }
            await Task.Delay(120_000);
        }
    }
}