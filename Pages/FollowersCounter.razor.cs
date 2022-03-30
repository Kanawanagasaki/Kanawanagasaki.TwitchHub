namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;

public partial class FollowersCounter : ComponentBase
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public NavigationManager NavMgr { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }
    private TwitchGetUsersResponse _channel;

    [Parameter]
    [SupplyParameterFromQuery]
    public int? Goal { get; set; }

    protected override void OnInitialized()
    {
        TwAuth.AuthenticationChange += () =>
        {
            if (!TwAuth.IsAuthenticated) return;

            InvokeAsync(async () =>
            {
                _channel = await TwApi.GetUserByLogin(Channel);
                StateHasChanged();
            });
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;
        if (!TwAuth.IsAuthenticated) return;

        _channel = await TwApi.GetUserByLogin(Channel);
        StateHasChanged();
    }
}