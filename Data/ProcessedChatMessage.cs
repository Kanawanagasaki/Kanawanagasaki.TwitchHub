namespace Kanawanagasaki.TwitchHub.Data;

using Kanawanagasaki.TwitchHub.Services;
using TwitchLib.Client.Models;

public class ProcessedChatMessage
{
    public ChatMessage Original { get; private set; }
    public RenderFragments Fragments { get; private set; }

    public List<(bool IsEmote, string Content)> ParsedMessage { get; private set; } = new();

    public bool IsCommand { get; private set; } = false;
    public string CommandName { get; private set; }
    public string[] CommandArgs { get; private set; } = Array.Empty<string>();
    public bool ShouldReply { get; private set; } = false;
    public string Reply { get; private set; }

    public List<object> CustomContent { get; private set; } = new();

    public string ImageUrl { get; private set; }

    public string YoutubeVideoId { get; private set; }

    public TwitchGetUsersResponse Sender { get; private set; }
    public string Color { get; private set; } = null;

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
        CustomContent.Add(content);
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

    public ProcessedChatMessage WithCode(CodeContent code)
    {
        Fragments |= RenderFragments.Code;
        return this.WithCustomContent(code);
    }

    public void SetColor(string color)
    {
        Color = color;
    }

    public void SetUser(TwitchGetUsersResponse user)
    {
        Sender = user;
    }

    public void ParseEmotes(BttvEmote[] allBttv)
    {
        var parts = new List<string>();
        var emoteUrls = new List<string>();

        int lastIndex = 0;
        foreach (var emote in Original.EmoteSet.Emotes.OrderBy(e => e.StartIndex))
        {
            parts.Add(Original.Message.Substring(lastIndex, emote.StartIndex - lastIndex));
            emoteUrls.Add($"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/1.0");
            lastIndex = emote.EndIndex + 1;
        }
        parts.Add(Original.Message.Substring(lastIndex));

        for (int i = 0; i < parts.Count; i++)
        {
            var split = parts[i].Split(" ");
            for (int j = 0; j < split.Length; j++)
            {
                var bttv = allBttv.FirstOrDefault(b => b.code == split[j]);
                if (bttv is null) continue;

                parts[i] = string.Join(" ", split.Take(j)) + " ";
                parts.Insert(i + 1, string.Join(" ", split.Skip(j + 1)));

                emoteUrls.Insert(i, $"https://cdn.betterttv.net/emote/{bttv.id}/2x");

                i--;
                break;
            }
        }

        ParsedMessage.Clear();
        for(int i = 0; i < parts.Count; i++)
        {
            ParsedMessage.Add((false, parts[i]));
            if(i < emoteUrls.Count)
            {
                ParsedMessage.Add((true, emoteUrls[i]));
            }
        }
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
        HtmlPreview = 32,
        Code = 64
    }
}