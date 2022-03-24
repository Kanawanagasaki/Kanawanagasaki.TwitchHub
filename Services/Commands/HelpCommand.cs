using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class HelpCommand : ACommand
{
    public override string Name => "help";

    public override string Description => "Show description of the command";

    private CommandsService _service;

    public HelpCommand(CommandsService service) : base()
    {
        _service = service;
    }

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage)
    {
        if(chatMessage.CommandArgs.Length == 0)
            return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, specify comamnd name to see description");
        else
        {
            var commandName = chatMessage.CommandArgs.First();
            if(_service.Commands.ContainsKey(commandName))
            {
                if(_service.Commands[commandName].IsAuthorizedToExecute(chatMessage.Original))
                    return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {_service.Commands[commandName].Description}");
                else return chatMessage.WithReply($"@{chatMessage.Original.DisplayName}, you not authorized to execute this command");
            }
            else if(_service.ExternalCommands.ContainsKey(commandName))
                return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {_service.ExternalCommands[commandName]}");
            else if(_service.TextCommands.ContainsKey(commandName))
                return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, command with useful information ;)");
            else return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {commandName} not found");
        }
    }
}