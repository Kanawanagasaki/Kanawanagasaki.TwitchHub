using Kanawanagasaki.TwitchHub.Data;
namespace Kanawanagasaki.TwitchHub.Services.Commands;

using TwitchLib.Client.Models;

public class LlamaResetCommand : ACommand
{
    public override string Name => "llamareset";

    public override string Description => "Will restart llama context";

    private LlamaService _llama;

    public LlamaResetCommand(LlamaService llama) : base()
    {
        _llama = llama;
    }
    
    public override Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        _llama.Reset();
        return Task.FromResult(chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, llama context was reset successfully"));
    }
}