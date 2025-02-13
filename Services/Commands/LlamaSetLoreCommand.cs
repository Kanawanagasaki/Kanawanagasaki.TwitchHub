namespace Kanawanagasaki.TwitchHub.Services.Commands;

using Kanawanagasaki.TwitchHub.Data;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Models;

public class LlamaSetLoreCommand(SQLiteContext _db) : ACommand
{
    public override string Name => "llamasetlore";

    public override string Description => "Set lore for llama model";

    public override bool IsAuthorizedToExecute(ChatMessage message)
        => message.IsBroadcaster || message.IsModerator || message.IsVip;

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        var lore = string.Join(" ", chatMessage.CommandArgs);

        var extraLore = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "llama_extra_lore");
        if (extraLore is null)
        {
            extraLore = new()
            {
                Uuid = Guid.NewGuid(),
                Key = "llama_extra_lore",
                Value = lore
            };
            _db.Settings.Add(extraLore);
        }
        else
            extraLore.Value = lore;

        await _db.SaveChangesAsync();

        return chatMessage.WithoutMessage().WithReply($"@{chatMessage.Original.DisplayName}, lore successfully updated");
    }
}