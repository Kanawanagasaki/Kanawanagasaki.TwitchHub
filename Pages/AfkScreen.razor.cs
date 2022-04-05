namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.ClearScript;
using Microsoft.JSInterop;

public partial class AfkScreen : ComponentBase, IAsyncDisposable
{
    [Inject]
    public JavaScriptService JsService { get; set; }
    [Inject]
    public IJSRuntime Js { get; set; }
    [Inject]
    public TwitchChatService Chat { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Content { get; set; } = "";

    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }

    private AfkSceneData _afkScreen;
    private IJSObjectReference _loopObj;
    private DateTime _startDateTime;

    private JsEngine _engine;

    private int _framesToSkip = 0;
    private int _skippedFramesCounter = 0;

    protected override void OnInitialized()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;

        JsService.OnEngineStateChange += (channel) =>
        {
            if (!JsService.HasEngineForChannel(Channel)) return;
            if (channel == Channel)
            {
                InvokeAsync(async () =>
                {
                    _engine = JsService.Engines[Channel];
                    _engine.StreamApi.afk.OnCodeChange += ()=>
                    {
                        InvokeAsync(async() => await Init());
                    };
                    await Init();
                    StateHasChanged();
                });
            }
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;
        if (!JsService.HasEngineForChannel(Channel)) return;

        _engine = JsService.Engines[Channel];
        _engine.StreamApi.afk.OnCodeChange += ()=>
        {
            InvokeAsync(async() => await Init());
        };

        await Init();
    }

    private async Task Init(bool flag = true)
    {
        if (!JsService.HasEngineForChannel(Channel)) return;

        _startDateTime = DateTime.UtcNow;

        _afkScreen = new();
        if (!string.IsNullOrWhiteSpace(Content))
            _afkScreen.SetContent(Content);

        try
        {
            _afkScreen = _engine.RegisterHostObjects("_$afkScreen", _afkScreen);
            _engine.RegisterHostObjects("_$symbols", _afkScreen.symbols);

            await _engine.Execute($"({_engine.StreamApi.afk.initCode})(_$afkScreen)");
            _engine.FlushLogs();
        }
        catch(Exception e)
        {
            await ProcessException(e, flag);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _loopObj = await Js.InvokeAsync<IJSObjectReference>("createLoopObj", DotNetObjectReference.Create(this));
            await _loopObj.InvokeVoidAsync("start");
        }
    }

    [JSInvokable("onTick")]
    public async Task OnTick()
    {
        if (!JsService.HasEngineForChannel(Channel)) return;
        if(_skippedFramesCounter > 0)
        {
            _skippedFramesCounter--;
            return;
        }

        var tickCode = _engine.StreamApi.afk.tickCode;
        var symbolTickCode = _engine.StreamApi.afk.symbolTickCode;

        var diff = DateTime.UtcNow - _startDateTime;
        var tickCounter = (int)(diff.TotalMilliseconds / 10);

        var code = $@"
            ({tickCode})(_$afkScreen, {tickCounter});
            for(let i = 0; i < _$symbols.length; i++)
                ({symbolTickCode})(_$symbols[i], i, _$symbols.length, {tickCounter});
        ";

        try
        {
            await _engine.Execute(code);
            _framesToSkip = 0;

            var logs = _engine.FlushLogs();
            if(!string.IsNullOrWhiteSpace(logs))
            {
                if(tickCode == _engine.StreamApi.afk.tickCode)
                    _engine.StreamApi.afk.resetToDefault();
                else
                {
                    _engine.StreamApi.afk.SetOnTick(tickCode);
                    _engine.StreamApi.afk.SetOnSymbolTick(symbolTickCode);
                }
                Chat.Client.SendMessage(Channel, logs);
            }
        }
        catch(Exception e)
        {
            await ProcessException(e);
        }
        StateHasChanged();
    }

    private async Task ProcessException(Exception e, bool flag = true)
    {
        if(e.Message != "ReferenceError: _$afkScreen is not defined" && e.Message != "The V8 runtime cannot perform the requested operation because a script exception is pending")
        {
            if(e is ScriptEngineException)
                Chat.Client.SendMessage(Channel, e.Message);
            
            _engine.StreamApi.afk.resetToDefault();
            if(flag)
                await Init(false);
            _startDateTime = DateTime.UtcNow;
        }
        else
        {
            if(e is ScriptEngineException see)
            {
                Console.WriteLine(see.EngineName);
                Console.WriteLine(see.ErrorDetails);
                Console.WriteLine(see.ExecutionStarted);
                Console.WriteLine(see.IsFatal);
                Console.WriteLine(see.ScriptException);
                Console.WriteLine(see.StackTrace);
                Console.WriteLine(see.TargetSite);
            }
            _framesToSkip++;
            _skippedFramesCounter = _framesToSkip;
            Console.WriteLine($"Skipping {_framesToSkip} frames");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_loopObj is not null)
                await _loopObj.InvokeVoidAsync("stop");
        }
        catch { }
    }
}
