namespace Kanawanagasaki.TwitchHub.Services;

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.EntityFrameworkCore;

public class TtsService : BackgroundService
{
    private IConfiguration _conf;
    private IServiceScopeFactory _serviceFactory;
    private ILogger<TtsService> _logger;
    private SpeechConfig _speechConf;
    private SpeechSynthesizer _synthesizer;
    private List<(string Username, string Text)> _textToRead = new();

    public TtsService(IConfiguration conf, IServiceScopeFactory serviceFactory, ILogger<TtsService> logger)
        => (_conf, _serviceFactory, _logger) = (conf, serviceFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_conf["Azure:Speech:Key1"])) return;
        if (string.IsNullOrWhiteSpace(_conf["Azure:Speech:Region"])) return;

        _speechConf = SpeechConfig.FromSubscription(_conf["Azure:Speech:Key1"], _conf["Azure:Speech:Region"]);
        _synthesizer = new SpeechSynthesizer(_speechConf);

        while (!token.IsCancellationRequested)
        {
            if (_textToRead.Count > 0)
            {
                var item = _textToRead[0];
                using (var sp = _serviceFactory.CreateScope())
                {
                    var db = sp.ServiceProvider.GetService<SQLiteContext>();
                    var voice = await db.ViewerVoices.FirstOrDefaultAsync(v => v.Username == item.Username);

                    try
                    {
                        if(voice is null)
                        {
                            await _synthesizer.SpeakTextAsync(item.Text);
                        }
                        else
                        {
                            string ssml = $@"
                                <speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""en-US"">
                                    <voice name=""{voice.VoiceName}"">
                                        <prosody volume=""100"" pitch=""{(voice.Pitch < 0 ? "" : "+")}{voice.Pitch}Hz"" rate=""{voice.Rate.ToString().Replace(",", ".")}"">
                                            {item.Text}
                                        </prosody>
                                    </voice >
                                </speak>";
                            await _synthesizer.SpeakSsmlAsync(ssml);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                    }

                    lock (_textToRead)
                    {
                        _textToRead.RemoveAt(0);
                    }
                }
            }
            else await Task.Delay(1000);
        }
    }

    public void AddTextToRead(string username, string text)
    {
        lock (_textToRead)
            _textToRead.Add((username, text));
    }

    public async Task<VoiceInfo[]> GetVoices()
    {
        if(_conf is null) return Array.Empty<VoiceInfo>();
        if(_synthesizer is null) return Array.Empty<VoiceInfo>();

        return (await _synthesizer.GetVoicesAsync()).Voices.ToArray();
    }
}
