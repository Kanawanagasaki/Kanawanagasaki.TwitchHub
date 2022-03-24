namespace Kanawanagasaki.TwitchHub.Components;

using Kanawanagasaki.TwitchHub.Data;
using Microsoft.AspNetCore.Components;

public partial class HtmlPreviewComponent : ComponentBase
{
    [Parameter]
    public HtmlPreviewCustomContent Content { get; set; }
}