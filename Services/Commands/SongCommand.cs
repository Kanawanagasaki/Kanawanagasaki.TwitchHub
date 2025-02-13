using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class SongCommand : ACommand
{
    public override string Name => "song";
    public override string Description => "Chech what song is playing right now";

    private IHubContext<YoutubeHub> _hub;

    public SongCommand(IHubContext<YoutubeHub> hub)
    {
        _hub = hub;
    }

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        await _hub.Clients.All.SendAsync("GetSong", chatMessage.Original.Channel, chatMessage.Original.BotUsername, chatMessage.Original.Id);
        return chatMessage.WithoutMessage().WithoutOriginalMessage();
    }
}
