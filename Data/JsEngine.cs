namespace Kanawanagasaki.TwitchHub.Data;

using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;

public class JsEngine : IDisposable
{
    public string LastCodeExecuted { get; private set; }

    public StreamApi StreamApi { get; private set; }

    private V8ScriptEngine _engine;
    private List<string> _logs = new();

    private Dictionary<string, object> _registeredHostObjects = new();

    public JsEngine(SQLiteContext db, string channel)
    {
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

        StreamApi = new StreamApi(db, this, channel);
        _engine.AddHostObject("stream", StreamApi);
    }

    public void RegisterHostObjects(string name, object obj)
    {
        lock(_registeredHostObjects)
        {
            if(_registeredHostObjects.ContainsKey(name))
                _engine.ExecuteCommand($"delete {name};");
            _engine.AddHostObject(name, obj);
            _registeredHostObjects[name] = obj;
        }
    }

    public async Task<string> Execute(string code, bool fromCommand)
    {
        bool finished = false;

        var mainTask = Task.Run(()=>
        {
            if(fromCommand)
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
