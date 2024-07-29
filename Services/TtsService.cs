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

public class TtsService : BackgroundService
{
    private IConfiguration _conf;
    private IServiceScopeFactory _serviceFactory;
    private ILogger<TtsService> _logger;
    private List<(string Id, string Username, string Text)> _textToRead = new();
    private AzureTtsVoiceInfo[] _cachedVoices = null;
    private AzureTtsVoiceInfo _defaultVoice = null;

    private (string Id, string Username, string Text)? _currentItem = null;

    public TtsService(IConfiguration conf, IServiceScopeFactory serviceFactory, ILogger<TtsService> logger)
        => (_conf, _serviceFactory, _logger) = (conf, serviceFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken cancellation)
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

        _defaultVoice = voices.FirstOrDefault(v => v.Locale == "en-US");
        if (_defaultVoice is null)
            _defaultVoice = voices.First();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _conf["Azure:Speech:Key1"]);
        http.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
        http.DefaultRequestHeaders.Add("User-Agent", _conf["Azure:Speech:AppName"]);

        while (!cancellation.IsCancellationRequested)
        {
            if (0 < _textToRead.Count)
            {
                try
                {
                    lock (_textToRead)
                    {
                        _currentItem = _textToRead[0];
                        _textToRead.RemoveAt(0);
                    }

                    using var sp = _serviceFactory.CreateScope();
                    var db = sp.ServiceProvider.GetService<SQLiteContext>();
                    var voice = await db.ViewerVoices.FirstOrDefaultAsync(v => v.Username == _currentItem.Value.Username);

                    string ssml = "";

                    if (voice is not null)
                    {
                        ssml = $"""
                        <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US">
                            <voice name="{voice.VoiceName}">
                                <prosody volume="100" pitch="{(voice.Pitch < 0 ? "" : "+")}{voice.Pitch}Hz" rate="{voice.Rate.ToString().Replace(",", ".")}">
                                    {_currentItem.Value.Text}
                                </prosody>
                            </voice>
                        </speak>
                        """;
                    }
                    else
                    {
                        ssml = $"""
                        <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US">
                            <voice name="{_defaultVoice.ShortName}">{_currentItem.Value.Text}</voice>
                        </speak>
                        """;
                    }

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
                            await Task.Delay(200, cancellation);
                        }
                    }
                    else _logger.LogWarning($"Failed to fetch audio file for tts: {(int)response.StatusCode} {response.StatusCode}");
                }
                finally
                {
                    _currentItem = null;
                }
            }
            else await Task.Delay(1000);
        }
    }

    public void AddTextToRead(string messageId, string username, string text)
    {
        if (text.StartsWith("!"))
            return;

        lock (_textToRead)
            _textToRead.Add((messageId, username, text));
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
            return Array.Empty<AzureTtsVoiceInfo>();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _conf["Azure:Speech:Key1"]);
        var response = await http.GetAsync(_conf["Azure:Speech:Endpoints:VoicesList"]);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            _cachedVoices = JsonConvert.DeserializeObject<AzureTtsVoiceInfo[]>(json);
            return _cachedVoices;
        }
        else return Array.Empty<AzureTtsVoiceInfo>();
    }
}
