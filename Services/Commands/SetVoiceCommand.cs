using Kanawanagasaki.TwitchHub.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class SetVoiceCommand : ACommand
{
    public override string Name => "setvoice";
    public override string Description => "Change your voice for tts on stream";

    private SQLiteContext _db;
    private TtsService _tts;

    public SetVoiceCommand(SQLiteContext db, TtsService tts) => (_db, _tts) = (db, tts);

    public override async Task<ProcessedChatMessage> ExecuteAsync(ProcessedChatMessage message, TwitchChatMessagesService chat)
    {
        if(message.CommandArgs.Length == 0)
            return message.WithReply("Please, provide a name for the voice. Use !getvoices command to see options.");

        var voices = await _tts.GetVoices();
        voices = voices.Where(v => v.ShortName.Contains(message.CommandArgs[0])).ToArray();
        if(voices.Length == 0)
            return message.WithReply("Voice not found");
        else if(voices.Length > 1)
            return message.WithReply("Please, specify voice: " + string.Join(", ", voices.Select(v => v.ShortName)));
        
        int pitch = 0;
        double rate = 1;
        if(message.CommandArgs.Length > 1) int.TryParse(message.CommandArgs[1], out pitch);
        if(message.CommandArgs.Length > 2) double.TryParse(message.CommandArgs[2].Replace(",", "."), out rate);

        pitch = Math.Clamp(pitch, -100, 100);
        rate = Math.Clamp(rate, 0.1, 2);

        var model = await _db.ViewerVoices.FirstOrDefaultAsync(v => v.Username == message.Original.Username);
        if(model is null)
        {
            model = new()
            {
                Username = message.Original.Username,
                VoiceName = voices[0].ShortName,
                Pitch = pitch,
                Rate = rate
            };
            await _db.ViewerVoices.AddAsync(model);
        }
        else
        {
            model.VoiceName = voices[0].ShortName;
            model.Pitch = pitch;
            model.Rate = rate;
        }

        await _db.SaveChangesAsync();

        return message.WithReply("Voice successfully updated.");
    }
}
