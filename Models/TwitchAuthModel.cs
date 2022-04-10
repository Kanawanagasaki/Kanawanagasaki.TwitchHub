using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kanawanagasaki.TwitchHub.Models;

[Table("twitch_auth")]
public class TwitchAuthModel
{
    [Key]
    public Guid Uuid { get; set; }

    public string UserId { get; set; }
    public string Username { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }

    public bool IsValid { get; set; }
}