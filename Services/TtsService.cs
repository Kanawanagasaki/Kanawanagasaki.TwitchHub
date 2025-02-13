namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Kanawanagasaki.TwitchHub.Data;
using System.Text;
using NAudio.Wave;
using System.Collections.Concurrent;
using TwitchLib.PubSub.Events;

public class TtsService(IConfiguration _conf, ILogger<TtsService> _logger, IServiceScopeFactory _serviceScopeFactory) : BackgroundService
{
    private List<(string Id, string Username, string Text)> _textToRead = new();
    private AzureTtsVoiceInfo[]? _cachedVoices = null;

    private (string Id, string Username, string Text)? _currentItem = null;

    private bool _isEnabled = true;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_conf["Azure:Speech:Key1"]))
        {
            _logger.LogWarning("Azure:Speech:Key1 was empty");
            return;
        }

        var voices = await GetVoices();
        if (voices.Length == 0)
        {
            _logger.LogWarning("Azure voices array was empty");
            return;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _conf["Azure:Speech:Key1"]);
        http.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
        http.DefaultRequestHeaders.Add("User-Agent", _conf["Azure:Speech:AppName"]);

        while (!ct.IsCancellationRequested)
        {
            if (0 < _textToRead.Count && _isEnabled)
            {
                try
                {
                    lock (_textToRead)
                    {
                        _currentItem = _textToRead[0];
                        _textToRead.RemoveAt(0);
                    }

                    using var scope = _serviceScopeFactory.CreateScope();
                    using var db = scope.ServiceProvider.GetRequiredService<SQLiteContext>();
                    var voice = await db.ViewerVoices.FirstOrDefaultAsync(v => v.Username == _currentItem.Value.Username);
                    if (voice is null)
                    {
                        var voiceInfo = voices.Where(v => v.Locale == "en-US").OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                        if (voiceInfo is null)
                            voiceInfo = voices.First();

                        voice = new()
                        {
                            Uuid = Guid.NewGuid(),
                            Username = _currentItem.Value.Username,
                            VoiceName = voiceInfo.ShortName!,
                            Pitch = 0,
                            Rate = 1
                        };
                        db.ViewerVoices.Add(voice);
                        await db.SaveChangesAsync();
                    }

                    var text = _currentItem.Value.Text.Replace("\"", "&quot;")
                                                      .Replace("&", "&amp;")
                                                      .Replace("'", "&apos;")
                                                      .Replace("<", "&lt;")
                                                      .Replace(">", "&gt;");

                    var tuples = text.Split(' ').Select(x => (word: x, pitch: voice.Pitch, rate: voice.Rate, style: string.Empty)).ToArray();
                    for (int i = 0; i < tuples.Length; i++)
                    {
                        var (word, pitch, rate, style) = tuples[i];

                        if (Uri.TryCreate(word, UriKind.Absolute, out var uri))
                            tuples[i] = ("<break time=\"200ms\" />" + uri.Host + " " + (uri.AbsolutePath.StartsWith("/") ? uri.AbsolutePath.Substring(1) : uri.AbsolutePath) + "<break time=\"200ms\" />", pitch, 2, string.Empty);
                        else if (voice.Pitch == 0 && voice.Rate == 1 && word.Any(char.IsLetter) && word.All(x => char.IsUpper(x) || !char.IsLetter(x)))
                            tuples[i] = (word, pitch, rate, "shouting");
                    }

                    var lastPitch = voice.Pitch;
                    var lastRate = voice.Rate;
                    var lastStyle = string.Empty;
                    var line = new List<string>();
                    var elements = new List<string>();

                    foreach (var (word, pitch, rate, style) in tuples)
                    {
                        if (pitch != lastPitch || rate != lastRate || style != lastStyle)
                        {
                            if (0 < line.Count)
                            {
                                if (lastStyle != string.Empty)
                                    elements.Add($"""<mstts:express-as style="{lastStyle}">{string.Join(" ", line)}</mstts:express-as>""");
                                else
                                    elements.Add($"""<prosody volume="100" pitch="{(lastPitch < 0 ? "" : "+")}{lastPitch}Hz" rate="{lastRate.ToString().Replace(",", ".")}">{string.Join(" ", line)}</prosody>""");
                            }

                            lastPitch = pitch;
                            lastRate = rate;
                            lastStyle = style;
                            line.Clear();
                        }

                        line.Add(word);
                    }

                    if (0 < line.Count)
                    {
                        if (lastStyle != string.Empty)
                            elements.Add($"""<mstts:express-as style="{lastStyle}">{string.Join(" ", line)}</mstts:express-as>""");
                        else
                            elements.Add($"""<prosody volume="100" pitch="{(lastPitch < 0 ? "" : "+")}{lastPitch}Hz" rate="{lastRate.ToString().Replace(",", ".")}">{string.Join(" ", line)}</prosody>""");
                    }

                    string ssml = $"""
                        <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US" xmlns:mstts="https://www.w3.org/2001/mstts">
                            <voice name="{voice.VoiceName}">
                                {string.Join("\n", elements)}
                            </voice>
                        </speak>
                        """;

                    var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
                    var response = await http.PostAsync(_conf["Azure:Speech:Endpoints:Tts"], content);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        using var audioStream = await response.Content.ReadAsStreamAsync();
                        using var audioProvider = new RawSourceWaveStream(audioStream, new WaveFormat(24000, 16, 1));
                        using var waveOut = new WaveOutEvent();
                        waveOut.Init(audioProvider);
                        waveOut.Play();
                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            if (_currentItem is null)
                                waveOut.Stop();
                            await Task.Delay(200, ct);
                        }
                    }
                    else
                    {
                        var responseStr = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning($"Failed to fetch audio file for tts: {(int)response.StatusCode} {response.StatusCode}\n{responseStr}");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e.Message);
                }
                finally
                {
                    _currentItem = null;
                }
            }
            else await Task.Delay(1000, ct);
        }
    }

    public void AddTextToRead(string messageId, string username, string text)
    {
        if (!_isEnabled)
            return;

        if (text.StartsWith('!'))
            return;

        lock (_textToRead)
            _textToRead.Add((messageId, username, text.Replace("<", "").Replace(">", "")));
    }

    public void DeleteById(string messageId)
    {
        if (_currentItem is not null && _currentItem.Value.Id == messageId)
            _currentItem = null;

        lock (_textToRead)
        {
            var index = _textToRead.FindIndex(x => x.Id == messageId);
            if (0 <= index)
                _textToRead.RemoveAt(index);
        }
    }

    public void DeleteByUsername(string username)
    {
        if (_currentItem is not null && _currentItem.Value.Username == username)
            _currentItem = null;

        lock (_textToRead)
        {
            int index = -1;
            do
            {
                index = _textToRead.FindIndex(x => x.Username == username);
                if (0 <= index)
                    _textToRead.RemoveAt(index);
            }
            while (0 <= index);
        }
    }

    public async Task<AzureTtsVoiceInfo[]> GetVoices()
    {
        if (_cachedVoices is not null)
            return _cachedVoices;

        if (string.IsNullOrWhiteSpace(_conf["Azure:Speech:Key1"]))
            return [];

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _conf["Azure:Speech:Key1"]);
        var response = await http.GetAsync(_conf["Azure:Speech:Endpoints:VoicesList"]);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _cachedVoices = JsonConvert.DeserializeObject<AzureTtsVoiceInfo[]>(json) ?? [];
            return _cachedVoices;
        }
        else return [];
    }

    public void Enable()
    {
        _isEnabled = true;
        _logger.LogInformation("Tts enabled");
    }

    public void Disable()
    {
        _isEnabled = false;
        _textToRead.Clear();
        _logger.LogInformation("Tts disabled");
    }
}
