namespace Kanawanagasaki.TwitchHub.Pages;

using System.Threading.Tasks;
using System.Web;
using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

public partial class Auth : ComponentBase
{
    [Inject]
    public required TwitchAuthService TwAuth { get; set; }
    [Inject]
    public required IConfiguration Conf { get; set; }
    [Inject]
    public required SQLiteContext Db { get; set; }
    [Inject]
    public required NavigationManager NavMgr { get; set; }

    private TwitchAuthModel[] _models = [];

    protected override async Task OnInitializedAsync()
    {
        TwAuth.AuthenticationChange += _ => InvokeAsync(StateHasChanged);

        _models = await Db.TwitchAuth.ToArrayAsync();
        foreach (var model in _models)
            await TwAuth.Restore(model);
        await Db.SaveChangesAsync();
    }

    private async Task RefreshInfo(TwitchAuthModel model)
    {
        var validationModel = await TwAuth.Validate(model.AccessToken);
        if (validationModel is null)
            return;
        model.UserId = validationModel.user_id;
        model.Username = validationModel.login;
        await Db.SaveChangesAsync();
    }

    private void NavigateToTwitchAuth()
    {
        var uri = new Uri(NavMgr.Uri);

        Dictionary<string, string> query = new()
        {
            { "client_id", Conf["Twitch:ClientId"] ?? string.Empty },
            { "redirect_uri", $"{uri.Scheme}://{uri.Host}:{uri.Port}/twitchauthresponse" },
            { "response_type", "code" },
            { "scope", "chat:read chat:edit channel:moderate whispers:read whispers:edit moderator:manage:banned_users channel:read:subscriptions channel:read:redemptions channel:manage:redemptions" }
        };
        string queryStr = "?" + string.Join("&", query.Select(item => HttpUtility.UrlEncode(item.Key) + "=" + HttpUtility.UrlEncode(item.Value)));
        NavMgr.NavigateTo("https://id.twitch.tv/oauth2/authorize" + queryStr);
    }
}