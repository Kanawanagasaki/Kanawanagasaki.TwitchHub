namespace Kanawanagasaki.TwitchHub.Services;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Models;
using Microsoft.AspNetCore.Http.Features;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Communication.Events;

public class TwitchChatMessagesService
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

    public event Action<ProcessedChatMessage>? OnMessage;
    public event Action<string>? OnMessageDelete;
    public event Action<string>? OnUserSuspend;

    private TwitchChatService _chat;
    private CommandsService _commands;
    private ILogger<TwitchChatService> _logger;
    private TtsService _tts;
    private LlamaService _llama;
    private EmotesService _emotes;

    private ConcurrentDictionary<string, (TwitchClient client, TwitchAuthModel botAuth)> _botnameToClient = new();

    public TwitchChatMessagesService(
        TwitchChatService chat,
        CommandsService commands,
        ILogger<TwitchChatService> logger,
        TtsService tts,
        LlamaService llama,
        EmotesService emotes)
    {
        _chat = chat;
        _commands = commands;
        _logger = logger;
        _tts = tts;
        _llama = llama;
        _emotes = emotes;
    }

    public void Connect(TwitchAuthModel botAuth, string channel)
    {
        if (_botnameToClient.ContainsKey(botAuth.Username.ToLower()))
            return;

        var client = _chat.GetClient(botAuth, this, channel);
        client.OnMessageReceived += MessageReceived;
        client.OnMessageCleared += MessageDeleted;
        client.OnUserTimedout += UserTimeout;
        client.OnUserBanned += UserBanned;
        client.OnError += ClientError;

        _botnameToClient.AddOrUpdate(botAuth.Username.ToLower(), (client, botAuth), (_, _) => (client, botAuth));
    }

    public void SendMessage(string botname, string channel, string message)
    {
        if (_botnameToClient.TryGetValue(botname.ToLower(), out var item))
            item.client.SendMessage(channel, message);
    }

    public void SendMessage(string botname, string channel, string replyId, string message)
    {
        if (_botnameToClient.TryGetValue(botname.ToLower(), out var item))
            item.client.SendReply(channel, replyId, message);
    }

    public void Disconnect(TwitchAuthModel botAuth)
    {
        if (!_botnameToClient.TryRemove(botAuth.Username.ToLower(), out var item))
            return;

        item.client.OnMessageReceived -= MessageReceived;
        item.client.OnMessageCleared -= MessageDeleted;
        item.client.OnUserTimedout -= UserTimeout;
        item.client.OnUserBanned -= UserBanned;
        item.client.OnError -= ClientError;

        _chat.Unlisten(botAuth, this);
    }

    public void Disconnect(TwitchAuthModel botAuth, string channel)
    {
        if (!_botnameToClient.TryGetValue(botAuth.Username.ToLower(), out var item))
            return;

        item.client.LeaveChannel(channel);
        if (item.client.JoinedChannels.Any())
            return;

        item.client.OnMessageReceived -= MessageReceived;
        item.client.OnMessageCleared -= MessageDeleted;
        item.client.OnUserTimedout -= UserTimeout;
        item.client.OnUserBanned -= UserBanned;
        item.client.OnError -= ClientError;

        _chat.Unlisten(botAuth, this);

        _botnameToClient.TryRemove(botAuth.Username.ToLower(), out _);
    }

    private void MessageReceived(object? sender, OnMessageReceivedArgs ev)
    {
        _logger.LogInformation($"{ev.ChatMessage.DisplayName}: {ev.ChatMessage.Message}");

        if (!_botnameToClient.TryGetValue(ev.ChatMessage.BotUsername.ToLower(), out var item))
        {
            _logger.LogError("Unregistered bot {BotName} received a message", ev.ChatMessage.BotUsername);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var res = await _commands.ProcessMessage(item.botAuth, ev.ChatMessage, this);

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
                                    Address = new Uri("socks5://192.168.0.51:12345")
                                }
                            };
                            using HttpClient http = new HttpClient(handler);
                            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.46 Kanawanagasaki/1 (Gimme your html please)");
                            var response = await http.GetAsync(uri);
                            if (!response.IsSuccessStatusCode)
                                continue;

                            if (response.Content.Headers.ContentType?.MediaType == "text/html")
                            {
                                var html = await response.Content.ReadAsStringAsync();
                                var doc = new HtmlDocument();
                                doc.LoadHtml(html);

                                var query = HttpUtility.ParseQueryString(uri.Query);

                                if (uri.Host.EndsWith("youtu.be") || (uri.Host.EndsWith("youtube.com") && query.AllKeys.Contains("v")))
                                {
                                    var videoId = (uri.Host?.EndsWith("youtu.be") ?? false) ? uri.LocalPath.Substring(1) : query["v"];
                                    if (videoId is not null)
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
                            else if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") ?? false)
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

                    int backtickIndex = ev.ChatMessage.Message.IndexOf('`');
                    while (backtickIndex >= 0 && backtickIndex < ev.ChatMessage.Message.Length - 1 && ev.ChatMessage.Message[backtickIndex + 1] == '`')
                        backtickIndex++;

                    int whitespaceIndex = ev.ChatMessage.Message.IndexOf(' ', backtickIndex + 1);
                    if (backtickIndex >= 0 && whitespaceIndex >= 0 && ev.ChatMessage.Message.Length - backtickIndex >= 5)
                    {
                        var language = ev.ChatMessage.Message.Substring(backtickIndex + 1, whitespaceIndex - backtickIndex - 1);
                        int closingBacktickIndex = ev.ChatMessage.Message.IndexOf('`', backtickIndex + language.Length);
                        if (CODE_LANGUAGES.TryGetValue(language, out var className) && closingBacktickIndex >= 0)
                        {
                            var code = ev.ChatMessage.Message.Substring(backtickIndex + language.Length + 2, closingBacktickIndex - language.Length - 2 - backtickIndex);
                            var content = new CodeContent(code, className);
                            res = res.WithCode(content);
                        }
                    }

                    if (res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Message) && res.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.OriginalMessage))
                        _tts.AddTextToRead(res.Original.Id, res.Original.Username, res.Original.Message);
                }

                if (res.ShouldReply && !string.IsNullOrWhiteSpace(res.Reply))
                {
                    var chunks = SplitMessageIntoChunks(res.Reply, 450);
                    foreach (var chunk in chunks)
                        item.client.SendReply(ev.ChatMessage.Channel, res.Original.Id, chunk);
                }

                var allEmotes = new Dictionary<string, ThirdPartyEmote>();

                try
                {
                    var globalEmotes = await _emotes.GetGlobal();
                    var channelEmotes = await _emotes.GetChannel(ev.ChatMessage.RoomId, ev.ChatMessage.Channel);
                    foreach (var (k, v) in globalEmotes)
                        allEmotes[k] = v;
                    foreach (var (k, v) in channelEmotes)
                        allEmotes[k] = v;
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to fetch emtoes: " + e.Message);
                }
                res.ParseEmotes(allEmotes);
                OnMessage?.Invoke(res);
                await _llama.OnTwitchChatMessage(this, res);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogError(e.StackTrace);
            }
        });
    }

    private void MessageDeleted(object? sender, OnMessageClearedArgs ev)
    {
        _tts.DeleteById(ev.TargetMessageId);
        OnMessageDelete?.Invoke(ev.TargetMessageId);
    }

    private void UserTimeout(object? sender, OnUserTimedoutArgs ev)
    {
        _tts.DeleteByUsername(ev.UserTimeout.Username);
        OnUserSuspend?.Invoke(ev.UserTimeout.Username);
    }

    private void UserBanned(object? sender, OnUserBannedArgs ev)
    {
        _tts.DeleteByUsername(ev.UserBan.Username);
        OnUserSuspend?.Invoke(ev.UserBan.Username);
    }

    private void ClientError(object? sender, OnErrorEventArgs ev)
    {
        _logger.LogError(ev.Exception.Message);
    }

    private List<string> SplitMessageIntoChunks(string message, int chunkLength)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(message) || chunkLength <= 0)
            return chunks;

        var sentences = Regex.Split(message, @"(?<=[.!?])\s+");
        var currentChunk = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length + 1 <= chunkLength)
            {
                if (0 < currentChunk.Length)
                    currentChunk.Append(" ");
                currentChunk.Append(sentence);
            }
            else
            {
                if (0 < currentChunk.Length)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                if (sentence.Length <= chunkLength)
                    currentChunk.Append(sentence);
                else
                    SplitSentenceIntoChunks(sentence, chunkLength, chunks);
            }
        }

        if (0 < currentChunk.Length)
            chunks.Add(currentChunk.ToString().Trim());

        return chunks;
    }
    private void SplitSentenceIntoChunks(string sentence, int chunkLength, List<string> chunks)
    {
        var words = sentence.Split(' ');
        var currentChunk = new StringBuilder();

        foreach (var word in words)
        {
            if (chunkLength < word.Length)
            {
                if (0 < currentChunk.Length)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }
                SplitWordIntoChunks(word, chunkLength, chunks);
            }
            else
            {
                if (currentChunk.Length + word.Length + 1 <= chunkLength)
                {
                    if (0 < currentChunk.Length)
                        currentChunk.Append(" ");
                    currentChunk.Append(word);
                }
                else
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                    currentChunk.Append(word);
                }
            }
        }

        if (0 < currentChunk.Length)
            chunks.Add(currentChunk.ToString().Trim());
    }
    private void SplitWordIntoChunks(string word, int chunkLength, List<string> chunks)
    {
        for (int i = 0; i < word.Length; i += chunkLength)
        {
            int length = Math.Min(chunkLength, word.Length - i);
            chunks.Add(word.Substring(i, length));
        }
    }

}