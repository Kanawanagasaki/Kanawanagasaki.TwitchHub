using System.Globalization;
using Kanawanagasaki.TwitchHub.Services;
using Kanawanagasaki.TwitchHub;
using Microsoft.EntityFrameworkCore;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

CultureInfo ci = new CultureInfo("ja-JP");
CultureInfo.DefaultThreadCurrentCulture = ci;
Thread.CurrentThread.CurrentCulture = ci;
Thread.CurrentThread.CurrentUICulture = ci;

using (var db = new SQLiteContext())
    db.Database.Migrate();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<SQLiteContext>();

builder.Services.AddScoped<HelperService>();
builder.Services.AddScoped<CommandsService>();
builder.Services.AddScoped<EmotesService>();
builder.Services.AddScoped<TwitchAuthService>();
builder.Services.AddScoped<TwitchApiService>();
builder.Services.AddScoped<TwitchChatMessagesService>();

builder.Services.AddSingleton<JsEnginesService>();
builder.Services.AddSingleton<TtsService>();
builder.Services.AddSingleton<TwitchChatService>();
builder.Services.AddSingleton<LlamaService>();

builder.Services.AddHostedService(sp => sp.GetService<TtsService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    builder.WebHost.UseUrls("http://localhost:5678");
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
