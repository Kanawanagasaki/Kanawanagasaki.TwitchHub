using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class GetVoicesCommand : ACommand
{
    public override string Name => "getvoices";

    public override string Description => "Get voices available for tts. Use !getvoices, !getvoices <language>";

    private TtsService _tts;

    public GetVoicesCommand(TtsService tts) => _tts = tts;

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage message, TwitchChatMessagesService chat)
    {
        var voices = await _tts.GetVoices();
        if (0 < message.CommandArgs.Length)
            voices = voices.Where(v => v.ShortName is not null && TransformString(v.ShortName).StartsWith(TransformString(message.CommandArgs[0]))).ToArray();
        if (voices.Length == 0)
            return message.WithReply("Voices not found");

        if (message.CommandArgs.Length == 0)
            return message.WithReply("Pick a language: " + string.Join(", ", voices.Where(v => v.Locale is not null).Select(v => v.Locale!.Substring(0, 2)).Distinct()) + ". Use !getvoices <language-code>");
        else if (0 < message.CommandArgs.Length && message.CommandArgs[0].Length == 2 && voices.Select(v => v.Locale).Distinct().Count() != 1)
            return message.WithReply("Pick a language: " + string.Join(", ", voices.Select(v => v.Locale).Distinct()) + ". Use !getvoices <language-code>");
        else
            return message.WithReply("Available voices: " + string.Join(", ", voices.Where(v => v.ShortName is not null && v.Locale is not null).Select(v => v.ShortName!.Replace(v.Locale!, "").Replace("-", "").Replace("Neural", "")).Distinct())+ ". Use !setvoice <voice>");
    }

    private string TransformString(string str)
        => new string(str.Where(x => char.IsLetterOrDigit(x) || x == ' ').ToArray()).ToLower();
}
