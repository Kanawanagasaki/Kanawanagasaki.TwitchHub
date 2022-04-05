using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

public class AfkSceneApi
{
    private const string DEFAULT_INIT_CODE = @"(obj) => {
        obj.bg = 'linear-gradient(0deg,#8a4c69,#c45123)';
        for (let i = 0; i < obj.symbols.length; i++) {
            let l = obj.symbols[i];
            l.size = 72;
            l.x = (-(obj.symbols.length * 36) / 2 + 18) + i * 36;
            l.y = -250;
        }
    }";
    private const string DEFAULT_TICK_CODE = @"(obj, tick) =>
    {
        obj.bg = `linear-gradient(${tick % 360}deg,#8a4c69,#c45123)`;
    }";
    private const string DEFAULT_SYMBOL_TICK_CODE = @"(symbol, index, count, tick) =>
    {
        let tt = [140, 50, .7, .95];
        let t = ((tick + (1 - index / count) * tt[1]) % tt[0]) / tt[0];
        if(t < tt[2])
            symbol.y = 0;
        else if(t < tt[3])
            symbol.y = Math.sin((t - tt[2]) * (1 / (tt[3] - tt[2])) * Math.PI / 2) * -40;
        else
            symbol.y = -40 * (1 - (t - tt[3]) * (1 / (1 - tt[3])));
        symbol.y -= 250;
    }";

    internal event Action OnCodeChange;

    public string initCode { get; private set; } = DEFAULT_INIT_CODE;
    public string tickCode { get; private set; } = DEFAULT_TICK_CODE;
    public string symbolTickCode { get; private set; } = DEFAULT_SYMBOL_TICK_CODE;

    private JsEngine _engine;
    private string _directory = "custom-sctiprs";
    private string _saveFile => $"{_directory}/{_engine.Channel}.json";

    public AfkSceneApi(JsEngine engine)
    {
        _engine = engine;

        try
        {
            if(File.Exists(_saveFile))
            {
                var json = File.ReadAllText(_saveFile);
                var obj = JsonConvert.DeserializeObject<JObject>(json);
                initCode = obj.Value<string>("initCode");
                tickCode = obj.Value<string>("tickCode");
                symbolTickCode = obj.Value<string>("symbolTickCode");
            }
        }
        catch
        {
            resetToDefault();
        }
    }

    public void resetToDefault()
    {
        this.initCode = DEFAULT_INIT_CODE;
        this.tickCode = DEFAULT_TICK_CODE;
        this.symbolTickCode = DEFAULT_SYMBOL_TICK_CODE;
        Save();
        OnCodeChange?.Invoke();
    }

    public void onInit(object callback)
    {
        if(callback.GetType().Name != "V8ScriptItem") return;
        initCode = ParseCode(nameof(onInit));
        if(initCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnInit(string code)
    {
        initCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    public void onTick(object callback)
    {
        if(callback.GetType().Name != "V8ScriptItem") return;
        tickCode = ParseCode(nameof(onTick));
        if(tickCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnTick(string code)
    {
        tickCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    public void onSymbolTick(object callback)
    {
        if(callback.GetType().Name != "V8ScriptItem") return;
        symbolTickCode = ParseCode(nameof(onSymbolTick));
        if(symbolTickCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnSymbolTick(string code)
    {
        symbolTickCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    private string ParseCode(string methodName)
    {
        var methodIndex = _engine.LastCodeExecuted.LastIndexOf(methodName);
        if(methodIndex < 0) return null;

        var afkIndex = _engine.LastCodeExecuted.LastIndexOf("afk", methodIndex);
        if(afkIndex < 0) return null;

        var streamIndex = _engine.LastCodeExecuted.LastIndexOf("stream", methodIndex);
        if(streamIndex < 0) return null;
        
        var dot = _engine.LastCodeExecuted.Substring(afkIndex + 3, methodIndex - afkIndex - 3).Trim();
        if(dot != ".") return null;
        
        dot = _engine.LastCodeExecuted.Substring(streamIndex + 6, afkIndex - streamIndex - 6).Trim();
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
            initCode = initCode,
            tickCode = tickCode,
            symbolTickCode = symbolTickCode
        };
        var json = JsonConvert.SerializeObject(obj);
        File.WriteAllText(_saveFile, json);
    }

    public override string ToString()
        => "{ "
            + "initCode: string, "
            + "tickCode: string, "
            + "symbolTickCode: string, "
            + "resetToDefault: function(), "
            + "onInit: function(callback: (obj: AfkSceneData) => void), "
            + "onTick: function(callback: (obj: AfkSceneData, tick: number) => void), "
            + "onSymbolTick: function(callback: (symbol: SymbolData, index: number, length: number, tick: number) => void)"
            + " }";
}