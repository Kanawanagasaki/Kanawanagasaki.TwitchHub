namespace Kanawanagasaki.TwitchHub.Services;

using System.Threading;
using Kanawanagasaki.TwitchHub.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TwitchAuthService : BackgroundService
{
    public event Action<TwitchAuthModel> AuthenticationChange;

    private IServiceScopeFactory _scopeFactory;
    private IConfiguration _conf;
    private ILogger<TwitchAuthService> _logger;

    public TwitchAuthService(IServiceScopeFactory scopeFactory, IConfiguration conf, ILogger<TwitchAuthService> logger)
    {
        _scopeFactory = scopeFactory;
        _conf = conf;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Restore();
    }

    public async Task Restore()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<SQLiteContext>();
        var models = await db.TwitchAuth.ToArrayAsync();

        foreach(var model in models)
            await Restore(model);
    }

    public async Task Restore(TwitchAuthModel model)
    {
        try
        {
            var isValid = await Validate(model.AccessToken);
            if(!isValid)
            {
                isValid = await RefreshToken(model);
                if(!isValid)
                    _logger.LogWarning("Failed to refresh token for " + model.Username);
            }
            if(isValid != model.IsValid)
            {
                model.IsValid = isValid;
                AuthenticationChange?.Invoke(model);
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e.Message);
            model.IsValid = false;
        }
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<SQLiteContext>();
        await db.SaveChangesAsync();
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<SQLiteContext>();

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
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            
            var accessToken = obj.Value<string>("access_token");
            var refreshToken = obj.Value<string>("refresh_token");

            var api = scope.ServiceProvider.GetService<TwitchApiService>();
            var user = await api.GetUser(accessToken);
            if(user is null) return false;

            var model = await db.TwitchAuth.FirstOrDefaultAsync(m => m.UserId == user.id);
            if(model is null)
            {
                model = new()
                {
                    Uuid = Guid.NewGuid(),
                    UserId = user.id,
                    Username = user.login,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    IsValid = true
                };
                await db.TwitchAuth.AddAsync(model);
            }
            else
            {
                model.Username = user.login;
                model.AccessToken = accessToken;
                model.RefreshToken = refreshToken;
                model.IsValid = true;
            }
            await db.SaveChangesAsync();

            AuthenticationChange?.Invoke(model);

            return true;
        }
        else return false;
    }

    public async Task<bool> RefreshToken(TwitchAuthModel model)
    {
        using var http = new HttpClient();

        var data = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", model.RefreshToken },
            { "client_id", _conf["Twitch:ClientId"] },
            { "client_secret", _conf["Twitch:Secret"] }
        };
        using var form = new FormUrlEncodedContent(data);

        var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            model.AccessToken = obj.Value<string>("access_token");
            model.RefreshToken = obj.Value<string>("refresh_token");

            return true;
        }
        else return false;
    }
}
