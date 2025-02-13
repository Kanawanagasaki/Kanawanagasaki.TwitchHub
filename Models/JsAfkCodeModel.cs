namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("js_afk_code_model")]
public class JsAfkCodeModel
{
    [Key]
    public Guid Uuid { get; set; }

    public required string Channel { get; set; }
    public required string InitCode { get; set; }
    public required string TickCode { get; set; }
    public required string SymbolTickCode { get; set; }
}