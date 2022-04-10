namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;
using TwitchLib.Client.Models;

public abstract class ACommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual bool IsAuthorizedToExecute(ChatMessage message) => true;

    public virtual ProcessedChatMessage Execute(ProcessedChatMessage chatMessage, TwitchChatService chat) => chatMessage;
    public virtual Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatService chat) => Task.FromResult(chatMessage);
}