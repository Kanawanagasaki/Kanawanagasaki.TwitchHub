namespace Kanawanagasaki.TwitchHub.Services;

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Kanawanagasaki.TwitchHub.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TwitchAuthService
{
    public event Action<TwitchAuthModel>? AuthenticationChange;

    private readonly IServiceScope _scope;
    private readonly IConfiguration _conf;
    private readonly ILogger<TwitchAuthService> _logger;
    private readonly SQLiteContext _db;
    private readonly TwitchApiService _api;

    public TwitchAuthService(IServiceScopeFactory serviceScopeFactory, IConfiguration conf, ILogger<TwitchAuthService> logger, TwitchApiService api)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
        _conf = conf;
        _logger = logger;
        _api = api;
    }

    public async Task<TwitchAuthModel?> GetRestored(string twitchLogin)
    {
        var model = await _db.TwitchAuth.FirstOrDefaultAsync(m => m.Username.ToLower() == twitchLogin.ToLower());
        if (model is null)
            return null;

        if (DateTimeOffset.UtcNow < model.ExpiresAt)
            return model;

        await Restore(model);
        await _db.SaveChangesAsync();
        return model;
    }

    public async Task<TwitchAuthModel?> GetRestoredById(string id)
    {
        var model = await _db.TwitchAuth.FirstOrDefaultAsync(m => m.UserId == id);
        if (model is null)
            return null;

        if (DateTimeOffset.UtcNow < model.ExpiresAt)
            return model;

        await Restore(model);
        await _db.SaveChangesAsync();
        return model;
    }

    public async Task<TwitchAuthModel?> GetRestoredByUuid(Guid uuid)
    {
        var model = await _db.TwitchAuth.FirstOrDefaultAsync(m => m.Uuid == uuid);
        if (model is null)
            return null;

        if (DateTimeOffset.UtcNow < model.ExpiresAt)
            return model;

        await Restore(model);
        await _db.SaveChangesAsync();
        return model;
    }

    public async Task Restore(TwitchAuthModel model)
    {
        try
        {
            var validationModel = await Validate(model.AccessToken);
            var isValid = validationModel is not null;
            if (!isValid)
            {
                _logger.LogWarning("Failed to validate token for {ModelUsername}", model.Username);
                isValid = await RefreshToken(model);
                if (!isValid)
                    _logger.LogWarning("Failed to refresh token for {ModelUsername}", model.Username);
                else _logger.LogInformation("Tokens for {ModelUsername} successfully refreshed", model.Username);
            }
            else
            {
                model.ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(validationModel!.expires_in / 2);
                _logger.LogInformation("Tokens for {ModelUsername} are valid", model.Username);
            }

            if (isValid != model.IsValid)
            {
                model.IsValid = isValid;
                AuthenticationChange?.Invoke(model);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            model.IsValid = false;
        }
    }

    public record ValidateRecord(string client_id, string login, string[] scopes, string user_id, int expires_in);
    public async Task<ValidateRecord?> Validate(string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await http.GetAsync("https://id.twitch.tv/oauth2/validate");

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<ValidateRecord>(json);
        }
        else
            return null;
    }

    public async Task<bool> SignIn(string redirecturi, string code)
    {
        Dictionary<string, string> postData = new()
        {
            { "client_id", _conf["Twitch:ClientId"] ?? string.Empty },
            { "client_secret", _conf["Twitch:Secret"] ?? string.Empty },
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
            if (obj is null)
                return false;

            var accessToken = obj.Value<string>("access_token");
            if (accessToken is null)
                return false;
            var refreshToken = obj.Value<string>("refresh_token");
            if (refreshToken is null)
                return false;
            var expiresIn = obj.Value<int>("expires_in");

            var user = await _api.GetUser(accessToken);
            if (user is null)
                return false;

            var model = await _db.TwitchAuth.FirstOrDefaultAsync(m => m.UserId == user.id);
            if (model is null)
            {
                model = new()
                {
                    Uuid = Guid.NewGuid(),
                    UserId = user.id,
                    Username = user.login,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresIn / 2),
                    IsValid = true
                };
                await _db.TwitchAuth.AddAsync(model);
            }
            else
            {
                model.Username = user.login;
                model.AccessToken = accessToken;
                model.RefreshToken = refreshToken;
                model.ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresIn / 2);
                model.IsValid = true;
            }
            await _db.SaveChangesAsync();

            AuthenticationChange?.Invoke(model);

            return true;
        }
        else return false;
    }

    private async Task<bool> RefreshToken(TwitchAuthModel model)
    {
        using var http = new HttpClient();

        var data = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", model.RefreshToken },
            { "client_id", _conf["Twitch:ClientId"] ?? string.Empty },
            { "client_secret", _conf["Twitch:Secret"] ?? string.Empty }
        };
        using var form = new FormUrlEncodedContent(data);

        var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", form);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            if (obj is null)
                return false;
            var accessToken = obj.Value<string>("access_token");
            if (accessToken is null)
                return false;
            var refreshToken = obj.Value<string>("refresh_token");
            if (refreshToken is null)
                return false;
            var expiresIn = obj.Value<int>("expires_in");

            model.AccessToken = accessToken;
            model.RefreshToken = refreshToken;
            model.ExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresIn / 2);

            return true;
        }
        else return false;
    }
}
