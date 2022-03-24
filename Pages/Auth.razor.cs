namespace Kanawanagasaki.TwitchHub.Pages;

using System.Web;
using Microsoft.AspNetCore.Components;

public partial class Auth : ComponentBase
{
    [Inject]
    public IConfiguration Conf { get; set; }

    [Inject]
    public NavigationManager NavMgr { get; set; }

    private void NavigateToTwitchAuth()
    {
        var uri = new Uri(NavMgr.Uri);

        Dictionary<string, string> query = new()
        {
            { "client_id", Conf["Twitch:ClientId"] },
            { "redirect_uri", $"{uri.Scheme}://{uri.Host}:{uri.Port}/twitchauthresponse" },
            { "response_type", "code" },
            { "scope", "chat:read chat:edit channel:moderate whispers:read whispers:edit" }
        };
        string queryStr = "?" + string.Join("&", query.Select(item => HttpUtility.UrlEncode(item.Key) + "=" + HttpUtility.UrlEncode(item.Value)));
        NavMgr.NavigateTo("https://id.twitch.tv/oauth2/authorize" + queryStr);
    }
}