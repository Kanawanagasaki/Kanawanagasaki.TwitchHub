namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

using Microsoft.ClearScript;

public class AfkSceneApi : IDisposable
{
    // private const string DEFAULT_INIT_CODE = @"(s) => {
    //     s.bg = '#e73232';
    //     for (let i = 0; i < s.symbols.length; i++) {
    //         let l = s.symbols[i];
    //         l.size = 72;
    //         l.x = (-(s.symbols.length * 36) / 2 + 18) + i * 36;
    //         l.y = -250;
    //         l.shadow = '1px 1px 2px black';
    //     }
    // }";
    // private const string DEFAULT_TICK_CODE = @"(s, cs) => {
    //     let x = (cs/2)%200, y = (cs/4) % 200;
    //     let o=(z,w)=>`conic-gradient(#0000 25%, #00000080 25%, #0000 30%, #0000 70%, #00000080 75%, #0000 75%) ${z}px ${w}px / 200px 200px`;
    //     let p=(z,w,a)=>`conic-gradient(from ${a}deg at 50% 50%, #3f3f3f 0%, 25%, #0000 25%) ${z}px ${w}px / 200px 200px repeat repeat`;
    //     s.bg=`${o(x,y)},${o(200-x,y+100)},${p(x,y,0)},${p(200-x,y,180)},#333333`;
    // }";

    private const string DEFAULT_INIT_CODE = @"(s) => {
        s.bg = '#0000';
        for (let i = 0; i < s.symbols.length; i++) {
            let l = s.symbols[i];
            l.size = 72;
            l.x = (-(s.symbols.length * 36) / 2 + 18) + i * 36;
            l.y = -250;
            l.shadow = '1px 1px 2px black';
        }
    }";
    private const string DEFAULT_TICK_CODE = @"(s, cs) => {
        s.bg = '#0000';
    }";
    private const string DEFAULT_SYMBOL_TICK_CODE = @"(s, i, c, cs) =>
    {
        let tt = [140, 50, .7, .95];
        let t = ((cs + (1 - i / c) * tt[1]) % tt[0]) / tt[0];
        if(t < tt[2])
            s.y = 0;
        else if(t < tt[3])
            s.y = Math.sin((t - tt[2]) * (1 / (tt[3] - tt[2])) * Math.PI / 2) * -40;
        else
            s.y = -40 * (1 - (t - tt[3]) * (1 / (1 - tt[3])));
        s.y -= 250;
    }";

    internal event Action? OnCodeChange;

    public string? initCode { get; private set; } = DEFAULT_INIT_CODE;
    public string? tickCode { get; private set; } = DEFAULT_TICK_CODE;
    public string? symbolTickCode { get; private set; } = DEFAULT_SYMBOL_TICK_CODE;

    private JsEngine _engine;
    private SQLiteContext _db;

    private string _channel;

    public AfkSceneApi(SQLiteContext db, JsEngine engine, string channel)
    {
        _db = db;
        _engine = engine;
        _channel = channel;

        var model = _db.JsAfkCodes.FirstOrDefault(m => m.Channel.ToLower() == _channel.ToLower());
        if (model is null) resetToDefault();
        else
        {
            initCode = model.InitCode;
            tickCode = model.TickCode;
            symbolTickCode = model.SymbolTickCode;
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
        if (callback is not ScriptObject) return;
        initCode = ParseCode(nameof(onInit));
        if (initCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnInit(string? code)
    {
        initCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    public void onTick(object callback)
    {
        if (callback is not ScriptObject) return;
        tickCode = ParseCode(nameof(onTick));
        if (tickCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnTick(string? code)
    {
        tickCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    public void onSymbolTick(object callback)
    {
        if (callback is not ScriptObject) return;
        symbolTickCode = ParseCode(nameof(onSymbolTick));
        if (symbolTickCode is not null)
        {
            Save();
            OnCodeChange?.Invoke();
        }
    }

    internal void SetOnSymbolTick(string? code)
    {
        symbolTickCode = code;
        Save();
        OnCodeChange?.Invoke();
    }

    private string? ParseCode(string methodName)
    {
        if (_engine.LastCodeExecuted is null)
            return null;

        var methodIndex = _engine.LastCodeExecuted.LastIndexOf(methodName);
        if (methodIndex < 0) return null;

        var afkIndex = _engine.LastCodeExecuted.LastIndexOf("afk", methodIndex);
        if (afkIndex < 0) return null;

        var streamIndex = _engine.LastCodeExecuted.LastIndexOf("stream", methodIndex);
        if (streamIndex < 0) return null;

        var dot = _engine.LastCodeExecuted.Substring(afkIndex + 3, methodIndex - afkIndex - 3).Trim();
        if (dot != ".") return null;

        dot = _engine.LastCodeExecuted.Substring(streamIndex + 6, afkIndex - streamIndex - 6).Trim();
        if (dot != ".") return null;

        var parenthesisIndex = _engine.LastCodeExecuted.IndexOf("(", methodIndex + methodName.Length);
        if (parenthesisIndex < 0) return null;

        var nothingness = _engine.LastCodeExecuted.Substring(methodIndex + methodName.Length, parenthesisIndex - methodIndex - methodName.Length).Trim();
        if (nothingness != "") return null;

        int parenthesisCounter = 0;
        int lastParenthesisIndex = -1;
        for (int i = parenthesisIndex; i < _engine.LastCodeExecuted.Length; i++)
        {
            if (_engine.LastCodeExecuted[i] == '(')
                parenthesisCounter++;
            else if (_engine.LastCodeExecuted[i] == ')')
                parenthesisCounter--;

            if (parenthesisCounter <= 0)
            {
                lastParenthesisIndex = i;
                break;
            }
        }

        if (lastParenthesisIndex < 0) return null;

        return _engine.LastCodeExecuted.Substring(parenthesisIndex + 1, lastParenthesisIndex - parenthesisIndex - 1);
    }

    private void Save()
    {
        var model = _db.JsAfkCodes.FirstOrDefault(m => m.Channel.ToLower() == _channel.ToLower());
        if (model is null)
        {
            model = new()
            {
                Channel = _channel,
                InitCode = initCode ?? DEFAULT_INIT_CODE,
                TickCode = tickCode ?? DEFAULT_TICK_CODE,
                SymbolTickCode = symbolTickCode ?? DEFAULT_SYMBOL_TICK_CODE
            };
            _db.JsAfkCodes.Add(model);
        }
        else
        {
            model.InitCode = initCode ?? DEFAULT_INIT_CODE;
            model.TickCode = tickCode ?? DEFAULT_TICK_CODE;
            model.SymbolTickCode = symbolTickCode ?? DEFAULT_SYMBOL_TICK_CODE;
        }
        _db.SaveChanges();
    }

    public string toString()
        => ToString();
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

    public void Dispose()
    {
        if (_db is not null)
            _db.Dispose();
    }
}
