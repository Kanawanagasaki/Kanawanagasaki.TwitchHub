namespace Kanawanagasaki.TwitchHub.Services;

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class SevenTvApiService
{
    private const string SEVEN_TV_AUTH_KEY = "seven_tv_auth";

    private readonly IServiceScope _scope;
    private readonly SQLiteContext _db;
    private readonly ILogger<SevenTvApiService> _logger;

    public SevenTvApiService(IServiceScopeFactory serviceScopeFactory, ILogger<SevenTvApiService> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
        _logger = logger;
    }

    public Task<GqlResponse<GqlMeData>?> GetMe()
        => Execute<GqlResponse<GqlMeData>>(new GqlRequest
        (
            "Me",
            "query Me($productId: Id!) { users { me { id mainConnection { platformDisplayName platformAvatarUrl __typename } style { activeProfilePicture { images { url mime size width height scale frameCount __typename } __typename } activePaint { id name data { layers { id ty { __typename ... on PaintLayerTypeSingleColor { color { hex __typename } __typename } ... on PaintLayerTypeLinearGradient { angle repeating stops { at color { hex __typename } __typename } __typename } ... on PaintLayerTypeRadialGradient { repeating stops { at color { hex __typename } __typename } shape __typename } ... on PaintLayerTypeImage { images { url mime size scale width height frameCount __typename } __typename } } opacity __typename } shadows { color { hex __typename } offsetX offsetY blur __typename } __typename } __typename } activeEmoteSetId __typename } highestRoleColor { hex __typename } roles { name color { hex __typename } __typename } editableEmoteSetIds permissions { admin { manageRedeemCodes manageEntitlements __typename } user { manageAny useCustomProfilePicture manageBilling manageSessions __typename } emote { manageAny __typename } emoteSet { manage manageAny __typename } ticket { create __typename } __typename } billing(productId: $productId) { subscriptionInfo { activePeriod { providerId { provider __typename } __typename } __typename } __typename } editorFor { user { id mainConnection { platformDisplayName platformAvatarUrl __typename } style { activeProfilePicture { images { url mime size width height scale frameCount __typename } __typename } activePaint { id name data { layers { id ty { __typename ... on PaintLayerTypeSingleColor { color { hex __typename } __typename } ... on PaintLayerTypeLinearGradient { angle repeating stops { at color { hex __typename } __typename } __typename } ... on PaintLayerTypeRadialGradient { repeating stops { at color { hex __typename } __typename } shape __typename } ... on PaintLayerTypeImage { images { url mime size scale width height frameCount __typename } __typename } } opacity __typename } shadows { color { hex __typename } offsetX offsetY blur __typename } __typename } __typename } activeEmoteSetId __typename } highestRoleColor { hex __typename } __typename } state __typename } __typename } __typename }}",
            new()
            {
                ["productId"] = "01FEVKBBTGRAT7FCY276TNTJ4A"
            }
        ));

    public Task<GqlResponse<GqlOneEmoteData>?> GetOneEmote(string emoteId)
        => Execute<GqlResponse<GqlOneEmoteData>>(new GqlRequest
        (
            "OneEmote",
            "query OneEmote($id: Id!, $isDefaultSetSet: Boolean!, $defaultSetId: Id!) { emotes { emote(id: $id) { id defaultName owner { id mainConnection { platformDisplayName platformAvatarUrl __typename } style { activeProfilePicture { images { url mime size width height scale frameCount __typename } __typename } activePaint { id name data { layers { id ty { __typename ... on PaintLayerTypeSingleColor { color { hex __typename } __typename } ... on PaintLayerTypeLinearGradient { angle repeating stops { at color { hex __typename } __typename } __typename } ... on PaintLayerTypeRadialGradient { repeating stops { at color { hex __typename } __typename } shape __typename } ... on PaintLayerTypeImage { images { url mime size scale width height frameCount __typename } __typename } } opacity __typename } shadows { color { hex __typename } offsetX offsetY blur __typename } __typename } __typename } __typename } highestRoleColor { hex __typename } editors { editorId permissions { emote { manage __typename } __typename } __typename } __typename } tags flags { animated approvedPersonal defaultZeroWidth deniedPersonal nsfw private publicListed __typename } attribution { user { mainConnection { platformDisplayName platformAvatarUrl __typename } style { activeProfilePicture { images { url mime size width height scale frameCount __typename } __typename } __typename } highestRoleColor { hex __typename } __typename } __typename } imagesPending images { url mime size width height scale frameCount __typename } ranking(ranking: TRENDING_WEEKLY) inEmoteSets(emoteSetIds: [$defaultSetId]) @include(if: $isDefaultSetSet) { emoteSetId emote { id alias __typename } __typename } deleted __typename } __typename }}",
            new()
            {
                ["defaultSetId"] = "",
                ["id"] = emoteId,
                ["isDefaultSetSet"] = false
            }
        ));

    public Task<GqlResponse<GqlAddEmoteToSetData>?> AddEmoteToSet(string setId, string emoteId, string emoteAlias)
        => Execute<GqlResponse<GqlAddEmoteToSetData>>(new GqlRequest
        (
            "AddEmoteToSet",
            "mutation AddEmoteToSet($setId: Id!, $emote: EmoteSetEmoteId!) { emoteSets { emoteSet(id: $setId) { addEmote(id: $emote) { id __typename } __typename } __typename }}",
            new()
            {
                ["emote"] = new
                {
                    alias = emoteAlias,
                    emoteId = emoteId
                },
                ["setId"] = setId
            }
        ));

    public async Task<(bool isSucess, string message)> AddEmoteToDefaultSet(string emoteId)
    {
        var me = await GetMe();
        var activeEmoteSetId = me?.data?.users?.me?.style?.activeEmoteSetId;
        if (activeEmoteSetId is null)
            return (false, "Failed to authenticate 7tv user");

        var emote = await GetOneEmote(emoteId);
        var emoteDefaultName = emote?.data?.emotes?.emote?.defaultName;
        if (emoteDefaultName is null)
            return (false, "Failed to retrieve 7tv emote from emote id");

        var addResult = await AddEmoteToSet(activeEmoteSetId, emoteId, emoteDefaultName);
        if (addResult is null)
            return (false, $"Failed to add \"{emoteDefaultName}\" to active 7tv set");
        if (addResult.errors is not null && 0 < addResult.errors.Count)
            return (false, string.Join("; ", addResult.errors.Select(x => x.message)));

        return (true, $"Pog New emote {emoteDefaultName}");
    }

    public async Task<T?> Execute<T>(GqlRequest gql) where T : class
    {
        using var req = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://7tv.io/v4/gql"),
            Content = JsonContent.Create(gql)
        };

        using var res = await Execute(req);
        if (res.IsSuccessStatusCode)
            return await res.Content.ReadFromJsonAsync<T>();

        var body = await res.Content.ReadAsStringAsync();
        _logger.LogError("{Method}(): {StatusCodeInt} {StatusCode}\n{Body}", nameof(GetMe), (int)res.StatusCode, res.StatusCode, body);
        return null;
    }

    public async Task<HttpResponseMessage> Execute(HttpRequestMessage req)
    {
        var sevenTvAuthSetting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == SEVEN_TV_AUTH_KEY);

        if (sevenTvAuthSetting is not null)
            req.Headers.Add("Authorization", "Bearer " + sevenTvAuthSetting.Value);

        using var client = new HttpClient();
        return await client.SendAsync(req);
    }

    public async Task Authenticate(string auth)
    {
        var sevenTvAuthSetting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == SEVEN_TV_AUTH_KEY);

        if (sevenTvAuthSetting is null)
        {
            sevenTvAuthSetting = new()
            {
                Uuid = Guid.NewGuid(),
                Key = SEVEN_TV_AUTH_KEY,
                Value = auth
            };
            _db.Settings.Add(sevenTvAuthSetting);
        }
        else
        {
            sevenTvAuthSetting.Value = auth;
        }

        await _db.SaveChangesAsync();
    }

    public record GqlRequest(string operationName, string query, Dictionary<string, object> variables);

    public class GqlResponse<T> where T : class
    {
        public T? data { get; set; }
        public Extensions? extensions { get; set; }
        public List<Error>? errors { get; set; }

        public class Analyzer
        {
            public int complexity { get; set; }
            public int depth { get; set; }
        }

        public class Error
        {
            public string? message { get; set; }
            public List<Location>? locations { get; set; }
            public List<string>? path { get; set; }
            public Extensions? extensions { get; set; }
        }

        public class Extensions
        {
            public Analyzer? analyzer { get; set; }
            public string? code { get; set; }
            public string? message { get; set; }
            public int status { get; set; }
        }

        public class Location
        {
            public int line { get; set; }
            public int column { get; set; }
        }
    }

    public class GqlMeData
    {
        public Users? users { get; set; }

        public class Admin
        {
            public bool manageRedeemCodes { get; set; }
            public bool manageEntitlements { get; set; }
            public string? __typename { get; set; }
        }

        public class Billing
        {
            public SubscriptionInfo? subscriptionInfo { get; set; }
            public string? __typename { get; set; }
        }

        public class Emote
        {
            public bool manageAny { get; set; }
            public string? __typename { get; set; }
        }

        public class EmoteSet
        {
            public bool manage { get; set; }
            public bool manageAny { get; set; }
            public string? __typename { get; set; }
        }

        public class MainConnection
        {
            public string? platformDisplayName { get; set; }
            public string? platformAvatarUrl { get; set; }
            public string? __typename { get; set; }
        }

        public class Me
        {
            public string? id { get; set; }
            public MainConnection? mainConnection { get; set; }
            public Style? style { get; set; }
            public object? highestRoleColor { get; set; }
            public List<Role>? roles { get; set; }
            public List<string>? editableEmoteSetIds { get; set; }
            public Permissions? permissions { get; set; }
            public Billing? billing { get; set; }
            public List<object>? editorFor { get; set; }
            public string? __typename { get; set; }
        }

        public class Permissions
        {
            public Admin? admin { get; set; }
            public User? user { get; set; }
            public Emote? emote { get; set; }
            public EmoteSet? emoteSet { get; set; }
            public Ticket? ticket { get; set; }
            public string? __typename { get; set; }
        }

        public class Role
        {
            public string? name { get; set; }
            public object? color { get; set; }
            public string? __typename { get; set; }
        }

        public class Style
        {
            public object? activeProfilePicture { get; set; }
            public object? activePaint { get; set; }
            public string? activeEmoteSetId { get; set; }
            public string? __typename { get; set; }
        }

        public class SubscriptionInfo
        {
            public object? activePeriod { get; set; }
            public string? __typename { get; set; }
        }

        public class Ticket
        {
            public bool create { get; set; }
            public string? __typename { get; set; }
        }

        public class User
        {
            public bool manageAny { get; set; }
            public bool useCustomProfilePicture { get; set; }
            public bool manageBilling { get; set; }
            public bool manageSessions { get; set; }
            public string? __typename { get; set; }
        }

        public class Users
        {
            public Me? me { get; set; }
            public string? __typename { get; set; }
        }
    }

    public class GqlOneEmoteData
    {
        public Emotes? emotes { get; set; }

        public class Editor
        {
            public string? editorId { get; set; }
            public Permissions? permissions { get; set; }
            public string? __typename { get; set; }
        }

        public class Emote
        {
            public string? id { get; set; }
            public string? defaultName { get; set; }
            public Owner? owner { get; set; }
            public List<object>? tags { get; set; }
            public Flags? flags { get; set; }
            public List<object>? attribution { get; set; }
            public bool imagesPending { get; set; }
            public List<Image>? images { get; set; }
            public object? ranking { get; set; }
            public bool deleted { get; set; }
            public string? __typename { get; set; }
            public bool manage { get; set; }
        }

        public class Emotes
        {
            public Emote? emote { get; set; }
            public string? __typename { get; set; }
        }

        public class Flags
        {
            public bool animated { get; set; }
            public bool approvedPersonal { get; set; }
            public bool defaultZeroWidth { get; set; }
            public bool deniedPersonal { get; set; }
            public bool nsfw { get; set; }
            public bool @private { get; set; }
            public bool publicListed { get; set; }
            public string? __typename { get; set; }
        }

        public class HighestRoleColor
        {
            public string? hex { get; set; }
            public string? __typename { get; set; }
        }

        public class Image
        {
            public string? url { get; set; }
            public string? mime { get; set; }
            public int size { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int scale { get; set; }
            public int frameCount { get; set; }
            public string? __typename { get; set; }
        }

        public class MainConnection
        {
            public string? platformDisplayName { get; set; }
            public string? platformAvatarUrl { get; set; }
            public string? __typename { get; set; }
        }

        public class Owner
        {
            public string? id { get; set; }
            public MainConnection? mainConnection { get; set; }
            public Style? style { get; set; }
            public HighestRoleColor? highestRoleColor { get; set; }
            public List<Editor>? editors { get; set; }
            public string? __typename { get; set; }
        }

        public class Permissions
        {
            public Emote? emote { get; set; }
            public string? __typename { get; set; }
        }

        public class Style
        {
            public object? activeProfilePicture { get; set; }
            public object? activePaint { get; set; }
            public string? __typename { get; set; }
        }
    }

    public class GqlAddEmoteToSetData
    {
        public EmoteSets? emoteSets { get; set; }

        public class AddEmote
        {
            public string? id { get; set; }
            public string? __typename { get; set; }
        }

        public class EmoteSet
        {
            public AddEmote? addEmote { get; set; }
            public string? __typename { get; set; }
        }

        public class EmoteSets
        {
            public EmoteSet? emoteSet { get; set; }
            public string? __typename { get; set; }
        }
    }

}
