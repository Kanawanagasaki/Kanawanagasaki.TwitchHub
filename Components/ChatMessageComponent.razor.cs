namespace Kanawanagasaki.TwitchHub.Components;

using System.Threading.Tasks;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public partial class ChatMessageComponent : ComponentBase, IDisposable
{
    [Inject]
    public required TwitchApiService TwApi { get; set; }
    [Inject]
    public required EmotesService Emotes { get; set; }
    [Inject]
    public required IJSRuntime Js { get; set; }
    [Inject]
    public required ILogger<ChatMessageComponent> Logger { get; set; }

    [Parameter]
    public ProcessedChatMessage? Message { get; set; }
    private ProcessedChatMessage? _cachedMessage = null;

    [Parameter]
    public bool IsLast { get; set; }
    private bool _animateNew = false;

    [CascadingParameter]
    public ChatComponent? Parent { get; set; }

    private ElementReference? _ref { get; set; }
    private int _width = 0;
    private int _height = 0;

    private ElementReference? _codeRef { get; set; }
    private int _codeWidth = 0;
    private bool _isAnimCode = true;
    private bool _shouldRehighlight = false;
    private bool _codeFlag = false;

    private bool _isAnimAway = false;

    private System.Timers.Timer _timer = new();

    protected override void OnInitialized()
    {
        if (Parent is not null)
            Parent.RegisterComponent(this);

        _timer.Interval = 10_000;
        _timer.Elapsed += (obj, ev) =>
        {
            _isAnimCode = !_isAnimCode;
            InvokeAsync(StateHasChanged);
        };
        _timer.Start();
    }

    protected override async Task OnParametersSetAsync()
    {
        if(Message is null) return;
        if(Message != _cachedMessage)
        {
            _shouldRehighlight = Message.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Code);
            if(_shouldRehighlight)
                _codeFlag = !_codeFlag;

            if(IsLast)
            {
                _animateNew = true;
                await Task.Delay(300);
                _animateNew = false;
            }

            _cachedMessage = Message;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Message is null)
            return;

        if (_ref is not null)
        {
            _width = await Js.InvokeAsync<int>("getWidth", _ref);
            _height = await Js.InvokeAsync<int>("getHeight", _ref);

            if (_codeRef is not null)
            {
                _codeWidth = await Js.InvokeAsync<int>("getScrollWidth", _codeRef);
                if (_codeWidth < _width)
                    _codeWidth = 0;
                else _codeWidth -= _width - 32;

                if (Message.Fragments.HasFlag(ProcessedChatMessage.RenderFragments.Code) && _shouldRehighlight)
                {
                    await Js.InvokeVoidAsync("hljs.highlightElement", _codeRef);
                    _shouldRehighlight = false;
                }
            }
        }
    }

    public async Task AnimateAway()
    {
        _isAnimAway = true;
        StateHasChanged();
        await Task.Delay(300);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();

        if (Parent is not null)
            Parent.UnregisterComponent(this);
    }
}
