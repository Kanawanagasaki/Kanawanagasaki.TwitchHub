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

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatService chat)
    {
        if(chatMessage.CommandArgs.Length == 0)
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, please provide command name");
        if(!(await _service.RemoveTextCommand(chatMessage.CommandArgs[0])))
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {chatMessage.CommandArgs[0]} not found");
            
        return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, command successfully removed");
    }
}