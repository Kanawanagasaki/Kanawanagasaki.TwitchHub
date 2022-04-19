using Kanawanagasaki.TwitchHub.Data;
namespace Kanawanagasaki.TwitchHub.Services.Commands;

using TwitchLib.Client.Models;

public class AddCommandCommand : ACommand
{
    public override string Name => "addcommand";

    public override string Description => "Create new text command";

    private CommandsService _service;

    public AddCommandCommand(CommandsService service) : base()
    {
        _service = service;
    }

    public override bool IsAuthorizedToExecute(ChatMessage message) => message.IsBroadcaster;

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        if(chatMessage.CommandArgs.Length < 2)
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, please provide command name and text for it");

        await _service.AddTextCommand(chatMessage.CommandArgs[0], string.Join(" ", chatMessage.CommandArgs.Skip(1)));
        return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, command successfully created");
    }
}