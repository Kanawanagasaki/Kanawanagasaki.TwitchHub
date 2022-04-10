namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("js_afk_code_model")]
public class JsAfkCodeModel
{
    [Key]
    public Guid Uuid { get; set; }

    public string Channel { get; set; }
    public string InitCode { get; set; }
    public string TickCode { get; set; }
    public string SymbolTickCode { get; set; }
}