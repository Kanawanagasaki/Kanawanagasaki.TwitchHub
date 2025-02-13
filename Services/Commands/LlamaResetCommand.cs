namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;

public class LlamaResetCommand(LlamaService _llama) : ACommand
{
    public override string Name => "llamareset";

    public override string Description => "Will restart llama context";

    public override Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        _llama.Reset();
        return Task.FromResult(chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, llama context has been reset"));
    }
}