namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;

public class PingCommand : ACommand
{
    public override string Name => "ping";

    public override string Description => "Ping the bot to check if it is connected to irc and listening to this channel";

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage, TwitchChatMessagesService _)
        => chatMessage.WithReply($"Pong!");
}
