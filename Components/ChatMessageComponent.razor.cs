namespace Kanawanagasaki.TwitchHub.Components;

using System.Threading.Tasks;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwitchLib.Client.Models;

public partial class ChatMessageComponent : ComponentBase, IDisposable
{
    [Inject]
    public TwitchApiService TwApi { get; set; }
    [Inject]
    public EmotesService Emotes { get; set; }
    [Inject]
    public IJSRuntime Js { get; set; }

    [Parameter]
    public ProcessedChatMessage Message { get; set; }
    private ProcessedChatMessage _cachedMessage = null;

    [Parameter]
    public int Offset { get; set; }

    [CascadingParameter]
    public Kanawanagasaki.TwitchHub.Pages.Index Parent { get; set; }

    private TwitchGetUsersResponse _user = null;
    private ElementReference? _ref { get; set; }
    private bool _shouldScroll = false;
    private int _height = 0;

    private List<string> _parts = new();
    private List<string> _emoteUrls = new();

    private Dictionary<string, string> _badges = new();

    private string _color = "";
    private bool _isAnimAway = false;

    protected override async Task OnInitializedAsync()
    {
        if(Parent is not null)
            Parent.RegisterComponent(this);

        var channelBadges = await TwApi.GetChannelBadges();
        var globalBadges = await TwApi.GetGlobalBadges();
        foreach (var badge in channelBadges.data.Concat(globalBadges.data))
        {
            var version = badge.versions.OrderByDescending(v => int.TryParse(v.id, out var id) ? id : 0).First();
            _badges[badge.set_id] = version.image_url_1x;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Message is not null && Message == _cachedMessage) return;

        if (Message is not null)
        {
            _isAnimAway = false;

            #region Increasing brightness of user's colors
            if (!string.IsNullOrWhiteSpace(Message.Original.ColorHex))
            {
                var hsl = RgbToHsl(HexToRgb(Message.Original.ColorHex));
                hsl.l += (1 - hsl.l) / 4;
                _color = $"hsl({hsl.h}, {(int)(hsl.s * 100)}%, {(int)(hsl.l * 100)}%)";
            }
            #endregion

            #region Parsing message for twitch emotes
            _parts.Clear();
            _emoteUrls.Clear();

            int lastIndex = 0;
            foreach (var emote in Message.Original.EmoteSet.Emotes.OrderBy(e => e.StartIndex))
            {
                _parts.Add(Message.Original.Message.Substring(lastIndex, emote.StartIndex - lastIndex));
                _emoteUrls.Add($"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/1.0");
                lastIndex = emote.EndIndex + 1;
            }
            _parts.Add(Message.Original.Message.Substring(lastIndex));
            #endregion

            #region Getting bttv emotes
            var globalBttv = await Emotes.GetGlobalBttv();
            var channelBttv = await Emotes.GetChannelBttv(Message.Original.RoomId);
            List<BttvEmote> allBttv = new();
            if (globalBttv is not null)
                allBttv.AddRange(globalBttv);
            if (channelBttv is not null)
            {
                if (channelBttv.channelEmotes is not null)
                    allBttv.AddRange(channelBttv.channelEmotes);
                if (channelBttv.sharedEmotes is not null)
                    allBttv.AddRange(channelBttv.sharedEmotes);
            }
            #endregion

            #region Parsing message for bttv emotes
            for (int i = 0; i < _parts.Count; i++)
            {
                var split = _parts[i].Split(" ");
                for (int j = 0; j < split.Length; j++)
                {
                    var bttv = allBttv.FirstOrDefault(b => b.code == split[j]);
                    if (bttv is null) continue;

                    _parts[i] = string.Join(" ", split.Take(j)) + " ";
                    _parts.Insert(i + 1, string.Join(" ", split.Skip(j + 1)));

                    _emoteUrls.Insert(i, $"https://cdn.betterttv.net/emote/{bttv.id}/2x");

                    i--;
                    break;
                }
            }
            #endregion

            _user = await TwApi.GetUser(Message.Original.UserId);
        }

        _cachedMessage = Message;
        _shouldScroll = Message is not null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Message is null)
            return;

        if(_ref is not null)
        {
            _height = await Js.InvokeAsync<int>("getHeight", _ref);
            
            if (_shouldScroll)
            {
                await Js.InvokeVoidAsync("scrollIntoView", _ref);
                _shouldScroll = false;
            }
        }
    }

    public async Task AnimateAway()
    {
        _isAnimAway = true;
        StateHasChanged();
        await Task.Delay(300);
    }

    private double GetLuma(string hex)
    {
        (var r, var g, var b) = HexToRgb(hex);
        return Math.Sqrt(0.299 * r * r + 0.587 * g * g + 0.114 * b * b);
    }

    private (byte r, byte g, byte b) HexToRgb(string hex)
    {
        int rgb = int.Parse(hex.Substring(1), System.Globalization.NumberStyles.HexNumber);
        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return (r, g, b);
    }

    private (double h, double s, double l) RgbToHsl((byte br, byte bg, byte bb) colors)
        => RgbToHsl(colors.br, colors.bg, colors.bb);

    private (double h, double s, double l) RgbToHsl(byte br, byte bg, byte bb)
    {
        double r = br / 255d;
        double g = bg / 255d;
        double b = bb / 255d;

        double cmin = Math.Min(r, Math.Min(g, b));
        double cmax = Math.Max(r, Math.Max(g, b));
        double delta = cmax - cmin;
        double h = 0;
        double s = 0;
        double l = 0;

        if (delta == 0) h = 0;
        else if (cmax == r) h = ((g - b) / delta) % 6;
        else if (cmax == g) h = (b - r) / delta + 2;
        else h = (r - g) / delta + 4;

        h = Math.Round(h * 60);

        if (h < 0) h += 360;

        l = (cmax + cmin) / 2;

        s = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * l - 1));

        return (h, s, l);
    }

    public void Dispose()
    {
        if(Parent is not null)
            Parent.UnregisterComponent(this);
    }
}