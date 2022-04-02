using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

public class ScreenApiJs
{
    public const string DEFAULT_INIT_CODE = @"(obj) => {
        obj.bg = '#1e1e1e';
        for (let i = 0; i < obj.symbols.length; i++) {
            let l = obj.symbols[i];
            l.size = 72;
            l.x = (-(obj.symbols.length * 36) / 2 + 18) + i * 36;
        }
    }";
    public const string DEFAULT_TICK_CODE = @"(obj, tick) =>
    {
        obj.bg = `linear-gradient(${tick % 360}deg,#8a4c69,#c45123)`;
        for (let i = 0; i < obj.symbols.length; i++) {
            let t = (tick - i * 10) % 260;
            let l = obj.symbols[i];
            l.y = t < 20 ? -40 + t * 2 : t < 140 ? 0 : -Math.sin((t - 140) / 120 * Math.PI / 2) * 40;
        }
    }";

    public string InitCode { get; set; } = DEFAULT_INIT_CODE;
    public string TickCode { get; set; } = DEFAULT_TICK_CODE;

    private JsEngine _engine;
    private string _directory = "custom-sctiprs";
    private string _saveFile => $"{_directory}/{_engine.Channel}.json";

    public ScreenApiJs(JsEngine engine)
    {
        _engine = engine;

        try
        {
            if(File.Exists(_saveFile))
            {
                var json = File.ReadAllText(_saveFile);
                var obj = JsonConvert.DeserializeObject<JObject>(json);
                InitCode = obj.Value<string>("initCode");
                TickCode = obj.Value<string>("tickCode");
            }
        }
        catch
        {
            ResetToDefault();
        }
    }

    public void ResetToDefault()
    {
        this.InitCode = DEFAULT_INIT_CODE;
        this.TickCode = DEFAULT_TICK_CODE;
        Save();
    }

    public void onInit(object callback)
    {
        if(callback.GetType().Name != "V8ScriptItem") return;
        InitCode = ParseCode(nameof(onInit));
        Save();
    }

    public void onTick(object callback)
    {
        if(callback.GetType().Name != "V8ScriptItem") return;
        TickCode = ParseCode(nameof(onTick));
        Save();
    }

    private string ParseCode(string methodName)
    {
        var screenApiIndex = _engine.LastCodeExecuted.IndexOf("screenApi");
        if(screenApiIndex < 0) return null;

        var methodIndex = _engine.LastCodeExecuted.IndexOf(methodName, screenApiIndex + 9);
        if(methodIndex < 0) return null;
        
        var dot = _engine.LastCodeExecuted.Substring(screenApiIndex + 9, methodIndex - screenApiIndex - 9).Trim();
        if(dot != ".") return null;

        var parenthesisIndex = _engine.LastCodeExecuted.IndexOf("(", methodIndex + methodName.Length);
        if(parenthesisIndex < 0) return null;

        var nothingness = _engine.LastCodeExecuted.Substring(methodIndex + methodName.Length, parenthesisIndex - methodIndex - methodName.Length).Trim();
        if(nothingness != "") return null;

        int parenthesisCounter = 0;
        int lastParenthesisIndex = -1;
        for(int i = parenthesisIndex; i < _engine.LastCodeExecuted.Length; i++)
        {
            if(_engine.LastCodeExecuted[i] == '(')
                parenthesisCounter++;
            else if(_engine.LastCodeExecuted[i] == ')')
                parenthesisCounter--;

            if(parenthesisCounter <= 0)
            {
                lastParenthesisIndex = i;
                break;
            }
        }

        if(lastParenthesisIndex < 0) return null;

        return _engine.LastCodeExecuted.Substring(parenthesisIndex + 1, lastParenthesisIndex - parenthesisIndex - 1);
    }

    private void Save()
    {
        if(!Directory.Exists(_directory))
            Directory.CreateDirectory(_directory);

        var obj = new
        {
            initCode = InitCode,
            tickCode = TickCode
        };
        var json = JsonConvert.SerializeObject(obj);
        File.WriteAllText(_saveFile, json);
    }
}
