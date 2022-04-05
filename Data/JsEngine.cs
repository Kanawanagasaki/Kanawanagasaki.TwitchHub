namespace Kanawanagasaki.TwitchHub.Data;

using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;

public class JsEngine : IDisposable
{
    public string Channel { get; set; }
    public string LastCodeExecuted { get; private set; }

    public StreamApi StreamApi { get; private set; }

    private V8ScriptEngine _engine;
    private List<string> _logs = new();

    private Dictionary<string, object> _registeredHostObjects = new();

    public JsEngine(string channel)
    {
        Channel = channel;

        _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDateTimeConversion
                    | V8ScriptEngineFlags.EnableTaskPromiseConversion
                    | V8ScriptEngineFlags.EnableValueTaskPromiseConversion);

        _engine.AddHostObject("console", new
        {
            log = new Action<object>((obj) =>
            {
                if (_logs.Count > 100) return;
                
                _logs.Add(obj.ToString());
            })
        });

        StreamApi = new StreamApi(this);
        _engine.AddHostObject("stream", StreamApi);
    }

    public T RegisterHostObjects<T>(string name, T obj) where T : class
    {
        if(_registeredHostObjects.ContainsKey(name))
            return _registeredHostObjects[name] as T;

        lock(_registeredHostObjects)
        {
            _engine.AddHostObject(name, obj);
            _registeredHostObjects.Add(name, obj);
        }
        return obj;
    }

    public async Task<string> Execute(string code)
    {
        bool finished = false;

        var mainTask = Task.Run(()=>
        {
            LastCodeExecuted = code;
            var result = _engine.ExecuteCommand(code);
            finished = true;
            return result;
        });

        var delayTask = Task.Delay(250);

        await Task.WhenAny(mainTask, delayTask);

        if(!finished)
            _engine.Interrupt();

        return await mainTask;
    }

    public string FlushLogs()
    {
        if (_logs.Count == 0) return null;

        string logs = string.Join(" ", _logs);
        if (logs.Length > 500)
            logs = logs.Substring(0, 500);

        _logs.Clear();

        return logs;
    }

    public void Dispose()
    {
        _engine.Dispose();
        _engine = null;
        _logs.Clear();
        _logs = null;
    }
}