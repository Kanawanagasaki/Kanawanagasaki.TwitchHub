using Kanawanagasaki.TwitchHub.Data;
using Microsoft.ClearScript;

namespace Kanawanagasaki.TwitchHub.Services.Commands;

public class JsCommand : ACommand
{
    public override string Name => "js";
    public override string Description => "Run javascript";

    private JavaScriptService _js;

    public JsCommand(JavaScriptService js) => _js = js;
    
    public override ProcessedChatMessage Execute(ProcessedChatMessage chatMessage)
    {
        string code = string.Join(" ", chatMessage.CommandArgs);
        chatMessage.WithCode(new CodeContent(code, ("js", "javascript", "javascript")));

        try
        {
            var result = _js.Execute(chatMessage.Original.Channel, code).Result;
            if(!string.IsNullOrWhiteSpace(result) && result != "[undefined]")
            {
                if(result.StartsWith("/") || result.StartsWith("."))
                    chatMessage.WithReply("Commands not allowed");
                else
                {
                    chatMessage.WithReply(result);
                    chatMessage.WithCustomContent(new OutputContent(result));
                }
            }
            var logs = _js.FlushLogs(chatMessage.Original.Channel);
            if(!string.IsNullOrWhiteSpace(logs))
                chatMessage.WithCustomContent(new OutputContent(logs));
        }
        catch(AggregateException e)
        {
            if(e.InnerException is ScriptEngineException see)
            {
                chatMessage.WithReply(see.Message);
            }
            else if(e.InnerException is TaskCanceledException tce)
            {
                chatMessage.WithReply("JavaScriptException: Execution was interrupted due to timeout");
            }
            else
            {
                Console.WriteLine($"InnerException: {e.InnerException.GetType()}: {e.InnerException.Message}");
                chatMessage.WithReply($"{e.InnerException.GetType()}: {e.InnerException.Message}");
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"{e.GetType()}: {e.Message}");
            chatMessage.WithReply($"{e.GetType()}: {e.Message}");
        }

        return chatMessage.WithoutOriginalMessage();
    }
}