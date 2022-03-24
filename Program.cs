using Kanawanagasaki.TwitchHub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<CommandsService>();
builder.Services.AddSingleton<EmotesService>();
builder.Services.AddSingleton<TwitchAuthService>();
builder.Services.AddSingleton<TwitchApiService>();
builder.Services.AddSingleton<TwitchChatService>();

builder.Services.AddHostedService(sp => sp.GetService<TwitchAuthService>());
builder.Services.AddHostedService(sp => sp.GetService<TwitchChatService>());

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
