namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Components;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwitchLib.Client.Models;

public partial class Index : ComponentBase
{
    [Inject]
    public TwitchAuthService TwAuth { get; set; }

    protected override void OnInitialized()
    {
        TwAuth.AuthenticationChange += () => InvokeAsync(StateHasChanged);
    }
}