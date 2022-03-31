namespace Kanawanagasaki.TwitchHub.Services;

using System.Globalization;
using Microsoft.ClearScript.V8;

public class JavaScriptService
{
    private Dictionary<string, V8ScriptEngine> _engines = new();
    private Dictionary<string, List<string>> _logs = new();
    private IServiceScopeFactory _serviceFactory;
    private TwitchChatService _twChat;

    public JavaScriptService(IServiceScopeFactory serviceFactory)
        => _serviceFactory = serviceFactory;

    public void CreateEngine(string channel)
    {
        if(_twChat is null)
        {
            using(var scope = _serviceFactory.CreateScope())
            {
                _twChat = scope.ServiceProvider.GetService<TwitchChatService>();
            }
        }

        lock(_logs)
        {
            if(!_logs.ContainsKey(channel))
            {
                _logs[channel] = new();
            }
        }

        lock(_engines)
        {
            if(!_engines.ContainsKey(channel))
            {
                _engines[channel] = new V8ScriptEngine(V8ScriptEngineFlags.EnableDateTimeConversion
                    | V8ScriptEngineFlags.EnableTaskPromiseConversion
                    | V8ScriptEngineFlags.EnableValueTaskPromiseConversion);

                _engines[channel].AddHostObject("console", new { log = new Action<object>((str) =>
                {
                    if(_logs[channel].Count < 100)
                         _logs[channel].Add(str.ToString());
                })});
            }
        }
    }

    public async Task<string> Execute(string channel, string code)
    {
        if(!_engines.ContainsKey(channel))
            return null;

        CancellationTokenSource source = new();

        bool finished = false;
        bool interrupted = false;
        var taskRun = Task.Run(()=>
        {
            var result = _engines[channel].ExecuteCommand(code);
            finished = true;

            if(!interrupted)
                source.Cancel();

            return result;
        });
        
        try
        {
            await Task.Delay(250, source.Token);
        }
        catch{}

        if(!finished)
        {
            interrupted = true;
            _engines[channel].Interrupt();
        }
        
        return await taskRun;
    }
    
    public string FlushLogs(string channel)
    {
        if(!_logs.ContainsKey(channel)) return null;
        if(_logs[channel].Count == 0) return null;

        string logs = string.Join(" ", _logs[channel]);
        if(logs.Length > 500)
            logs = logs.Substring(0, 500);

        _twChat.Client.SendMessage(channel, logs);
        _logs[channel].Clear();

        return logs;
    }

    public void DisposeEngine(string channel)
    {
        lock(_logs)
        {
            if(_logs.ContainsKey(channel))
            {
                _logs[channel].Clear();
                _logs.Remove(channel);
            }
        }

        lock(_engines)
        {
            if(_engines.ContainsKey(channel))
            {
                _engines[channel].Dispose();
                _engines.Remove(channel);
            }
        }
    }
}