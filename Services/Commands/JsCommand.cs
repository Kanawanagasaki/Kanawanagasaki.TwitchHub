using Kanawanagasaki.TwitchHub.Data;
using Microsoft.ClearScript;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class JsCommand(JsEnginesService _jsEngines) : ACommand
{
    public override string Name => "js";
    public override string Description => "Run javascript";

    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage, TwitchChatMessagesService chat)
    {
        string code = string.Join(" ", chatMessage.CommandArgs);
        chatMessage.WithCode(new CodeContent(code, ("js", "javascript", "javascript")));

        var js = _jsEngines.GetEngine(chatMessage.Original.Channel);
        if (js is null)
        {
            chatMessage.WithReply("Js engine not connected to this channel");
            return chatMessage.WithoutOriginalMessage();
        }

        try
        {
            var result = js.Execute(code, true).Result;
            if (!string.IsNullOrWhiteSpace(result) && result != "[undefined]")
            {
                if (result.StartsWith("/") || result.StartsWith("."))
                    chatMessage.WithReply("Commands not allowed");
                else
                {
                    chatMessage.WithReply(result);
                    chatMessage.WithCustomContent(new OutputContent(result));
                }
            }
            var logs = js.FlushLogs();
            if (!string.IsNullOrWhiteSpace(logs))
            {
                if (logs.Length > 450)
                    logs = logs.Substring(0, 450) + "...";
                chatMessage.WithCustomContent(new OutputContent(logs));
                chat.SendMessage(chatMessage.BotAuth.Username, chatMessage.Original.Channel, logs);
            }
        }
        catch (AggregateException e)
        {
            if (e.InnerException is ScriptEngineException see)
            {
                chatMessage.WithReply(see.Message);
            }
            else if (e.InnerException is TaskCanceledException tce)
            {
                chatMessage.WithReply("JavaScriptException: Execution was interrupted due to timeout");
            }
            else
            {
                Console.WriteLine($"InnerException: {e.InnerException?.GetType()}: {e.InnerException?.Message}");
                chatMessage.WithReply($"{e.InnerException?.GetType()}: {e.InnerException?.Message}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.GetType()}: {e.Message}");
            chatMessage.WithReply($"{e.GetType()}: {e.Message}");
        }

        return chatMessage.WithoutOriginalMessage();
    }
}