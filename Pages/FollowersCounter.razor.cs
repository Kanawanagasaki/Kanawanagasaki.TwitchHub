namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

public partial class FollowersCounter : ComponentBase
{
    [Inject]
    public required TwitchApiService TwApi { get; set; }
    [Inject]
    public required TwitchAuthService TwAuth { get; set; }
    [Inject]
    public required NavigationManager NavMgr { get; set; }
    [Inject]
    public required SQLiteContext Db { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Channel { get; set; }
    private TwitchGetUsersResponse? _channel;

    [Parameter]
    [SupplyParameterFromQuery]
    public int? Goal { get; set; }

    protected override void OnInitialized()
    {
        TwAuth.AuthenticationChange += model =>
        {
            if (Channel is null)
                return;
            if (model.Username.ToLower() != Channel.ToLower())
                return;
            InvokeAsync(async () => await UpdateChannel(model));
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel))
            return;

        var model = await Db.TwitchAuth.FirstOrDefaultAsync(m => m.Username.ToLower() == Channel.ToLower());
        if (model is null)
            return;
        await UpdateChannel(model);
    }

    private async Task UpdateChannel(TwitchAuthModel model)
    {
        if (Channel is null)
            return;
        _channel = await TwApi.GetUserByLogin(model.AccessToken, Channel);
        if (_channel is null)
        {
            await TwAuth.Restore(model);
            _channel = await TwApi.GetUserByLogin(model.AccessToken, Channel);
        }

        StateHasChanged();
    }
}
