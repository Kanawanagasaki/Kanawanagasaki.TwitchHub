namespace Kanawanagasaki.TwitchHub.Services;

using System.Net;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Communication.Events;

public class TwitchChatMessagesService : IDisposable
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

    public event Action<ProcessedChatMessage> OnMessage;
    public event Action<string> OnMessageDelete;
    public event Action<string> OnUserSuspend;

    private TwitchChatService _chat;
    private CommandsService _commands;
    private ILogger<TwitchChatService> _logger;
    private TtsService _tts;
    private JsEnginesService _jsEngines;
    private LlamaService _llama;
    private EmotesService _emotes;

    private TwitchAuthModel _authModel;

    public TwitchClient Client;
    public JsEngine Js;

    public TwitchChatMessagesService(
        TwitchChatService chat,
        CommandsService commands,
        ILogger<TwitchChatService> logger,
        TtsService tts,
        JsEnginesService jsEngines,
        LlamaService llama,
        EmotesService emotes)
    {
        _chat = chat;
        _commands = commands;
        _logger = logger;
        _tts = tts;
        _jsEngines = jsEngines;
        _llama = llama;
        _emotes = emotes;
    }

    public void Connect(TwitchAuthModel authModel, string channel)
    {
        if (_authModel is not null)
            _chat.Unlisten(_authModel, this);

        _authModel = authModel;

        Client = _chat.GetClient(authModel, this, channel);
        Client.OnMessageReceived += MessageReceived;
        Client.OnMessageCleared += MessageDeleted;
        Client.OnUserTimedout += UserTimeout;
        Client.OnUserBanned += UserBanned;
        Client.OnError += ClientError;

        Js = _jsEngines.GetEngine(channel);
    }

    public void SendMessage(string channel, string message)
    {
        Client.SendMessage(channel, message);
    }

    public void Disconnect()
    {
        if (Client is not null)
            Client.OnMessageReceived -= MessageReceived;

        if (_authModel is not null)
            _chat.Unlisten(_authModel, this);
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
            while (backtickIndex >= 0 && backtickIndex < ev.ChatMessage.Message.Length - 1 && ev.ChatMessage.Message[backtickIndex + 1] == '`')
                backtickIndex++;

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

            if (res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message) && res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.OriginalMessage))
                _tts.AddTextToRead(res.Original.Id, res.Original.Username, res.Original.Message);
        }

        if (res.ShouldReply)
            Client.SendMessage(ev.ChatMessage.Channel, res.Reply);

        var globalEmotes = await _emotes.GetGlobal();
        var channelEmotes = await _emotes.GetChannel(ev.ChatMessage.RoomId, ev.ChatMessage.Channel);
        var allEmotes = new Dictionary<string, ThirdPartyEmote>();
        foreach (var (k, v) in globalEmotes)
            allEmotes[k] = v;
        foreach (var (k, v) in channelEmotes)
            allEmotes[k] = v;
        res.ParseEmotes(allEmotes);
        OnMessage?.Invoke(res);
        await _llama.OnTwitchChatMessage(this, res);
    }

    private void MessageDeleted(object sernder, OnMessageClearedArgs ev)
    {
        _tts.DeleteById(ev.TargetMessageId);
        OnMessageDelete?.Invoke(ev.TargetMessageId);
    }

    private void UserTimeout(object sernder, OnUserTimedoutArgs ev)
    {
        _tts.DeleteByUsername(ev.UserTimeout.Username);
        OnUserSuspend?.Invoke(ev.UserTimeout.Username);
    }

    private void UserBanned(object sernder, OnUserBannedArgs ev)
    {
        _tts.DeleteByUsername(ev.UserBan.Username);
        OnUserSuspend?.Invoke(ev.UserBan.Username);
    }

    private void ClientError(object sender, OnErrorEventArgs ev)
    {
        _logger.LogError(ev.Exception.Message);
    }

    public void Dispose()
    {
        if (_authModel is not null)
            _chat.Unlisten(_authModel, this);
    }
}