namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;
using TwitchLib.Client.Models;

public class RemoveCommandCommand : ACommand
{
    public override string Name => "removecommand";

    public override string Description => "Remove text command";

    private CommandsService _service;

    public RemoveCommandCommand(CommandsService service) : base()
    {
        _service = service;
    }
    
    public override bool IsAuthorizedToExecute(ChatMessage message) => message.IsBroadcaster;

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage)
    {
        if(chatMessage.CommandArgs.Length == 0)
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, please provide command name");
        if(!_service.TextCommands.ContainsKey(chatMessage.CommandArgs[0]))
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {chatMessage.CommandArgs[0]} not found");
        
        _service.RemoveTextCommand(chatMessage.CommandArgs[0]);
        return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, command successfully removed");
    }
}