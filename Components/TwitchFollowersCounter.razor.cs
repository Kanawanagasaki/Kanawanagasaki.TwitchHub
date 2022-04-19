namespace Kanawanagasaki.TwitchHub.Components;

using System.Threading.Tasks;
using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using TwitchLib.PubSub;

public partial class TwitchFollowersCounter : ComponentBase, IDisposable
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public ILogger<TwitchFollowersCounter> Logger { get; set; }
    [Inject]
    public SQLiteContext Db { get; set; }

    [Parameter]
    public string ChannelId { get; set; }
    [Parameter]
    public int? Goal { get; set; }

    private int _count = 0;

    private bool _isRunning = true;

    protected override void OnInitialized()
    {
        TwAuth.AuthenticationChange += model =>
        {
            if(model.UserId == ChannelId)
                InvokeAsync(async () => await UpdateCount(model));
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        while (_isRunning)
        {
            if (!string.IsNullOrWhiteSpace(ChannelId))
            {
                var model = await Db.TwitchAuth.FirstOrDefaultAsync(m => m.UserId == ChannelId);
                if(model is not null)
                    await UpdateCount(model);
            }
            await Task.Delay(120_000);
        }
    }

    private async Task UpdateCount(TwitchAuthModel model)
    {
        if (string.IsNullOrWhiteSpace(ChannelId)) return;
        if (!model.IsValid) return;

        _count = await TwApi.GetFollowersCount(model.AccessToken, ChannelId);
        if (_count < 0)
        {
            await TwAuth.Restore(model);
            _count = await TwApi.GetFollowersCount(model.AccessToken, ChannelId);
        }
        StateHasChanged();
    }

    public void Dispose()
    {
        _isRunning = false;
    }
}
