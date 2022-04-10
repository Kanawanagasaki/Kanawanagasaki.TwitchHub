namespace Kanawanagasaki.TwitchHub.Pages;

using Kanawanagasaki.TwitchHub.Components;
using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using TwitchLib.Client.Models;

public partial class Index : ComponentBase
{
    [Inject]
    public TwitchAuthService TwAuth { get; set; }
    [Inject]
    public SQLiteContext Db { get; set; }
}
