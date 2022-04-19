namespace Kanawanagasaki.TwitchHub.Services;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;

public class TtsService : BackgroundService
{
    private IConfiguration _conf;
    private SpeechConfig _speechConf;
    private SpeechSynthesizer _synthesizer;
    private List<string> _textToRead = new();

    public TtsService(IConfiguration conf)
        => (_conf) = (conf);

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
                await _synthesizer.SpeakTextAsync(_textToRead[0]);
                lock (_textToRead)
                {
                    _textToRead.RemoveAt(0);
                }
            }
            else await Task.Delay(1000);
        }
    }

    public void AddTextToRead(string text)
    {
        lock (_textToRead)
        {
            _textToRead.Add(text);
        }
    }
}
