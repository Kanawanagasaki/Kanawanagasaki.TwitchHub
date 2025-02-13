namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("viewer_voice")]
public class ViewerVoice
{
    [Key]
    public Guid Uuid { get; set; }

    public required string Username { get; set; }
    public required string VoiceName { get; set; }
    public int Pitch { get; set; }
    public double Rate { get; set; }
}
