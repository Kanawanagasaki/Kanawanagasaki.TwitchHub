using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class CommandsCommand : ACommand
{
    public override string Name => "commands";

    public override string Description => "Show all commands";

    private CommandsService _service;

    public CommandsCommand(CommandsService service) : base()
    {
        _service = service;
    }

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage)
    {
        List<string> commandNames = new();
        commandNames.AddRange(_service.Commands.Where(c => c.Value.IsAuthorizedToExecute(chatMessage.Original)).Select(c => c.Value.Name));
        commandNames.AddRange(_service.ExternalCommands.Select(c => c.Key));
        commandNames.AddRange(_service.TextCommands.Select(c => c.Key));

        return chatMessage
            .WithoutMessage()
            .WithReply($"@{chatMessage.Original.DisplayName}, "  + string.Join(", ", commandNames.OrderBy(c => c).Select(c => $"!{c}")));
    }
}
