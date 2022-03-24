namespace Kanawanagasaki.TwitchHub.Services;

using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TwitchAuthService : BackgroundService
{
    public string AccessToken { get; private set; }

    public bool IsAuthenticated { get; private set; } = false;
    public event Action AuthenticationChange;

    private IConfiguration _conf;
    private ILogger<TwitchAuthService> _logger;

    public TwitchAuthService(IConfiguration conf, ILogger<TwitchAuthService> logger)
    {
        _conf = conf;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Restore();
    }

    public async Task Restore()
    {
        try
        {
            if(!File.Exists("twitch_accessToken.json"))
                return;
            var json = await File.ReadAllTextAsync("twitch_accessToken.json");

            var obj = JsonConvert.DeserializeObject<JObject>(json);
            var accessToken = obj.Value<string>("access_token");
            var refreshToken = obj.Value<string>("refresh_token");

            if(await Validate(AccessToken))
            {
                AccessToken = accessToken;

                IsAuthenticated = true;
                AuthenticationChange?.Invoke();
            }
            else if(!await RefreshToken(refreshToken))
                _logger.LogWarning("Failed to refresh token");
        }
        catch(Exception e)
        {
            _logger.LogError(e.Message);
        }
    }

    public async Task<bool> Validate(string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await http.GetAsync("https://id.twitch.tv/oauth2/validate");
        return response.StatusCode == System.Net.HttpStatusCode.OK;
    }

    public async Task<bool> SignIn(string redirecturi, string code)
    {
        Dictionary<string, string> postData = new()
        {
            { "client_id", _conf["Twitch:ClientId"] },
            { "client_secret", _conf["Twitch:Secret"] },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirecturi }
        };
        FormUrlEncodedContent form = new(postData);

        using var http = new HttpClient();
        var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync("twitch_accessToken.json", json);

            var obj = JsonConvert.DeserializeObject<JObject>(json);
            AccessToken = obj.Value<string>("access_token");

            IsAuthenticated = true;
            AuthenticationChange?.Invoke();

            return true;
        }
        else return false;
    }

    public async Task<bool> RefreshToken(string refreshToken)
    {
        using var http = new HttpClient();

        var data = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "client_id", _conf["Twitch:ClientId"] },
            { "client_secret", _conf["Twitch:Secret"] }
        };
        using var form = new FormUrlEncodedContent(data);

        var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync("twitch_accessToken.json", json);

            var obj = JsonConvert.DeserializeObject<JObject>(json);
            AccessToken = obj.Value<string>("access_token");

            IsAuthenticated = true;
            AuthenticationChange?.Invoke();

            return true;
        }
        else return false;
    }
}