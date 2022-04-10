namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

public partial class Chat : ComponentBase
{
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public NavigationManager NavMgr { get; set; }
    [Inject]
    public SQLiteContext Db { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Bot { get; set; }
    private string _bot;

    private TwitchAuthModel _model { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Bot))
            _bot = Channel;
        else _bot = Bot;

        if(string.IsNullOrWhiteSpace(_bot)) return;

        _model = await Db.TwitchAuth.FirstOrDefaultAsync(m => m.Username.ToLower() == _bot.ToLower());
        if (_model is not null)
            if (!_model.IsValid)
                await TwAuth.Restore(_model);
    }
}
