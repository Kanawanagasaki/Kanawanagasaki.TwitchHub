namespace Kanawanagasaki.TwitchHub.Data;

using TwitchLib.Client.Models;

public class ProcessedChatMessage
{
    public ChatMessage Original { get; private set; }
    public RenderFragments Fragments { get; private set; }

    public bool IsCommand { get; private set; } = false;
    public string CommandName { get; private set; }
    public string[] CommandArgs { get; private set; } = Array.Empty<string>();
    public bool ShouldReply { get; private set; } = false;
    public string Reply { get; private set; }

    public object CustomContent { get; private set; }

    public string ImageUrl { get; private set; }

    public string YoutubeVideoId { get; private set; }

    public ProcessedChatMessage(ChatMessage originalMessage)
    {
        Original = originalMessage;
        Fragments = RenderFragments.Message | RenderFragments.OriginalMessage;
    }

    public ProcessedChatMessage AsCommand(string commandName, string[] args)
    {
        IsCommand = true;
        CommandName = commandName;
        CommandArgs = args;
        return this;
    }

    public ProcessedChatMessage WithoutMessage()
    {
        Fragments &= ~RenderFragments.Message;
        return this;
    }

    public ProcessedChatMessage WithoutOriginalMessage()
    {
        Fragments &= ~RenderFragments.OriginalMessage;
        return this;
    }

    public ProcessedChatMessage WithCustomContent(object content)
    {
        CustomContent = content;
        Fragments |= RenderFragments.CustomContent;
        return this;
    }

    public ProcessedChatMessage WithReply(string reply)
    {
        Reply = reply;
        ShouldReply = true;
        return this;
    }

    public ProcessedChatMessage WithImage(string url)
    {
        this.ImageUrl = url;
        Fragments |= RenderFragments.Image;
        return this;
    }

    public ProcessedChatMessage WithYoutubeVideo(string videoId)
    {
        this.YoutubeVideoId = videoId;
        Fragments |= RenderFragments.YoutubeVideo;
        return this;
    }

    [Flags]
    public enum RenderFragments
    {
        None = 0,
        Message = 1,
        OriginalMessage = 2,
        CustomContent = 4,
        Image = 8,
        YoutubeVideo = 16,
        HtmlPreview = 32
    }
}