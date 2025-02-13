namespace Kanawanagasaki.TwitchHub.Hubs;

using System.Threading.Tasks;
using Kanawanagasaki.TwitchHub.Services;
using Microsoft.AspNetCore.SignalR;

public class YoutubeHub(TwitchChatMessagesService _chat) : Hub
{
    public void Song(string channelName, string botName, string replyId, string song)
    {
        _chat.SendMessage(botName, channelName, replyId, $"Song currently playing: {song}");
    }
}
