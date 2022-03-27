namespace Kanawanagasaki.TwitchHub.Data;

public class CodeContent
{
    public string HighlighterClass = "";
    public CSLine[] Lines { get; private set; }

    public CodeContent(string code, string language)
    {
        HighlighterClass = $"language-{language}";

        var lines = code
            .Replace("{", "\n{\n")
            .Replace("}", "\n}\n")
            .Replace(";", ";\n")
            .Split("\n")
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new CSLine(l.Trim()))
            .ToList();

        var regex = new System.Text.RegularExpressions.Regex("[a-z0-9\\{\\}]");

        int indent = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (regex.IsMatch(lines[i].Text))
            {
                if (lines[i].Text == "}") indent--;
                lines[i].Indent = indent;
                if (lines[i].Text == "{") indent++;
            }
            else
            {
                if (i > 0)
                {
                    lines[i - 1].Text += lines[i].Text;
                    lines.RemoveAt(i);
                    i--;
                }
            }
        }

        Lines = lines.ToArray();
    }
}

public class CSLine
{
    public string Text { get; set; }
    public int Indent { get; set; } = 0;

    public CSLine(string text) => Text = text;

    public override string ToString() => new string(Enumerable.Range(0, Indent).Select(_ => '\t').ToArray()) + Text;
}