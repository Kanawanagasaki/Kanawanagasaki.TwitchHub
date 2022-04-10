using System.Globalization;
using Kanawanagasaki.TwitchHub.Services;
using Kanawanagasaki.TwitchHub;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddScoped<TwitchChatService>();
builder.Services.AddScoped<CommandsService>();

builder.Services.AddSingleton<EmotesService>();
builder.Services.AddSingleton<TwitchAuthService>();
builder.Services.AddSingleton<TwitchApiService>();
builder.Services.AddSingleton<TtsService>();
builder.Services.AddSingleton<JavaScriptService>();

builder.Services.AddHostedService(sp => sp.GetService<EmotesService>());
builder.Services.AddHostedService(sp => sp.GetService<TwitchAuthService>());
builder.Services.AddHostedService(sp => sp.GetService<TtsService>());

builder.WebHost.UseUrls("http://localhost:5678");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
