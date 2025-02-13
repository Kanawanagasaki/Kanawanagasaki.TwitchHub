namespace Kanawanagasaki.TwitchHub.Data;

public class HtmlPreviewCustomContent
{
    public required Uri Uri { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}