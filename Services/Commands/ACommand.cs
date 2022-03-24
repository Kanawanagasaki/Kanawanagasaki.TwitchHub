namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;
using TwitchLib.Client.Models;

public abstract class ACommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual bool IsAuthorizedToExecute(ChatMessage message) => true;

    public abstract ProcessedChatMessage Execute(ProcessedChatMessage chatMessage);
}