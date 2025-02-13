using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kanawanagasaki.TwitchHub.Models;

[Table("twitch_auth")]
public class TwitchAuthModel
{
    [Key]
    public Guid Uuid { get; set; }

    public required string UserId { get; set; }
    public required string Username { get; set; }
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }

    public bool IsValid { get; set; } = false;
}