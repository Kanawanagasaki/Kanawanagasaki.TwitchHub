using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services.Commands;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Models;

namespace Kanawanagasaki.TwitchHub.Services;

public class CommandsService
{
    private SQLiteContext _db;

    private Dictionary<string, ACommand> _commands = new();
    public IReadOnlyDictionary<string, ACommand> Commands => _commands;

    private Dictionary<string, string> _externalCommands = new();
    public IReadOnlyDictionary<string, string> ExternalCommands => _externalCommands;

    public CommandsService(SQLiteContext db, TtsService tts)
    {
        _db = db;

        RegisterCommand(new DiceCommand());
        RegisterCommand(new CommandsCommand(this, db));
        RegisterCommand(new HelpCommand(this, db));
        RegisterCommand(new AddCommandCommand(this));
        RegisterCommand(new RemoveCommandCommand(this));
        RegisterCommand(new JsCommand());
        RegisterCommand(new GetVoicesCommand(tts));
        RegisterCommand(new SetVoiceCommand(db, tts));

        _externalCommands.Add("drop", "Drop from the sky!");
    }

    public async Task AddTextCommand(string commandName, string text)
    {
        var model = await _db.TextCommands.FirstOrDefaultAsync(m => m.Name.ToLower() == commandName.ToLower());
        if(model is null)
        {
            model = new()
            {
                Name = commandName.ToLower(),
                Text = text
            };
            await _db.TextCommands.AddAsync(model);
        }
        else model.Text = text;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> RemoveTextCommand(string commandName)
    {
        var model = await _db.TextCommands.FirstOrDefaultAsync(m => m.Name.ToLower() == commandName.ToLower());
        if(model is not null)
        {
            _db.TextCommands.Remove(model);
            await _db.SaveChangesAsync();
            return true;
        }
        else return false;
    }

    private void RegisterCommand(ACommand command)
    {
        _commands[command.Name] = command;
    }

    public async Task<ProcessedChatMessage> ProcessMessage(ChatMessage message, TwitchChatMessagesService chat)
    {
        var processedMessage = new ProcessedChatMessage(message);

        if(message.Message.StartsWith("!"))
        {
            string[] split = message.Message.Substring(1).Split(" ");
            string commandName = split[0];
            string[] args = split.Skip(1).ToArray();
            if(_commands.ContainsKey(commandName))
            {
                if(_commands[commandName].IsAuthorizedToExecute(message))
                {
                    processedMessage = processedMessage.AsCommand(commandName, args);
                    processedMessage = _commands[commandName].Execute(processedMessage, chat);
                    processedMessage = await _commands[commandName].ExecuteAsync(processedMessage, chat);
                }
                else processedMessage = processedMessage.WithReply($"@{message.DisplayName}, you not authozired to execute this command");
            }
            else
            {
                var command = await _db.TextCommands.FirstOrDefaultAsync(m => m.Name == commandName.ToLower());
                if(command is not null)
                    processedMessage = processedMessage.WithReply(command.Text);
            }
        }

        return processedMessage;
    }
}
