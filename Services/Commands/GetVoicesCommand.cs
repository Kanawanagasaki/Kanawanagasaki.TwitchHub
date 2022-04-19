using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class GetVoicesCommand : ACommand
{
    public override string Name => "getvoices";

    public override string Description => "Get voices available for tts";

    private TtsService _tts;

    public GetVoicesCommand(TtsService tts) => _tts = tts;

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage message, TwitchChatMessagesService chat)
    {
        var voices = await _tts.GetVoices();
        if (message.CommandArgs.Length > 0)
            voices = voices.Where(v => v.ShortName.StartsWith(message.CommandArgs[0])).ToArray();
        if (voices.Length == 0)
            return message.WithReply("Voices not found");

        if(message.CommandArgs.Length == 0)
            return message.WithReply("Choose language: " + string.Join(", ", voices.Select(v => v.Locale.Substring(0, 2)).Distinct()));
        else if(message.CommandArgs.Length > 0 && message.CommandArgs[0].Length == 2)
            return message.WithReply("Choose language: " + string.Join(", ", voices.Select(v => v.Locale).Distinct()));
        else
            return message.WithReply("Available voices: " + string.Join(", ", voices.Select(v => v.ShortName.Replace(v.Locale, "").Replace("-", "").Replace("Neural", "")).Distinct()));
    }
}
