using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class DiceCommand : ACommand
{
    public override string Name => "dice";

    public override string Description => "Throw a dice and get a number!";

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage)
    {
        int min = 1, max = 6;
        if(chatMessage.CommandArgs.Length == 1)
            int.TryParse(chatMessage.CommandArgs[0], out max);
        else if(chatMessage.CommandArgs.Length >= 2)
        {
            int.TryParse(chatMessage.CommandArgs[0], out min);
            int.TryParse(chatMessage.CommandArgs[1], out max);
        }

        min = Math.Clamp(min, 1, int.MaxValue - 1);
        max = Math.Clamp(max, 1, int.MaxValue - 1);

        if(min > max)
            (min, max) = (max, min);

        int result = Random.Shared.Next(min, max + 1);

        return chatMessage
            .WithoutOriginalMessage()
            .WithCustomContent("🎲 " + result)
            .WithReply($"@{chatMessage.Original.DisplayName} throw {(max - min + 1)} sided dice and got {result}");
    }
}
