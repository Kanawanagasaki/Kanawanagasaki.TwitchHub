namespace Kanawanagasaki.TwitchHub.Services;

using System.Globalization;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Microsoft.ClearScript.V8;

public class JavaScriptService
{
    private Dictionary<string, JsEngine> _engines = new();
    public IReadOnlyDictionary<string, JsEngine> Engines => _engines;

    private IServiceScopeFactory _serviceFactory;
    private TwitchChatService _twChat;

    public event Action<string> OnEngineStateChange;

    public JavaScriptService(IServiceScopeFactory serviceFactory)
        => _serviceFactory = serviceFactory;

    public bool HasEngineForChannel(string channel)
        => _engines.ContainsKey(channel);

    public void CreateEngine(string channel)
    {
        if (_twChat is null)
        {
            using (var scope = _serviceFactory.CreateScope())
            {
                _twChat = scope.ServiceProvider.GetService<TwitchChatService>();
            }
        }

        lock (_engines)
        {
            if (!_engines.ContainsKey(channel))
            {
                var engine = new JsEngine(channel);
                _engines[channel] = engine;
                OnEngineStateChange?.Invoke(channel);
            }
        }
    }

    public async Task<string> Execute(string channel, string code)
    {
        if (!_engines.ContainsKey(channel))
            return null;
            
        return await _engines[channel].Execute(code);
    }

    public string FlushLogs(string channel)
    {
        if (!_engines.ContainsKey(channel)) return null;

        var logs = _engines[channel].FlushLogs();
        _twChat.Client.SendMessage(channel, logs);
        return logs;
    }

    public void DisposeEngine(string channel)
    {
        lock (_engines)
        {
            if (_engines.ContainsKey(channel))
            {
                _engines[channel].Dispose();
                _engines.Remove(channel);

                OnEngineStateChange?.Invoke(channel);
            }
        }
    }
}
