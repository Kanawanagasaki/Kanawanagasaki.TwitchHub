namespace Kanawanagasaki.TwitchHub.Services;

using System.Net;
using System.Linq;
using System.Threading;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors;
using HtmlAgilityPack.CssSelectors.NetCore;
using Kanawanagasaki.TwitchHub.Data;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Microsoft.JSInterop;
using Kanawanagasaki.TwitchHub.Models;

public class TwitchChatService : IDisposable
{
    private static Dictionary<string, (string code, string name, string slug)> CODE_LANGUAGES = new()
    {
        { "cs", ("cs", "c#", "csharp") },
        { "js", ("js", "javascript", "javascript") },
        { "ts", ("ts", "typescript", "typescript") },
        { "css", ("css", "css", "css") },
        { "html", ("html", "html", "html") },
        { "json", ("json", "json", "json") },
        { "java", ("java", "java", "java") },
        { "cpp", ("cpp", "c++", "cpp") }
    };

    public TwitchClient Client { get; private set; }
    public bool IsConnected => Client is not null && Client.IsConnected;

    public event Action<ProcessedChatMessage> OnMessage;

    private TwitchAuthService _twAuth;
    private TwitchApiService _twApi;
    private CommandsService _commands;
    private ILogger<TwitchChatService> _logger;
    private List<string> _channelsToJoin = new();
    private IServiceScopeFactory _serviceFactory;
    private JavaScriptService _js;
    private TtsService _tts;

    public TwitchChatService(TwitchAuthService twAuth,
        TwitchApiService twApi,
        CommandsService commands,
        ILogger<TwitchChatService> logger,
        IServiceScopeFactory serviceFactory,
        JavaScriptService js,
        TtsService tts)
    {
        _twAuth = twAuth;
        _twApi = twApi;
        _commands = commands;
        _logger = logger;
        _serviceFactory = serviceFactory;
        _js = js;
        _tts = tts;
    }

    public void Connect(TwitchAuthModel authModel)
    {
        ConnectionCredentials credentials = new ConnectionCredentials(authModel.Username, authModel.AccessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new WebSocketClient(clientOptions);
        Client = new TwitchClient(customClient);
        Client.Initialize(credentials);

        Client.OnConnected += (_, _) =>
        {
            _logger.LogInformation($"[{authModel.Username}] Connected");
            foreach (var channel in _channelsToJoin)
                Client.JoinChannel(channel);
        };
        Client.OnJoinedChannel += (_, ev) =>
        {
            _logger.LogInformation($"[{authModel.Username}] Channel {ev.Channel} joined");
            if (!_channelsToJoin.Contains(ev.Channel))
                Client.LeaveChannel(ev.Channel);
        };
        Client.OnLeftChannel += (_, ev) =>
        {
            _logger.LogInformation($"[{authModel.Username}] Channel {ev.Channel} left");
            if (_channelsToJoin.Contains(ev.Channel))
                Client.JoinChannel(ev.Channel);
        };
        Client.OnDisconnected += (_, _) =>
        {
            _logger.LogInformation($"[{authModel.Username}] Disconnected");
            if(Client is not null)
                Client.Connect();
        };
        Client.OnMessageReceived += MessageReceived;
        // Client.OnLog += (_, ev) => _logger.LogDebug($"LOG:[{ev.BotUsername}] {ev.Data}");

        Client.Connect();
    }

    public void JoinChannel(string channel)
    {
        lock (_channelsToJoin)
            if (!_channelsToJoin.Contains(channel))
                _channelsToJoin.Add(channel);

        _js.CreateEngine(channel);

        if (IsConnected)
            Client.JoinChannel(channel);
    }

    public void LeaveChannel(string channel)
    {
        lock (_channelsToJoin)
            if (_channelsToJoin.Contains(channel))
                _channelsToJoin.Remove(channel);

        _js.DisposeEngine(channel);

        if (IsConnected)
            Client.LeaveChannel(channel);
    }

    private async void MessageReceived(object sender, OnMessageReceivedArgs ev)
    {
        _logger.LogInformation($"{ev.ChatMessage.DisplayName}: {ev.ChatMessage.Message}");

        var res = await _commands.ProcessMessage(ev.ChatMessage, this);

        if (!res.IsCommand)
        {
            #region Checking for url
            var split = ev.ChatMessage.Message.Split(" ");
            foreach (var word in split)
            {
                if (!Uri.TryCreate(word, UriKind.Absolute, out var uri))
                    continue;
                if (uri.Scheme != "http" && uri.Scheme != "https")
                    continue;

                try
                {
                    var handler = new HttpClientHandler
                    {
                        Proxy = new WebProxy
                        {
                            Address = new Uri("socks5://127.0.0.1:9050")
                        }
                    };
                    using HttpClient http = new HttpClient(handler);
                    http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.46 Kanawanagasaki/1 (Gimme your html please)");
                    var response = await http.GetAsync(uri);
                    if (!response.IsSuccessStatusCode) continue;

                    if (response.Content.Headers.ContentType.MediaType == "text/html")
                    {
                        var html = await response.Content.ReadAsStringAsync();
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var query = HttpUtility.ParseQueryString(uri.Query);

                        if (uri.Host.EndsWith("youtu.be") || (uri.Host.EndsWith("youtube.com") && query.AllKeys.Contains("v")))
                        {
                            string videoId = uri.Host.EndsWith("youtu.be") ? uri.LocalPath.Substring(1) : query["v"];
                            res = res.WithYoutubeVideo(videoId);
                        }

                        var titleTag = doc.QuerySelector("title");
                        var titleOgTag = doc.QuerySelector("meta[property=og:title]");
                        var descriptionTag = doc.QuerySelector("meta[name=description]");
                        var descriptionOgTag = doc.QuerySelector("meta[property=og:description]");
                        var imageOgTag = doc.QuerySelector("meta[property=og:image]")?.GetAttributeValue("content", "");

                        if (!string.IsNullOrWhiteSpace(imageOgTag) && Uri.IsWellFormedUriString(imageOgTag, UriKind.Relative))
                            imageOgTag = uri.Scheme + "://" + uri.Host + imageOgTag;

                        var htmlPreview = new HtmlPreviewCustomContent
                        {
                            Uri = uri,
                            Title = titleOgTag is not null
                                    ? titleOgTag.GetAttributeValue("content", "")
                                    : titleTag?.InnerText,
                            Description = descriptionOgTag is not null
                                    ? descriptionOgTag.GetAttributeValue("content", "")
                                    : descriptionTag?.GetAttributeValue("content", ""),
                            ImageUrl = imageOgTag
                        };

                        if (!string.IsNullOrWhiteSpace(htmlPreview.Title))
                            res = res.WithCustomContent(htmlPreview)
                                    .WithReply($"@{res.Original.DisplayName} shared a page titled {htmlPreview.Title}");

                    }
                    else if (response.Content.Headers.ContentType.MediaType.StartsWith("image/"))
                    {
                        var peopleThatITrust = new string[] { "ljtech", "stoney_eagle", "vvvvvedma_anna" };
                        if (ev.ChatMessage.IsBroadcaster || ev.ChatMessage.IsModerator || ev.ChatMessage.IsVip || peopleThatITrust.Contains(ev.ChatMessage.Username))
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            string base64 = $"data:{response.Content.Headers.ContentType.MediaType};base64," + Convert.ToBase64String(bytes);
                            res = res.WithImage(base64);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to load preview for url, " + e.Message);
                    continue;
                }
            }
            #endregion

            int backtickIndex = ev.ChatMessage.Message.IndexOf("`");
            int whitespaceIndex = ev.ChatMessage.Message.IndexOf(" ", backtickIndex + 1);
            if (backtickIndex >= 0 && whitespaceIndex >= 0 && ev.ChatMessage.Message.Length - backtickIndex >= 5)
            {
                var language = ev.ChatMessage.Message.Substring(backtickIndex + 1, whitespaceIndex - backtickIndex - 1);
                int closingBacktickIndex = ev.ChatMessage.Message.IndexOf("`", backtickIndex + language.Length);
                if (CODE_LANGUAGES.ContainsKey(language) && closingBacktickIndex >= 0)
                {
                    var code = ev.ChatMessage.Message.Substring(backtickIndex + language.Length + 2, closingBacktickIndex - language.Length - 2 - backtickIndex);
                    var className = CODE_LANGUAGES[language];
                    var content = new CodeContent(code, className);
                    res = res.WithCode(content);
                }
            }

            if(res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message) && res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.OriginalMessage))
                _tts.AddTextToRead(res.Original.Message);
        }

        if (res.ShouldReply)
            Client.SendMessage(ev.ChatMessage.Channel, res.Reply);

        OnMessage?.Invoke(res);
    }

    public void Dispose()
    {
        if(Client is not null)
        {
            Client.Disconnect();
            Client = null;
        }
    }
}
