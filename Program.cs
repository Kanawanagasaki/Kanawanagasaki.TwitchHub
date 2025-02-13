using System.Globalization;
using Kanawanagasaki.TwitchHub.Services;
using Kanawanagasaki.TwitchHub;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Runtime.InteropServices;
using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Hubs;

Console.OutputEncoding = Encoding.UTF8;

var ci = new CultureInfo("ja-JP");
CultureInfo.DefaultThreadCurrentCulture = ci;
Thread.CurrentThread.CurrentCulture = ci;
Thread.CurrentThread.CurrentUICulture = ci;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    })
);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddCors(options =>
    {
        options.AddPolicy("musicyoutubecom",
            builder =>
            {
                builder.WithOrigins("https://music.youtube.com")
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials(); // Allow credentials
            });
    });

builder.Services.AddDbContext<SQLiteContext>(ServiceLifetime.Scoped);
builder.Services.AddSingleton<TwitchApiService>();
builder.Services.AddSingleton<JsEnginesService>();
builder.Services.AddSingleton<TtsService>();
builder.Services.AddSingleton<TwitchChatService>();
builder.Services.AddSingleton<TwitchEventSubService>();
builder.Services.AddSingleton<TwitchRewardsService>();
builder.Services.AddSingleton<LlamaService>();
builder.Services.AddSingleton<SevenTvApiService>();
builder.Services.AddSingleton<HelperService>();
builder.Services.AddSingleton<CommandsService>();
builder.Services.AddSingleton<EmotesService>();
builder.Services.AddSingleton<TwitchAuthService>();
builder.Services.AddSingleton<TwitchChatMessagesService>();

builder.Services.AddHostedService(sp => sp.GetRequiredService<TtsService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TwitchEventSubService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TwitchRewardsService>());

var app = builder.Build();

app.UseCors("musicyoutubecom");

using var rootScope = app.Services.CreateScope();
rootScope.ServiceProvider.GetRequiredService<SQLiteContext>().Database.Migrate();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    builder.WebHost.UseUrls("http://localhost:5678");
}

app.UseStaticFiles();

app.UseRouting();

app.MapHub<YoutubeHub>("/hubs/Youtube");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

var appTask = app.RunAsync();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) =>
{
    cts.Cancel();
    var handle = GetStdHandle(-10);
    CancelIoEx(handle, IntPtr.Zero);
};

bool isFirstIteration = true;
while (!cts.IsCancellationRequested)
{
    string? line = null;
    if (isFirstIteration)
    {
        line = string.Join(" ", args);
        isFirstIteration = false;
    }
    else
    {
        try
        {
            line = Console.ReadLine();
        }
        catch
        {
            line = null;
        }
    }

    try
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;

        var words = line.Split(' ');
        switch (words[0])
        {
            case "twitch":
                if (words.Length < 2)
                {
                    Console.WriteLine("Available commands: connect, disconnect, say, rewards");
                    break;
                }

                switch (words[1])
                {
                    case "connect":
                        {
                            if (words.Length < 4)
                            {
                                Console.WriteLine("twitch connect <bot> <channel>");
                                break;
                            }

                            var twitchAuth = rootScope.ServiceProvider.GetRequiredService<TwitchAuthService>();
                            var auth = await twitchAuth.GetRestored(words[2]);
                            if (auth is null || !auth.IsValid)
                            {
                                Console.WriteLine("Cannot authenticate " + words[2]);
                                break;
                            }

                            var twitchChat = rootScope.ServiceProvider.GetRequiredService<TwitchChatMessagesService>();
                            twitchChat.Connect(auth, words[3]);

                            break;
                        }
                    case "disconnect":
                        {
                            if (words.Length < 3)
                            {
                                Console.WriteLine("twitch disconnect <bot> [<channel>]");
                                break;
                            }

                            var twitchAuth = rootScope.ServiceProvider.GetRequiredService<TwitchAuthService>();
                            var auth = await twitchAuth.GetRestored(words[2]);
                            if (auth is null)
                            {
                                Console.WriteLine("Cannot authenticate " + words[2]);
                                break;
                            }

                            var twitchChat = rootScope.ServiceProvider.GetRequiredService<TwitchChatMessagesService>();
                            if (3 < words.Length)
                                twitchChat.Disconnect(auth, words[3]);
                            else
                                twitchChat.Disconnect(auth);

                            break;
                        }
                    case "say":
                        {
                            if (words.Length < 4)
                            {
                                Console.WriteLine("twitch say <bot> <channel>");
                                break;
                            }

                            var twitchChat = rootScope.ServiceProvider.GetRequiredService<TwitchChatMessagesService>();
                            twitchChat.SendMessage(words[2], words[3], string.Join(" ", words.Skip(4)));

                            break;
                        }
                    case "rewards":
                        {
                            if (words.Length < 3)
                            {
                                Console.WriteLine("Available commands: sync, enable, disable");
                                break;
                            }

                            switch (words[2])
                            {
                                case "sync":
                                    {
                                        if (words.Length < 4)
                                        {
                                            Console.WriteLine("twitch rewards sync <channel>");
                                            break;
                                        }
                                        var twitchAuth = rootScope.ServiceProvider.GetRequiredService<TwitchAuthService>();
                                        var auth = await twitchAuth.GetRestored(words[3]);
                                        if (auth is null || !auth.IsValid)
                                        {
                                            Console.WriteLine("Cannot authenticate " + words[4]);
                                            break;
                                        }

                                        var rewards = rootScope.ServiceProvider.GetRequiredService<TwitchRewardsService>();
                                        if (await rewards.Sync(auth))
                                            Console.WriteLine("Rewards have been successfully synchronized");
                                        else
                                            Console.WriteLine("Failed to synchronize rewards");
                                        break;
                                    }
                                case "enable":
                                    {
                                        if (words.Length == 5)
                                        {
                                            Console.WriteLine("Available types: " + string.Join(", ", Enum.GetValues<ERewardType>()));
                                            break;
                                        }
                                        if (words.Length < 6)
                                        {
                                            Console.WriteLine("twitch rewards enable <bot> <channel> <type>");
                                            break;
                                        }

                                        var twitchAuth = rootScope.ServiceProvider.GetRequiredService<TwitchAuthService>();

                                        var botAuth = await twitchAuth.GetRestored(words[3]);
                                        if (botAuth is null || !botAuth.IsValid)
                                        {
                                            Console.WriteLine("Cannot authenticate " + words[3]);
                                            break;
                                        }

                                        var auth = await twitchAuth.GetRestored(words[4]);
                                        if (auth is null || !auth.IsValid)
                                        {
                                            Console.WriteLine("Cannot authenticate " + words[4]);
                                            break;
                                        }

                                        if (!Enum.TryParse<ERewardType>(words[5], out var rewardType))
                                        {
                                            Console.WriteLine("Unknown type: " + words[5]);
                                            break;
                                        }

                                        var rewards = rootScope.ServiceProvider.GetRequiredService<TwitchRewardsService>();
                                        if (await rewards.Enable(auth, botAuth, rewardType))
                                            Console.WriteLine($"{rewardType} enabled");
                                        else
                                            Console.WriteLine($"Failed to enable {rewardType}");

                                        break;
                                    }
                                case "disable":
                                    {
                                        if (words.Length == 5)
                                        {
                                            Console.WriteLine("Available types: " + string.Join(", ", Enum.GetValues<ERewardType>()));
                                            break;
                                        }
                                        if (words.Length < 6)
                                        {
                                            Console.WriteLine("twitch rewards disable <bot> <channel> <type>");
                                            break;
                                        }

                                        var twitchAuth = rootScope.ServiceProvider.GetRequiredService<TwitchAuthService>();

                                        var botAuth = await twitchAuth.GetRestored(words[3]);
                                        if (botAuth is null || !botAuth.IsValid)
                                        {
                                            Console.WriteLine("Cannot authenticate " + words[3]);
                                            break;
                                        }

                                        var auth = await twitchAuth.GetRestored(words[4]);
                                        if (auth is null || !auth.IsValid)
                                        {
                                            Console.WriteLine("Cannot authenticate " + words[4]);
                                            break;
                                        }

                                        if (!Enum.TryParse<ERewardType>(words[5], out var rewardType))
                                        {
                                            Console.WriteLine("Unknown type: " + words[5]);
                                            break;
                                        }

                                        var rewards = rootScope.ServiceProvider.GetRequiredService<TwitchRewardsService>();
                                        if (await rewards.Disable(auth, botAuth, rewardType))
                                            Console.WriteLine($"{rewardType} enabled");
                                        else
                                            Console.WriteLine($"Failed to enable {rewardType}");

                                        break;
                                    }
                                default:
                                    Console.WriteLine("Available commands: sync, enable, disable");
                                    break;

                            }

                            break;
                        }
                    default:
                        Console.WriteLine("Available commands: connect, disconnect, say, rewards");
                        break;
                }

                break;
            case "tts":
                if (words.Length < 2)
                {
                    Console.WriteLine("Available commands: enable, disable");
                    break;
                }

                var tts = rootScope.ServiceProvider.GetRequiredService<TtsService>();
                switch (words[1])
                {
                    case "enable":
                        tts.Enable();
                        break;
                    case "disable":
                        tts.Disable();
                        break;
                    default:
                        Console.WriteLine("Available commands: enable, disable");
                        break;
                }
                break;
            case "7tv":
                if (words.Length < 2)
                {
                    Console.WriteLine("Available commands: authenticate, addemote");
                    break;
                }

                var sevenTv = rootScope.ServiceProvider.GetRequiredService<SevenTvApiService>();
                switch (words[1])
                {
                    case "authenticate":
                        if (words.Length < 3)
                        {
                            Console.WriteLine("7tv authenticate <token>");
                            break;
                        }
                        var token = string.Join(" ", words.Skip(2));
                        await sevenTv.Authenticate(token);
                        break;
                    case "addemote":
                        if (words.Length < 3)
                        {
                            Console.WriteLine("7tv addemote <emoteid>");
                            break;
                        }
                        var emoteId = string.Join(" ", words.Skip(2));
                        var (_, addEmoteMessage) = await sevenTv.AddEmoteToDefaultSet(emoteId);
                        Console.WriteLine(addEmoteMessage);
                        break;
                    default:
                        Console.WriteLine("Available commands: authenticate, addemote");
                        break;
                }
                break;
            default:
                Console.WriteLine("Available commands: twitch, tts, 7tv");
                break;
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        Console.WriteLine(e.StackTrace);
    }
}

await appTask;

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetStdHandle(int nStdHandle);
[DllImport("kernel32.dll", SetLastError = true)]
static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);
