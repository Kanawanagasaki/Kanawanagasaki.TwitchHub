using Kanawanagasaki.TwitchHub.Data;
using Kanawanagasaki.TwitchHub.Services.Commands;
using TwitchLib.Client.Models;

namespace Kanawanagasaki.TwitchHub.Services;

public class CommandsService
{
    private Dictionary<string, ACommand> _commands = new();
    public IReadOnlyDictionary<string, ACommand> Commands => _commands;

    private Dictionary<string, string> _externalCommands = new();
    public IReadOnlyDictionary<string, string> ExternalCommands => _externalCommands;

    private Dictionary<string, string> _textCommands = new();
    public IReadOnlyDictionary<string, string> TextCommands => _textCommands;

    public CommandsService(JavaScriptService js)
    {
        RegisterCommand(new DiceCommand());
        RegisterCommand(new CommandsCommand(this));
        RegisterCommand(new HelpCommand(this));
        RegisterCommand(new AddCommandCommand(this));
        RegisterCommand(new RemoveCommandCommand(this));
        RegisterCommand(new JsCommand(js));

        _externalCommands.Add("drop", "Drop from the sky!");

        if(File.Exists("textcommands.txt"))
        {
            var lines = File.ReadAllLines("textcommands.txt");
            for(int i = 0; i < lines.Length - 1; i+=2)
                _textCommands[lines[i]] = lines[i + 1];
        }
    }

    public void AddTextCommand(string commandName, string text)
    {
        _textCommands[commandName] = text;
        var lines = new List<string>();
        foreach(var kv in _textCommands)
        {
            lines.Add(kv.Key);
            lines.Add(kv.Value);
        }
        File.WriteAllLines("textcommands.txt", lines);
    }

    public void RemoveTextCommand(string commandName)
    {
        if(_textCommands.ContainsKey(commandName))
        {
            _textCommands.Remove(commandName);
            var lines = new List<string>();
            foreach(var kv in _textCommands)
            {
                lines.Add(kv.Key);
                lines.Add(kv.Value);
            }
            File.WriteAllLines("textcommands.txt", lines);
        }
    }

    private void RegisterCommand(ACommand command)
    {
        _commands[command.Name] = command;
    }

    public ProcessedChatMessage ProcessMessage(ChatMessage message)
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
                    processedMessage = _commands[commandName].Execute(processedMessage.AsCommand(commandName, args));
                else processedMessage = processedMessage.WithReply($"@{message.DisplayName}, you not authozired to execute this command");
            }
            else if(_textCommands.ContainsKey(commandName))
                processedMessage = processedMessage.WithReply(_textCommands[commandName]);
        }

        return processedMessage;
    }
}
