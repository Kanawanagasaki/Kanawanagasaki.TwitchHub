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

public class LlamaService(ILogger<LlamaService> _logger, IServiceScopeFactory _serviceScopeFactory, TtsService _tts, JsEnginesService _js, IConfiguration _conf)
{
    private ConcurrentDictionary<string, List<HistoryItem>> _messageHistory = new();
    private HttpClient _http = new();
    private JsonSerializerOptions _jsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private Dictionary<string, string> _aiEmotes = new()
    {
        [":rawr:"] = "catRawr",
        [":nya:"] = "pandaNyaa",
        [":cow:"] = "pandaCow",
        [":chonk:"] = "nyaChonk",
        [":attack:"] = "nyaAttack",
        [":baby:"] = "nyaBaby",
        [":bday:"] = "nyaBdaycake",
        [":banana:"] = "nyaBanana",
        [":blushing:"] = "nyaBlushing",
        [":dead:"] = "ded",
        [":bla:"] = "bla",
        [":sit:"] = "nyaSit",
        [":bow:"] = "bunBow",
        [":smack:"] = "nyasSmack",
        [":cute:"] = "nyaCute",
        [":bleh:"] = "nyaBleh",
        [":spin:"] = "CatSpin",
        [":nod:"] = "nyanod",
        [":mhm:"] = "nyaMhm",
        [":clover:"] = "nyaClover",
        [":please:"] = "nyaPlease",
        [":tears:"] = "nyaTears",
        [":drag:"] = "nyasDrag",
        [":wokeup:"] = "Wokege",
        [":clap:"] = "nyaClap",
        [":hungry:"] = "nyahungry",
        [":ignorework:"] = "nyaIgnorework",
        [":verysad:"] = "imfine",
        [":socute:"] = "catCute",
        [":tuck:"] = "TuckaCat",
        [":cozy:"] = "nyaCozy",
        [":sad:"] = "nyasad2",
        [":angryrawr:"] = "rawr",
        [":pizza:"] = "catPizza",
        [":wave:"] = "nyaWave",
        [":wave2:"] = "nyaaWave",
        [":sleep:"] = "CatSleep",
        [":sleeptogether:"] = "SleepTogether",
        [":poke:"] = "catPoke",
        [":jagaimo:"] = "kanawaJagaimo",
        [":kutsude:"] = "kanawaKutsude",
        [":potato:"] = "kanawaJagaimo",
        [":shoe:"] = "kanawaKutsude",
        [":sparkle:"] = "CatSparkle",
        [":lurk:"] = "CatLurk",
        [":confused:"] = "FubukiWhat"
    };
    private Dictionary<string, (string original, string description)> _emoteDescriptionCache = new();

    private char[] _nihongoKanaBotChars = ['カ', 'ナ', 'ワ', 'ナ', 'ガ', 'サ', 'キ', '�'];

    private string _appleAccessToken = string.Empty;
    private DateTimeOffset _appleAccessTokenExpire = DateTimeOffset.MinValue;

    public async Task OnTwitchChatMessage(TwitchChatMessagesService twitchChatMessages, ProcessedChatMessage message)
    {
        try
        {
            if (message.IsCommand)
                return;

            List<HistoryItem> history;
            if (!_messageHistory.TryGetValue(message.Original.Channel, out history))
            {
                history = new();
                _messageHistory.AddOrUpdate(message.Original.Channel, history, (_, _) => history);
            }

            var userMessage = string.Empty;
            var emotesUsed = new Dictionary<string, (string original, string url)>();
            foreach (var part in message.ParsedMessage)
            {
                var processedPart = ConvertNihongoNick(part.Content);
                if (part.IsEmote && part.EmoteUrl is not null)
                {
                    processedPart = $":{processedPart.ToLower()}:";
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

            while (30 < history.Count)
                history.RemoveAt(0);

            if (!userMessage.ToLower().Contains(message.Original.BotUsername.ToLower()) && !userMessage.ToLower().Contains("kanabot"))
                return;

            var responseMessage = string.Empty;
            var assistantRole = "assistant";

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
                    Kanawanagasaki don't have any more pets.
                    Your nickname is KanawanagasakiBot.
                    You respect viewers who ask questions in chat.
                    It is {{DateTime.UtcNow:U}} UTC now.
                    You was built using LLAMA 3.1 8B model which is used for text processing and LLAVA 1.6 7B model for image recognition via OLLAMA.
                    Remember, seekingtreasure (aka seek) is cute! Her pronounce is she/her.
                    One day seek asked you what is your nickname and you answered KanaBot.
                    Regynate (aka regy) sometimes make his appear in chat. He is smart lad who know how to hack games.
                    Reguas is type of fish. What fish? Nobody knows. But also Reguas is a type of grass. Why? Nobody knows.
                    There is also lace193 (aka lace) who talk a lot.
                    Hascowo (aka hasco) playing games fast, speedrunning them.
                    There are emotes in chat that viewers and you can use: {{string.Join(" ", _aiEmotes.Keys)}}
                    :nya: your favorite, but never use it unless asked.
                    Do not exceed 200 characters in your answers.
                    Respond in JSON format, for example: {"nickname":"kanawanagasakibot", "message":"hello, world!", "action":"sendmessage"}
                    There are 4 actions available: "sendmessage", "searchinternet", "weather", "timeout".
                    If your action is "searchinternet", make your message a query for search engine, short and on point!
                    If your action is "weather", make your message country and city only.
                    If your action is "timeout", make sure to include nickname in your message. You should never use timeout action.
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

            foreach (var historyItem in history)
            {
                var obj = new OllamaSchema(historyItem.Username, historyItem.Message, historyItem.Action);
                var json = JsonSerializer.Serialize(obj);
                apiMessages.Add(new(historyItem.Role, json));
            }

            var apiObj = new OllamaApiChat("llama3.1", apiMessages, "json", false, new(4096));

            for (int i = 0; i < 5; i++)
            {
                using var httpResponse = await _http.PostAsJsonAsync("http://192.168.0.52:11434/api/chat", apiObj, _jsonOptions);
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

                OllamaSchema llamaResponse;
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
                if (!responseMessage.StartsWith("/me ") && (responseMessage.StartsWith("/") || responseMessage.StartsWith(".")))
                    responseMessage = ":nya: " + responseMessage;
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
                    var internetQuery = await PrepareInternetSearch(userMessage + "\n" + responseMessage);
                    _logger.LogInformation("Searching internet: " + internetQuery + "\n(" + responseMessage + ")");
                    twitchChatMessages.SendMessage(message.Original.Channel, $"[Searching internet: {internetQuery}]");

                    var internetResponse = await SearchInternet(internetQuery);
                    var msg = $"Internet result for query {internetQuery}:\n{internetResponse}\n\nConvey this information to user!";

                    history.Add(new("user", "system", "system", msg, "sendmessage"));
                    var obj2 = new OllamaSchema("system", msg, "sendmessage");
                    var json2 = JsonSerializer.Serialize(obj2);
                    apiMessages.Add(new("user", json2));
                }
                else if (action == "weather")
                {
                    _logger.LogInformation("Getting weather: " + responseMessage);
                    twitchChatMessages.SendMessage(message.Original.Channel, $"[Getting weather: {responseMessage}]");

                    var placeDescription = await GetPlaceDescription(responseMessage);
                    if (placeDescription is null)
                    {
                        _logger.LogWarning("Failed to get place description");
                        twitchChatMessages.SendMessage(message.Original.Channel, $"[Failed to get longitude and latitude for weather]");
                        return;
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
                    OpenWeather weather;
                    try
                    {
                        weather = await GetWeather(placeDescriptionResult.center.lat, placeDescriptionResult.center.lng);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Exception thrown while fetching data from openweather: " + e.Message);
                        twitchChatMessages.SendMessage(message.Original.Channel, "[Failed to connect to OpenWeather]");
                        return;
                    }
                    if (weather is null)
                    {
                        _logger.LogWarning("Failed to get weather for " + string.Join(" ", placeDescriptionResult.formattedAddressLines));
                        twitchChatMessages.SendMessage(message.Original.Channel, $"[Failed to get weather for {string.Join(" ", placeDescriptionResult.formattedAddressLines)}]");
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

                            using var scope = _serviceScopeFactory.CreateScope();
                            var twitchAuth = scope.ServiceProvider.GetRequiredService<TwitchAuthService>();
                            var auth = await twitchAuth.GetRestored(message.Original.BotUsername);
                            var twitchApi = scope.ServiceProvider.GetRequiredService<TwitchApiService>();
                            var res = await twitchApi.Timeout(auth.AccessToken, message.Original.RoomId, auth.UserId, user.UserId, TimeSpan.FromMinutes(1), "Just because");

                            if (res)
                            {
                                twitchChatMessages.SendMessage(message.Original.Channel, $"[Timeout user: {user.Username}]");
                                _logger.LogInformation("Timeout user: " + user.Username);
                            }
                            else
                            {
                                twitchChatMessages.SendMessage(message.Original.Channel, $"[Timeout user: {user.Username} - Fail]");
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
                        twitchChatMessages.SendMessage(message.Original.Channel, $"[An attempt was made to timeout users, but it failed]");
                        var msg = $"Timeout attempt failed, user not found.";
                        history.Add(new("user", "system", "system", msg, "sendmessage"));
                        var obj3 = new OllamaSchema("system", msg, "sendmessage");
                        var json3 = JsonSerializer.Serialize(obj3);
                        apiMessages.Add(new("user", json3));

                        _logger.LogInformation("Failed to timeout any user");
                    }
                }
                else break;
            }

            var regex = new Regex(@"\:([^\s]+)\:");
            var matches = regex.Matches(responseMessage);
            for (int i = 0; i < matches.Count; i++)
                responseMessage = responseMessage.Substring(0, matches[i].Index)
                                + responseMessage.Substring(matches[i].Index, matches[i].Length).ToLower()
                                + responseMessage.Substring(matches[i].Index + matches[i].Length);

            foreach (var (k, v) in _aiEmotes)
                responseMessage = responseMessage.Replace(k, v + " ");
            foreach (var (k, v) in _emoteDescriptionCache)
                responseMessage = responseMessage.Replace(k, v.original + " ");
            responseMessage = Regex.Replace(responseMessage, @"\s+", " ");
            _logger.LogInformation(message.Original.BotUsername + ": " + responseMessage);

            _tts.AddTextToRead(message.Original.Id, message.Original.BotUsername, responseMessage);

            var chunks = SplitMessageIntoChunks(responseMessage, 450);
            foreach (var chunk in chunks)
                twitchChatMessages.SendMessage(message.Original.Channel, chunk);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            _logger.LogError(e.StackTrace);
            twitchChatMessages.SendMessage(message.Original.Channel, "[There was an error while executing llama request]");
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

    private async Task<string> DescribeImage(string url)
    {
        using var imageResponse = await _http.GetAsync(url);
        if (!imageResponse.IsSuccessStatusCode)
            return null;

        var bytes = await imageResponse.Content.ReadAsByteArrayAsync();

        if (imageResponse.Content.Headers.ContentType.MediaType == "image/webp")
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
        var llavaApiObj = new OllamaApiChat("llava", llavaMessages, null, false, null);
        using var llavaHttpResponse = await _http.PostAsJsonAsync("http://192.168.0.52:11434/api/chat", llavaApiObj, _jsonOptions);
        if (!llavaHttpResponse.IsSuccessStatusCode)
            return null;
        var llavaResponse = await llavaHttpResponse.Content.ReadFromJsonAsync<OllamaApiChatResponse>();
        if (llavaResponse is null)
            return null;
        return llavaResponse.message.content;
    }

    private async Task<string> PrepareInternetSearch(string prompt)
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
        var llamaApiObj = new OllamaApiChat("llama3.1", llamaMessages, null, false, new(4096));
        using var llamaHttpResponse = await _http.PostAsJsonAsync("http://192.168.0.52:11434/api/chat", llamaApiObj, _jsonOptions);
        if (!llamaHttpResponse.IsSuccessStatusCode)
            return null;
        var llamaResponse = await llamaHttpResponse.Content.ReadFromJsonAsync<OllamaApiChatResponse>();
        if (llamaResponse is null)
            return null;
        return llamaResponse.message.content;
    }
    private async Task<string> SearchInternet(string query)
    {
        var response = await _http.GetAsync("https://html.duckduckgo.com/html/?q=" + HttpUtility.UrlEncode(query));
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

            var snippetNode = node.QuerySelector(".result__snippet");
            var snippet = snippetNode is null ? "" : HttpUtility.HtmlDecode(snippetNode.InnerText).Trim();

            res += title + ":\n";
            res += snippet + "\n\n";
        }

        return res;
    }

    private async Task<OpenWeather> GetWeather(double lat, double lon)
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

    private async Task<AppleGeocode> GetPlaceDescription(string place)
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
            _appleAccessToken = bootstrap.authInfo.access_token;
            _appleAccessTokenExpire = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(bootstrap.authInfo.expires_in) - TimeSpan.FromMinutes(1);
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

    private async Task<string> GetGpsCoordinatesToken()
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
    }

    private record HistoryItem(string Role, string UserId, string Username, string Message, string Action);

    private record OllamaApiChat(string model, IEnumerable<OllamaMessage> messages, string format, bool stream, OllamaOptions options);
    private record OllamaMessage(string role, string content, string[] images = null);
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
