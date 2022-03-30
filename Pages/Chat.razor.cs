namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;

public partial class Chat : ComponentBase
{
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public NavigationManager NavMgr { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }
}