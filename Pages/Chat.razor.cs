namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

public partial class Chat : ComponentBase
{
    [Inject]
    public required TwitchAuthService TwAuth { get; set; }
    [Inject]
    public required NavigationManager NavMgr { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Channel { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Bot { get; set; }
    private string? _bot;

    private TwitchAuthModel? _model { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Bot))
            _bot = Channel;
        else _bot = Bot;
        if(string.IsNullOrWhiteSpace(_bot)) return;
        _model = await TwAuth.GetRestored(_bot);
    }
}
