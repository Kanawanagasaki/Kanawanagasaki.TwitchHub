namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Data.JsHostObjects;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class AfkScreen : ComponentBase, IAsyncDisposable
{
    [Inject]
    public JavaScriptService JsService { get; set; }
    [Inject]
    public IJSRuntime Js { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string Content { get; set; } = "";

    [Parameter]
    [SupplyParameterFromQuery]
    public string Channel { get; set; }

    private AfkScreenJs _afkScreen;
    private int _tickCounter = 0;
    private IJSObjectReference _loopObj;

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;

        JsService.OnEngineStateChange += (channel) =>
        {
            if (channel == Channel)
            {
                InvokeAsync(async () =>
                {
                    await Init();
                    StateHasChanged();
                });
            }
        };

        await Init();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel)) return;
        await Init();
    }

    private async Task Init(bool flag = true)
    {
        if (!JsService.HasEngineForChannel(Channel)) return;

        _afkScreen = new();
        if(!string.IsNullOrWhiteSpace(Content))
            _afkScreen.SetContent(Content);

        var engine = JsService.Engines[Channel];

        try
        {
            await engine.Execute($"({engine.ScreenApi.InitCode})(_$afkScreen)", new() { { "_$afkScreen", _afkScreen } });
        }
        catch
        {
            engine.ScreenApi.ResetToDefault();
            if(flag)
                await Init(false);
            _tickCounter = 0;
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
        var engine = JsService.Engines[Channel];

        try
        {
            await engine.Execute($"({engine.ScreenApi.TickCode})(_$afkScreen, {_tickCounter})", new() { { "_$afkScreen", _afkScreen } });
            _tickCounter++;
        }
        catch
        {
            engine.ScreenApi.ResetToDefault();
            await Init();
            _tickCounter = 0;
        }
        StateHasChanged();
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
