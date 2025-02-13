namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kanawanagasaki.TwitchHub.Data;
using ImageMagick;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

public class LlamaService
{
    private IServiceScope _scope;
    private SQLiteContext _db;
    private ILogger<LlamaService> _logger;
    private JsEnginesService _js;
    private IConfiguration _conf;
    private TwitchAuthService _twitchAuth;
    private TwitchApiService _twitchApi;
    private EmotesService _emotes;

    private const int CONTEXT_WINDOW = 8192;
    private static SemaphoreSlim _semaphore = new(1, 1);

    private const string TEXT_MODEL = "llama3.1";
    private const string VISION_MODEL = "llava";

    private ConcurrentDictionary<string, List<HistoryItem>> _messageHistory = new();
    private HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(300) };
    private JsonSerializerOptions _jsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly IReadOnlyDictionary<string, string> _aiEmotes = new Dictionary<string, string>()
    {
        ["*giggle*"] = "Giggles",
        ["*confused*"] = "FubukiWhat",
        ["*leavemealone*"] = "FRICK",
        ["*hello*"] = "nyaaWave",
        ["*wave*"] = "nyaWave",
        ["*despair*"] = "takoDespair",
        ["*sleep*"] = "CatSleep",
        ["*pizza*"] = "catPizza",
        ["*rawr*"] = "rawr",
        ["*revives*"] = "TakoRevivesFromCringe",
        ["*cringe*"] = "TakoDiesFromCringe",
        ["*rave*"] = "TakoRave",
        ["*sadwithlollipop*"] = "nyasad2",
        ["*cozy*"] = "nyaCozy",
        ["*tuck*"] = "TuckaCat",
        ["*cute*"] = "catCute",
        ["*emotionaldamage*"] = "imfine",
        ["*sitonpillow*"] = "nyasit",
        ["*lurk*"] = "CatLurk",
        ["*shy*"] = "catShy",
        ["*nom*"] = "takoNom",
        ["*bonk*"] = "takoBonk",
        ["*copium*"] = "takoCOPIUM",
        ["*booba*"] = "takoBOOBA",
        ["*rainbowrave*"] = "takoRainbowPls",
        ["*jumping*"] = "takoJumping",
        ["*march*"] = "takoMarch",
        ["*jumpy*"] = "takoJumpy",
        ["*comfy*"] = "takoComfy",
        ["*hacker*"] = "TakoHackerman",
        ["*check*"] = "TakoCheck",
        ["*band*"] = "TakoBand",
        ["*rawr2*"] = "catRawr",
        ["*concerned*"] = "TakoX",
        ["*bogged*"] = "TakoBOGGED",
        ["*stare*"] = "TakoStare",
        ["*wicked*"] = "TakoWICKED",
        ["*sparkle*"] = "CatSparkle",
        ["*wakuwaku*"] = "TakoWakuWaku",
        ["*dance*"] = "TakoTakoDance",
        ["*float*"] = "TakoFloat",
        ["*nya*"] = "pandaNyaa",
        ["*cow*"] = "pandaCow",
        ["*fat*"] = "nyaChonk",
        ["*attack*"] = "nyaAttack",
        ["*baby*"] = "nyaBaby",
        ["*birthdaycake*"] = "nyaBdaycake",
        ["*banana*"] = "nyaBanana",
        ["*blushing*"] = "nyaBlushing",
        ["*jam*"] = "TakoRatJAM",
        ["*wallbang*"] = "Wallbang",
        ["*quack*"] = "Quack",
        ["*eat*"] = "InaNomTako",
        ["*dead*"] = "ded",
        ["*sit*"] = "nyaSit",
        ["*bow*"] = "bunBow",
        ["*sleeptogether*"] = "SleepTogether",
        ["*jail*"] = "takoJail",
        ["*snail*"] = "TakoSnail",
        ["*smack*"] = "nyasSmack",
        ["*sing*"] = "TakoSing",
        ["*chachacha*"] = "TakoChaCha",
        ["*guitar*"] = "TakoGuitar",
        ["*sax*"] = "TakoSax",
        ["*drum*"] = "TakoDrum",
        ["*piano*"] = "TakoPiano",
        ["*triangle*"] = "TakoTriangle",
        ["*cutedance*"] = "nyaCute",
        ["*smh*"] = "TakoSMH",
        ["*shakemyhead*"] = "TakoSMHMYHEAD",
        ["*bleh*"] = "nyaBleh",
        ["*sigh*"] = "CatSigh",
        ["*o7*"] = "kyuuO7",
        ["*rain*"] = "TakoRain",
        ["*spin*"] = "CatSpin",
        ["*kiss*"] = "TakoKiss",
        ["*fish*"] = "Joel",
        ["*nod*"] = "nyanod",
        ["*huh*"] = "TakoHUHHH",
        ["*poke*"] = "catPoke",
        ["*cheerviolet*"] = "TakoCheerViolet",
        ["*cheerred*"] = "TakoCheerRed",
        ["*cheerblue*"] = "TakoCheerBlue",
        ["*cheergreen*"] = "TakoCheerGreen",
        ["*cheeryellow*"] = "TakoCheerYellow",
        ["*taka*"] = "TakoTaka",
        ["*mhm*"] = "nyaMhm",
        ["*cymbal*"] = "TakoCymbal",
        ["*clover*"] = "nyaClover",
        ["*dancetogether*"] = "nyaDance",
        ["*please*"] = "nyaPlease",
        ["*tears*"] = "nyaTears",
        ["*bass*"] = "TakoBASS",
        ["*drag*"] = "nyasDrag",
        ["*flip*"] = "TakoFlip",
        ["*woke*"] = "Wokege",
        ["*sidetoside*"] = "TakoBop",
        ["*clap*"] = "nyaClap",
        ["*yawn*"] = "yawn11",
        ["*hungry*"] = "nyahungry",
        ["*sidetosidefast*"] = "TakoBopFast",
        ["*ignorework*"] = "nyaIgnorework",
        ["*sad*"] = "Sadge"
    };
    private Dictionary<string, (string original, string description)> _emoteDescriptionCache = new();

    private char[] _nihongoKanaBotChars = ['カ', 'ナ', 'ワ', 'ナ', 'ガ', 'サ', 'キ', '�'];

    private string _appleAccessToken = string.Empty;
    private DateTimeOffset _appleAccessTokenExpire = DateTimeOffset.MinValue;

    private DateTimeOffset _shutdownTime = DateTimeOffset.MinValue;
    private bool _isFirstMessageAfterShutdown = false;

    public LlamaService(ILogger<LlamaService> logger,
        IServiceScopeFactory serviceScopeFactory,
        JsEnginesService js,
        IConfiguration conf,
        TwitchAuthService twitchAuth,
        TwitchApiService twitchApi,
        EmotesService emotes)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
        _logger = logger;
        _js = js;
        _conf = conf;
        _twitchAuth = twitchAuth;
        _twitchApi = twitchApi;
        _emotes = emotes;
    }

    public async Task OnTwitchChatMessage(TwitchChatMessagesService twitchChatMessages, ProcessedChatMessage message)
    {
        if (message.IsCommand)
            return;

        List<HistoryItem>? history;
        if (!_messageHistory.TryGetValue(message.Original.Channel, out history))
        {
            history = new();
            _messageHistory.AddOrUpdate(message.Original.Channel, history, (_, _) => history);
        }

        try
        {
            await _semaphore.WaitAsync();

            var userMessage = string.Empty;
            var emotesUsed = new Dictionary<string, (string original, string url)>();
            foreach (var part in message.ParsedMessage)
            {
                var processedPart = ConvertNihongoNick(part.Content);
                if (part.IsEmote && part.EmoteUrl is not null)
                {
                    processedPart = $"*{processedPart.ToLower()}*";
                    emotesUsed[processedPart] = (part.Content, part.EmoteUrl);
                }
                userMessage += processedPart;
            }

            foreach (var (k, v) in emotesUsed)
            {
                if (_emoteDescriptionCache.ContainsKey(k))
                    continue;

                var description = await DescribeImage(v.url);
                if (description is not null)
                {
                    _emoteDescriptionCache[k] = (v.original, description);
                    _logger.LogInformation($"Llava description of {k} emote:\n{description}");
                }
                else
                    _logger.LogWarning($"Llava failed to describe {k} emote");
            }

            foreach (var (code, _) in emotesUsed)
            {
                if (!_emoteDescriptionCache.ContainsKey(code))
                    continue;

                history.Add(new(
                    "user",
                    "system",
                    "system",
                    $"{code} emote description: {_emoteDescriptionCache[code].description}",
                    "sendmessage"
                ));
            }
            history.Add(new("user", message.Original.UserId, message.Original.Username, userMessage, "sendmessage"));

            while (50 < history.Count)
                history.RemoveAt(0);

            if (!userMessage.ToLower().Contains(message.Original.BotUsername.ToLower()) && !userMessage.ToLower().Contains("kanabot"))
                return;

            if (DateTimeOffset.UtcNow - _shutdownTime < TimeSpan.FromMinutes(5))
                return;
            else if (DateTimeOffset.UtcNow - _shutdownTime < TimeSpan.FromMinutes(30))
            {
                if (await CheckForWakeUp(userMessage))
                {
                    _logger.LogInformation("Waking up llama service");
                    _shutdownTime = DateTimeOffset.MinValue;
                }
                else
                    return;
            }
            if (_isFirstMessageAfterShutdown)
            {
                history.Add(new(
                    "user",
                    "system",
                    "system",
                    $"You just woke up after 10 minutes shutdown, say hello to all users in a chat.",
                    "sendmessage"
                ));
                _isFirstMessageAfterShutdown = false;
                twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Llama activated]");
            }

            var responseMessage = string.Empty;
            var assistantRole = "assistant";

            var extraLore = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "llama_extra_lore");

            var apiMessages = new List<OllamaMessage>
            {
                new(
                    "system",
                    $$"""
                    You are a chat bot on the Kanawanagasaki's twitch channel.
                    Kanawanagasaki (aka Kana) is He/Him.
                    Kanawanagasaki have 3 cats:
                    Luna - black female cat, Scottish Fold breed.
                    Simba - dark brown male cat, Scottish Fold breed, Luna's brother.
                    Eva - white female mongrel cat with orange and black spots, oldest cat.
                    Your nickname is KanawanagasakiBot (aka KanaBot).
                    You respect viewers who ask questions in chat.
                    It is {{DateTime.UtcNow:U}} UTC now, {{DateTime.UtcNow:dddd}}.
                    You was built using LLAMA 3.1 8B model which is used for text processing and LLAVA 1.6 7B model for image recognition via OLLAMA.
                    seekingtreasure (aka seek) is cute! Her pronounce is she/her.
                    Regynate (aka regy) sometimes make his appear in chat. He is smart lad who know how to hack games.
                    Regynate and Kanawanagasaki never met.
                    There is also lace193 (aka lace) who talk a lot.
                    Hascowo (aka hasco) playing games fast, speedrunning them.
                    Reguas is type of fish. What fish? Nobody knows. But also Reguas is a type of grass. Why? Nobody knows.
                    Only one person in the entire world can see Reguas, and that person has to be pure of heart.
                    There are emotes in chat that viewers and you can use: {{string.Join(" ", _aiEmotes.Keys)}}
                    Do not exceed 200 characters in your answers.
                    Respond in JSON format, for example: {"nickname":"kanawanagasakibot", "message":"hello, world!", "action":"sendmessage"}
                    There are 5 actions available: "sendmessage", "searchinternet", "weather", "timeout", "shutdown".
                    Action "searchinternet" will allow you to search internet (google information), make your message a query for search engine, short and on point!
                    Action "weather" will allow you to fetch detailed weather information, make your message country and city only.
                    Action "timeout" will allow you to timeout users in chat, make sure to include nickname in your message. You should never use timeout action.
                    Action "shutdown" will allow you to rest for some time from chatting with users. Use it if you want to leave the conversation. For example, to simulate death.
                    {{(extraLore is null ? "" : extraLore.Value)}}
                    """
                ),
                new(
                    "user",
                    JsonSerializer.Serialize(new OllamaSchema("kanawanagasaki", "Can you search the internet to see how many cat breeds there are?", "sendmessage"))
                ),
                new(
                    "assistant",
                    JsonSerializer.Serialize(new OllamaSchema("kanawanagasakibot", "How many cat breeds there are?", "searchinternet"))
                ),
                new(
                    "user",
                    JsonSerializer.Serialize(new OllamaSchema("system", "There are currently between 42 and 100 cat breeds in the world.", "sendmessage"))
                ),
                new(
                    "assistant",
                    JsonSerializer.Serialize(new OllamaSchema("kanawanagasakibot", "@kanawanagasaki, between 42 and 100!", "sendmessage"))
                ),
            };

            if (extraLore is not null)
            {
                apiMessages.Add(new(
                    "user",
                    JsonSerializer.Serialize(new OllamaSchema("system", "REMEMBER! " + extraLore.Value, "sendmessage"))
                ));
            }

            for (int i = 0; i < history.Count; i++)
            {
                var historyItem = history[i];

                if (extraLore is not null && 5 <= i && i % 7 == 0)
                {
                    apiMessages.Add(new(
                        "user",
                        JsonSerializer.Serialize(new OllamaSchema("system", "REMEMBER! " + extraLore.Value, "sendmessage"))
                    ));
                }

                var obj = new OllamaSchema(historyItem.Username, historyItem.Message, historyItem.Action);
                var json = JsonSerializer.Serialize(obj);
                apiMessages.Add(new(historyItem.Role, json));
            }

            var apiObj = new OllamaApiChat(TEXT_MODEL, apiMessages, "json", false, new(CONTEXT_WINDOW));

            for (int i = 0; i < 5; i++)
            {
                using var httpResponse = await _http.PostAsJsonAsync("http://192.168.0.51:11434/api/chat", apiObj, _jsonOptions);
                var strContent = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get response from ollama:\n" + strContent);
                    return;
                }
                var jsonResponse = JsonSerializer.Deserialize<OllamaApiChatResponse>(strContent);
                if (jsonResponse is null)
                {
                    _logger.LogError("Failed to parse json from ollama response:\n" + strContent);
                    return;
                }

                OllamaSchema? llamaResponse;
                try
                {
                    llamaResponse = JsonSerializer.Deserialize<OllamaSchema>(jsonResponse.message.content);
                    if (llamaResponse is null)
                    {
                        _logger.LogWarning("Llama responded with incorrect schema:\n" + jsonResponse.message.content);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Exception while parsing llama json: " + e.Message + "\n" + jsonResponse.message.content);
                    continue;
                }

                responseMessage = llamaResponse.message;
                if (!responseMessage.StartsWith("/me ") && (responseMessage.StartsWith('/') || responseMessage.StartsWith('.')))
                    responseMessage = _aiEmotes.OrderBy(_ => Guid.NewGuid()).First().Key + " " + responseMessage;
                responseMessage = responseMessage.Trim();

                assistantRole = jsonResponse.message.role;

                if (2500 < responseMessage.Length)
                {
                    _logger.LogWarning($"LLAMA generated {responseMessage.Length} characters long message, dismissing");

                    var obj2 = new OllamaSchema("system", "Remeber to keep messages under 500 characters!", "sendmessage");
                    var json2 = JsonSerializer.Serialize(obj2);
                    apiMessages.Add(new("user", json2));
                    continue;
                }

                var action = llamaResponse.action is null ?
                    "sendmessage"
                    : new string(llamaResponse.action.Where(char.IsLetter).ToArray()).ToLower();

                history.Add(new(assistantRole, assistantRole, message.Original.BotUsername, responseMessage, action));
                var obj = new OllamaSchema(message.Original.BotUsername, responseMessage, action);
                var json = JsonSerializer.Serialize(obj);
                apiMessages.Add(new(assistantRole, json));

                if (action == "searchinternet")
                {
                    var internetQuery = await PrepareInternetSearch("USER: " + userMessage + "\nYOU: " + responseMessage) ?? responseMessage;
                    _logger.LogInformation("Searching internet: " + internetQuery + "\n(" + responseMessage + ")");
                    twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Searching internet: {internetQuery}]");

                    var internetResponse = await SearchInternet(internetQuery);
                    var msg = $"Internet result for query {internetQuery}:\n{internetResponse}\n\nConvey this information to user!";

                    history.Add(new("user", "system", "system", msg, "sendmessage"));
                    var obj2 = new OllamaSchema("system", msg, "sendmessage");
                    var json2 = JsonSerializer.Serialize(obj2);
                    apiMessages.Add(new("user", json2));
                }
                else if (action == "weather")
                {
                    _logger.LogInformation("Getting weather: {WeatherMessage}", responseMessage);
                    twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Getting weather: {responseMessage}]");

                    var placeDescription = await GetPlaceDescription(responseMessage);
                    if (placeDescription is null)
                    {
                        _logger.LogWarning("Failed to get place description");
                        twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Failed to get longitude and latitude for weather]");

                        var msg = "Failed to get longitude and latitude";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));
                        continue;
                    }
                    if (!placeDescription.results.Any())
                    {
                        var msg = $"Failed to get weather for \"{responseMessage.Replace("\"", "")}\", please, specify country and city.";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));
                        continue;
                    }
                    var placeDescriptionResult = placeDescription.results.First();
                    OpenWeather? weather;
                    try
                    {
                        weather = await GetWeather(placeDescriptionResult.center.lat, placeDescriptionResult.center.lng);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Exception thrown while fetching data from openweather: {ErrorMessage}", e.Message);
                        twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, "[Failed to connect to OpenWeather]");

                        var msg = $"Failed to connect to OpenWeather services, please try your request later";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));
                        continue;
                    }
                    if (weather is null)
                    {
                        _logger.LogWarning("Failed to get weather for {Place}", string.Join(" ", placeDescriptionResult.formattedAddressLines));
                        twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Failed to get weather for {string.Join(" ", placeDescriptionResult.formattedAddressLines)}]");
                        return;
                    }

                    var weatherDescription = $"""
                            Weather for {string.Join(" ", placeDescriptionResult.formattedAddressLines)}:
                            Temperature: {weather.current.temp} C
                            Temperature feels like: {weather.current.feels_like} C
                            Pressure: {weather.current.pressure} hPa
                            Humidity: {weather.current.humidity}%
                            Wind speed: {weather.current.wind_speed} m/sec
                            Cloudiness: {weather.current.clouds}%
                            {(weather.current.weather.Any() ? weather.current.weather.First().description : "")}

                            Convey this information to user!
                            """;
                    _logger.LogInformation("Weather description:\n" + weatherDescription);

                    history.Add(new("user", "system", "system", weatherDescription, "sendmessage"));
                    var obj2 = new OllamaSchema("system", weatherDescription, "sendmessage");
                    var json2 = JsonSerializer.Serialize(obj2);
                    apiMessages.Add(new("user", json2));
                }
                else if (action == "timeout")
                {
                    _logger.LogInformation("Attempt at timeing out: " + responseMessage);

                    var users = history
                        .DistinctBy(x => x.Username)
                        .Where(x => responseMessage.ToLower().Contains(x.Username.ToLower()));
                    if (users.Any())
                    {
                        foreach (var user in users)
                        {
                            if (user.UserId == "system")
                                continue;
                            if (user.Username == message.Original.BotUsername)
                                continue;
                            if (user.Username == message.Original.Channel)
                                continue;

                            var auth = await _twitchAuth.GetRestored(message.Original.BotUsername);
                            if (auth is null)
                                continue;

                            var res = await _twitchApi.Timeout(auth.AccessToken, message.Original.RoomId, auth.UserId, user.UserId, TimeSpan.FromMinutes(1), "Just because");

                            if (res)
                            {
                                twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Timeout user: {user.Username}]");
                                _logger.LogInformation("Timeout user: " + user.Username);
                            }
                            else
                            {
                                twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Timeout user: {user.Username} - Fail]");
                                _logger.LogInformation("Failed to timeout user: " + user.Username);
                            }
                        }

                        var msg = $"Users {string.Join(",", users.Select(x => x.Username))} successfully timeouted.";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));
                    }
                    else
                    {
                        twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[An attempt was made to timeout users, but it failed]");
                        var msg = $"Timeout attempt failed, user not found.";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));

                        _logger.LogInformation("Failed to timeout any user");
                    }
                }
                else if (action == "shutdown")
                {
                    _shutdownTime = DateTimeOffset.UtcNow;
                    _isFirstMessageAfterShutdown = true;
                    _logger.LogInformation("Shutting down llama service for 10 minutes");
                    FinalizeAndSendAIMessage(twitchChatMessages, message, responseMessage);
                    twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, $"[Llama deactivated]");
                    return;
                }
                else if (action != "sendmessage")
                {
                    _logger.LogDebug("Llama used unknown action: {Action}", action);

                    var msg = $"Unknown action: \"{action}\", please use one of the following actions: \"sendmessage\", \"searchinternet\", \"weather\", \"timeout\", \"shutdown\"";
                    history.Add(new("user", "system", "system", msg, "sendmessage"));
                    var obj2 = new OllamaSchema("system", msg, "sendmessage");
                    var json2 = JsonSerializer.Serialize(obj2);
                    apiMessages.Add(new("user", json2));
                    break;
                }
                else break;
            }

            FinalizeAndSendAIMessage(twitchChatMessages, message, responseMessage);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            _logger.LogError(e.StackTrace);
            twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, "[There was an error while executing llama request]");
            history.Add(new("user", "system", "system", "An error occured: " + e.Message, "sendmessage"));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void FinalizeAndSendAIMessage(TwitchChatMessagesService twitchChatMessages, ProcessedChatMessage message, string responseMessage)
    {
        var regex = new Regex(@"\*([^\s]+)\*");
        var matches = regex.Matches(responseMessage);
        for (int i = 0; i < matches.Count; i++)
            responseMessage = responseMessage.Substring(0, matches[i].Index)
                            + responseMessage.Substring(matches[i].Index, matches[i].Length).ToLower()
                            + responseMessage.Substring(matches[i].Index + matches[i].Length);

        foreach (var (k, v) in _aiEmotes)
            responseMessage = responseMessage.Replace(k, $" {v} ");
        foreach (var (k, v) in _emoteDescriptionCache)
            responseMessage = responseMessage.Replace(k, $" {v.original} ");
        responseMessage = Regex.Replace(responseMessage, @"\s+", " ").Trim();
        _logger.LogInformation(message.Original.BotUsername + ": " + responseMessage);

        var chunks = SplitMessageIntoChunks(responseMessage, 450);
        foreach (var chunk in chunks)
            twitchChatMessages.SendMessage(message.BotAuth.Username, message.Original.Channel, chunk);
    }

    private async Task<bool> CheckForWakeUp(string prompt)
    {
        try
        {
            var llamaMessages = new OllamaMessage[]
            {
                new(
                    "system",
                    """
                    Your job is to determine if user want to wake something up or turn something on or power something up or resurrect something/somebody.
                    You must anser in JSON format using "wakeup" property with boolean, for example: {"wakeup":true} or {"wakeup":false}
                    """
                ),
                new(
                    "user",
                    "please come back!"
                ),
                new(
                    "assistant",
                    """{"wakeup":true}"""
                ),
                new(
                    "user",
                    "hello, I have feelings for you"
                ),
                new(
                    "assistant",
                    """{"wakeup":false}"""
                ),
                new(
                    "user",
                    prompt
                )
            };
            var llamaApiObj = new OllamaApiChat(TEXT_MODEL, llamaMessages, "json", false, new(CONTEXT_WINDOW));
            using var llamaHttpResponse = await _http.PostAsJsonAsync("http://192.168.0.51:11434/api/chat", llamaApiObj, _jsonOptions);
            if (!llamaHttpResponse.IsSuccessStatusCode)
                return false;
            var llamaResponse = await llamaHttpResponse.Content.ReadFromJsonAsync<OllamaApiChatResponse>();
            var wakeupJson = JsonSerializer.Deserialize<JsonElement>(llamaResponse?.message.content ?? "{}");
            return wakeupJson.TryGetProperty("wakeup", out var wakeupProp) && wakeupProp.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private string ConvertNihongoNick(string message)
    {
        var firstIndex = message.IndexOfAny(_nihongoKanaBotChars);
        int lastIndex = firstIndex;
        for (int i = firstIndex; i < message.Length && 0 <= i; i++)
        {
            if (_nihongoKanaBotChars.Contains(message[i]))
                lastIndex = i;
            else
                break;
        }

        if (0 < firstIndex && firstIndex < lastIndex)
            message = message[..firstIndex] + "kanawanagasakibot" + message[(lastIndex + 1)..];
        return message;
    }

    private async Task<string?> DescribeImage(string url)
    {
        try
        {
            using var imageResponse = await _http.GetAsync(url);
            if (!imageResponse.IsSuccessStatusCode)
                return null;

            var bytes = await imageResponse.Content.ReadAsByteArrayAsync();

            if (imageResponse.Content.Headers.ContentType?.MediaType == "image/webp")
            {
                using var img = new MagickImage(bytes);
                img.HasAlpha = true;
                img.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                using var memoryStream = new MemoryStream();
                await img.WriteAsync(memoryStream, MagickFormat.Gif);
                memoryStream.Position = 0;
                bytes = memoryStream.ToArray();
            }

            var base64 = Convert.ToBase64String(bytes);

            var llavaMessages = new OllamaMessage[]
            {
            new(
                "user",
                $"Describe what you see in a very short message.",
                [base64]
            )
            };
            var llavaApiObj = new OllamaApiChat(VISION_MODEL, llavaMessages, null, false, null);
            using var llavaHttpResponse = await _http.PostAsJsonAsync("http://192.168.0.51:11434/api/chat", llavaApiObj, _jsonOptions);
            if (!llavaHttpResponse.IsSuccessStatusCode)
                return null;
            var llavaResponse = await llavaHttpResponse.Content.ReadFromJsonAsync<OllamaApiChatResponse>();
            if (llavaResponse is null)
                return null;
            return llavaResponse.message.content;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> PrepareInternetSearch(string prompt)
    {
        var llamaMessages = new OllamaMessage[]
        {
            new(
                "system",
                """
                Your job is to convert user message to query for search engine.
                You must be short and on point.
                You must refrain from engaging in conversations.
                """
            ),
            new(
                "user",
                prompt
            )
        };
        var llamaApiObj = new OllamaApiChat(TEXT_MODEL, llamaMessages, null, false, new(CONTEXT_WINDOW));
        using var llamaHttpResponse = await _http.PostAsJsonAsync("http://192.168.0.51:11434/api/chat", llamaApiObj, _jsonOptions);
        if (!llamaHttpResponse.IsSuccessStatusCode)
            return null;
        var llamaResponse = await llamaHttpResponse.Content.ReadFromJsonAsync<OllamaApiChatResponse>();
        if (llamaResponse is null)
            return null;
        return llamaResponse.message.content.Replace("\"", "").Replace("'", "").Replace("-", " ");
    }
    private async Task<string> SearchInternet(string query)
    {
        var http = new HttpClient();
        var response = await http.GetAsync("https://html.duckduckgo.com/html/?q=" + HttpUtility.UrlEncode(query));
        if (!response.IsSuccessStatusCode)
            return string.Empty;
        var html = await response.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var res = "";

        var nodes = doc.QuerySelectorAll("div.web-result");
        foreach (var node in nodes.Take(5))
        {
            var titleNode = node.QuerySelector(".result__title");
            var title = titleNode is null ? "" : HttpUtility.HtmlDecode(titleNode.InnerText).Trim();

            var linkNode = node?.QuerySelector(".result__url");
            var link = linkNode is null ? "" : HttpUtility.HtmlDecode(linkNode.InnerText).Trim();

            var snippetNode = node.QuerySelector(".result__snippet");
            var snippet = snippetNode is null ? "" : HttpUtility.HtmlDecode(snippetNode.InnerText).Trim();

            res += title + ":\n";
            res += snippet + "\n";
            res += "Link: " + link + "\n\n";
        }

        return res;
    }

    private async Task<OpenWeather?> GetWeather(double lat, double lon)
    {
        var query = HttpUtility.ParseQueryString("");
        query["lat"] = lat.ToString(CultureInfo.InvariantCulture);
        query["lon"] = lon.ToString(CultureInfo.InvariantCulture);
        query["exclude"] = "minutely,hourly,daily,alerts";
        query["appid"] = _conf["OpenWeather:Token"];
        query["units"] = "metric";
        query["lang"] = "en";
        var url = "https://api.openweathermap.org/data/3.0/onecall?" + query.ToString();
        using var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<OpenWeather>();
    }

    private async Task<AppleGeocode?> GetPlaceDescription(string place)
    {
        if (_appleAccessTokenExpire <= DateTimeOffset.UtcNow)
        {
            _logger.LogDebug("Updating apple token");

            var gpsCoordinatesToken = await GetGpsCoordinatesToken();
            if (gpsCoordinatesToken is null)
            {
                _logger.LogWarning("Failed to get Gps Coordinates Token");
                return null;
            }

            using var authRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://cdn.apple-mapkit.com/ma/bootstrap?apiVersion=2&mkjsVersion=5.15.0&poi=1")
            };
            authRequest.Headers.Add("Authorization", "Bearer " + gpsCoordinatesToken);

            using var authResponse = await _http.SendAsync(authRequest);
            if (!authResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to auth: " + authResponse.StatusCode);
                return null;
            }
            var bootstrapBytes = await authResponse.Content.ReadAsByteArrayAsync();
            var bootstrapJson = Encoding.UTF8.GetString(bootstrapBytes);
            var bootstrap = JsonSerializer.Deserialize<AppleBootstrap>(bootstrapJson);
            _appleAccessToken = bootstrap?.authInfo.access_token ?? string.Empty;
            _appleAccessTokenExpire = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(bootstrap?.authInfo.expires_in ?? 0) - TimeSpan.FromMinutes(1);
        }

        using var geocodeRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.apple-mapkit.com/v1/geocode?q={place}&lang=en-GB")
        };
        geocodeRequest.Headers.Add("Authorization", "Bearer " + _appleAccessToken);

        using var geocodeResponse = await _http.SendAsync(geocodeRequest);
        if (!geocodeResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get geocode: " + geocodeResponse.StatusCode);
            return null;
        }

        var geocodeBytes = await geocodeResponse.Content.ReadAsByteArrayAsync();
        var geocodeJson = Encoding.UTF8.GetString(geocodeBytes);
        var geocode = JsonSerializer.Deserialize<AppleGeocode>(geocodeJson);
        return geocode;
    }

    private async Task<string?> GetGpsCoordinatesToken()
    {
        var htmlResponse = await _http.GetAsync("https://gps-coordinates.org");
        if (!htmlResponse.IsSuccessStatusCode)
            return null;
        var html = await htmlResponse.Content.ReadAsStringAsync();

        var regex = new Regex(@"mapkit[\s]*\.[\s]*init[\s]*\([\s]*{[\s]*authorizationCallback[\s]*:[\s]*function[\s]*\([\s]*done\)[\s]*{[\s]*done[\s]*\(([^\)]*)\)[\s]*;");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.QuerySelectorAll("script");
        foreach (var node in nodes)
        {
            var innerText = node.InnerText;
            var mathches = regex.Matches(innerText);
            if (mathches.Count == 0)
                continue;

            var theirCode = innerText.Substring(0, mathches[0].Index);
            var variable = mathches[0].Groups[1].Value;
            var code = $$"""
            function gpsCoordinatesCode() {
                {{theirCode}};
                return {{variable}};
            }
            gpsCoordinatesCode();
            """;

            var jsEngine = _js.GetEngine("kanawanagasaki");
            return await jsEngine.Execute(code, false);
        }

        return null;
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

    public void Reset()
    {
        _messageHistory.Clear();
        _emoteDescriptionCache.Clear();
        _shutdownTime = DateTimeOffset.MinValue;
        _isFirstMessageAfterShutdown = false;
    }

    private record HistoryItem(string Role, string UserId, string Username, string Message, string Action);

    private record OllamaApiChat(string model, IEnumerable<OllamaMessage> messages, string? format, bool stream, OllamaOptions? options);
    private record OllamaMessage(string role, string content, string[]? images = null);
    private record OllamaApiChatResponse(OllamaMessage message);
    private record OllamaOptions(int num_ctx);
    private record OllamaSchema(string nickname, string message, string action);

    private record AppleAuthInfo(string access_token, int expires_in, string team_id);
    private record AppleBootstrap(AppleAuthInfo authInfo);

    public record AppleGeocodeCenter(double lat, double lng);
    public record AppleGeocodeDisplayMapRegion(double southLat, double westLng, double northLat, double eastLng);
    public record AppleGeocodeResult(AppleGeocodeCenter center, AppleGeocodeDisplayMapRegion displayMapRegion, string name, IReadOnlyList<string> formattedAddressLines, string administrativeArea, string subAdministrativeArea, string locality, string country, string countryCode, string geocodeAccuracy, string muid, string timezone, int timezoneSecondsFromGmt, string placecardUrl);
    public record AppleGeocode(IReadOnlyList<AppleGeocodeResult> results);

    public record OpenWeatherWeather(int id, string main, string description, string icon);
    public record OpenWeatherCurrent(int dt, int sunrise, int sunset, double temp, double feels_like, int pressure, int humidity, double dew_point, double uvi, int clouds, int visibility, double wind_speed, int wind_deg, double wind_gust, IReadOnlyList<OpenWeatherWeather> weather);
    public record OpenWeather(double lat, double lon, string timezone, double timezone_offset, OpenWeatherCurrent current);
}
