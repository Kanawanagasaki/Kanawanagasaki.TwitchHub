using System.Net;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Kanawanagasaki.TwitchHub.Services;

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

    private TwitchChatService _chat;
    private CommandsService _commands;
    private ILogger<TwitchChatService> _logger;
    private TtsService _tts;
    private JsEnginesService _jsEngines;

    private TwitchAuthModel _authModel;

    public TwitchClient Client;
    public JsEngine Js;

    public TwitchChatMessagesService(
        TwitchChatService chat,
        CommandsService commands,
        ILogger<TwitchChatService> logger,
        TtsService tts,
        JsEnginesService jsEngines)
    {
        _chat = chat;
        _commands = commands;
        _logger = logger;
        _tts = tts;
        _jsEngines = jsEngines;
    }

    public void Connect(TwitchAuthModel authModel, string channel)
    {
        if (_authModel is not null)
            _chat.Unlisten(_authModel, this);

        _authModel = authModel;

        Client = _chat.GetClient(authModel, this, channel);
        Client.OnMessageReceived += MessageReceived;

        Js = _jsEngines.GetEngine(channel);
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

            if (res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message) && res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.OriginalMessage))
                _tts.AddTextToRead(res.Original.Username, res.Original.Message);
        }

        if (res.ShouldReply)
            Client.SendMessage(ev.ChatMessage.Channel, res.Reply);

        OnMessage?.Invoke(res);
    }

    public void Dispose()
    {
        if (_authModel is not null)
            _chat.Unlisten(_authModel, this);
    }
}