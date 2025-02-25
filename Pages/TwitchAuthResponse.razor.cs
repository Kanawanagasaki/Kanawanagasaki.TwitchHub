using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;

namespace Kanawanagasaki.TwitchHub.Pages;

public partial class TwitchAuthResponse : ComponentBase
{
    [Inject]
    public required TwitchAuthService TwAuth { get; set; }
    [Inject]
    public required NavigationManager NavMgr { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Code { get; set; }

    private bool _processed = false;

    protected override async Task OnParametersSetAsync()
    {
        if (_processed) return;

        var uri = new Uri(NavMgr.Uri);
        await TwAuth.SignIn($"{uri.Scheme}://{uri.Host}:{uri.Port}/twitchauthresponse", Code ?? string.Empty);
        NavMgr.NavigateTo("/Auth");

        _processed = true;
    }
}