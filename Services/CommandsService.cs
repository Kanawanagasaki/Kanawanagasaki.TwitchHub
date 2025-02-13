using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Hubs;
using Kanawanagasaki.TwitchHub.Models;
using Kanawanagasaki.TwitchHub.Services.Commands;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Models;

namespace Kanawanagasaki.TwitchHub.Services;

public class CommandsService
{
    private IServiceScope _scope;
    private SQLiteContext _db;
    private ILogger<CommandsService> _logger;

    private Dictionary<string, ACommand> _commands = new();
    public IReadOnlyDictionary<string, ACommand> Commands => _commands;

    private Dictionary<string, string> _externalCommands = new();
    public IReadOnlyDictionary<string, string> ExternalCommands => _externalCommands;

    public CommandsService(IServiceScopeFactory serviceScopeFactory, JsEnginesService jsEngines, TtsService tts, LlamaService llama, IHubContext<YoutubeHub> youtubeHub, ILogger<CommandsService> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
        _logger = logger;

        RegisterCommand(new PingCommand());
        RegisterCommand(new DiceCommand());
        RegisterCommand(new CommandsCommand(this, _db));
        RegisterCommand(new HelpCommand(this, _db));
        RegisterCommand(new AddCommandCommand(this));
        RegisterCommand(new RemoveCommandCommand(this));
        RegisterCommand(new JsCommand(jsEngines));
        RegisterCommand(new GetVoicesCommand(tts));
        RegisterCommand(new SetVoiceCommand(_db, tts));
        RegisterCommand(new LlamaResetCommand(llama));
        RegisterCommand(new LlamaSetLoreCommand(_db));
        RegisterCommand(new SongCommand(youtubeHub));

        _externalCommands.Add("drop", "Drop from the sky!");
    }

    public async Task AddTextCommand(string commandName, string text)
    {
        var model = await _db.TextCommands.FirstOrDefaultAsync(m => m.Name.ToLower() == commandName.ToLower());
        if (model is null)
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
        if (model is not null)
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

    public async Task<ProcessedChatMessage> ProcessMessage(TwitchAuthModel botAuth, ChatMessage message, TwitchChatMessagesService chat)
    {
        var processedMessage = new ProcessedChatMessage(botAuth, message);

        try
        {
            if (message.Message.StartsWith("!"))
            {
                string[] split = message.Message.Substring(1).Split(" ");
                string commandName = split[0];
                string[] args = split.Skip(1).ToArray();
                if (_commands.ContainsKey(commandName))
                {
                    if (_commands[commandName].IsAuthorizedToExecute(message))
                    {
                        processedMessage = processedMessage.AsCommand(commandName, args);
                        processedMessage = _commands[commandName].Execute(processedMessage, chat);
                        processedMessage = await _commands[commandName].ExecuteAsync(processedMessage, chat);
                    }
                    else processedMessage = processedMessage.WithReply($"@{message.DisplayName}, you not authorized to execute this command");
                }
                else
                {
                    var command = await _db.TextCommands.FirstOrDefaultAsync(m => m.Name == commandName.ToLower());
                    if (command is not null)
                        processedMessage = processedMessage.WithReply(command.Text);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }

        return processedMessage;
    }
}
