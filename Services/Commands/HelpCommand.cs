using Kanawanagasaki.TwitchHub.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class HelpCommand : ACommand
{
    public override string Name => "help";

    public override string Description => "Show description of the command";

    private CommandsService _service;
    private SQLiteContext _db;

    public HelpCommand(CommandsService service, SQLiteContext db) : base()
    {
        _service = service;
        _db = db;
    }

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
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
            else
            {
                var model = await _db.TextCommands.FirstOrDefaultAsync(c => c.Name.ToLower() == commandName.ToLower());
                if(model is not null) return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, command with useful information ;)");
                else return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, {commandName} not found");
            }
        }
    }
}