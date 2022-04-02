namespace Kanawanagasaki.TwitchHub.Data;

using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Microsoft.ClearScript.V8;

public class JsEngine : IDisposable
{
    public string Channel { get; set; }
    public string LastCodeExecuted { get; private set; }

    public ScreenApiJs ScreenApi { get; private set; }

    private V8ScriptEngine _engine;
    private List<string> _logs = new();


    public JsEngine(string channel)
    {
        Channel = channel;

        _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDateTimeConversion
                    | V8ScriptEngineFlags.EnableTaskPromiseConversion
                    | V8ScriptEngineFlags.EnableValueTaskPromiseConversion);

        _engine.AddHostObject("console", new
        {
            log = new Action<object>((str) =>
            {
                if (_logs.Count < 100)
                    _logs.Add(str.ToString());
            })
        });

        ScreenApi = new ScreenApiJs(this);
        _engine.AddHostObject("screenApi", ScreenApi);
    }

    public async Task<string> Execute(string code, Dictionary<string, object> hostObjects)
    {
        CancellationTokenSource source = new();

        bool finished = false;
        bool interrupted = false;
        var taskRun = Task.Run(() =>
        {
            foreach (var kv in hostObjects)
                _engine.AddHostObject(kv.Key, kv.Value);

            LastCodeExecuted = code;
            var result = _engine.ExecuteCommand(code);
            finished = true;

            if (!interrupted)
                source.Cancel();

            return result;
        });

        try
        {
            await Task.Delay(250, source.Token);
        }
        catch { }

        if (finished)
        {
            if (hostObjects.Count > 0)
                _engine.Execute(string.Join("\n", hostObjects.Select(kv => $"delete {kv.Key};")));
        }
        else
        {
            interrupted = true;
            _engine.Interrupt();
        }

        return await taskRun;
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
