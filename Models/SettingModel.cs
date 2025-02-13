namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("setting"), Index(nameof(Key))]
public class SettingModel
{
    [Key]
    public required Guid Uuid { get; set; }

    public required string Key { get; set; }
    public required string Value { get; set; }
}
