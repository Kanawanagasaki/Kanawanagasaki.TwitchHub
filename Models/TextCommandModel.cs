namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("text_command")]
public class TextCommandModel
{
    [Key]
    public Guid Uuid { get; set; }

    public string Name { get; set; }
    public string Text { get; set; }
}
