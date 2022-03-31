using System.Text;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kanawanagasaki.TwitchHub.Data;

public class CodeContent
{
    public (string code, string name, string slug) Language {get;private set;}
    public string Code {get;private set;}
    public CSLine[] Lines { get; private set; }

    public string HighlighterClass => $"language-{Language.slug}";

    public CodeContent(string code, (string code, string name, string slug) lang)
    {
        Code = code;
        Language = lang;
    }

    public async Task Format(IJSRuntime js)
    {
        Lines = await Prettier(js);
        if(Lines is not null) return;

        Lines = await FormatterOrg();
        if(Lines is not null) return;

        Lines = DefaultFormatter();
    }

    private async Task<CSLine[]> Prettier(IJSRuntime js)
    {
        string[] availableLanguages = new[] { "typescript", "css", "json", "html" };
        if(!availableLanguages.Contains(Language.slug)) return null;

        var result = await js.InvokeAsync<string>("prettierFormat", Language.slug, Code);
        if(string.IsNullOrWhiteSpace(result)) return null;
        
        var lines = result.Trim().Split("\n");
        return lines.Select(l => new CSLine(l.Trim()) { Indent = l.TakeWhile(Char.IsWhiteSpace).Count() }).ToArray();
    }

    private async Task<CSLine[]> FormatterOrg()
    {
        string[] availableLanguages = new[] { "cpp", "java", "csharp", "objective-c", "javascript", "protobuf" };
        if(!availableLanguages.Contains(Language.slug)) return null;

        Dictionary<string, string> langToStyle = new()
        {
            { "cpp", "Google" },
            { "java", "Mozilla" },
            { "csharp", "Mozilla" },
            { "objective-c", "Mozilla" },
            { "javascript", "Mozilla" },
            { "protobuf", "Mozilla" }
        };

        Dictionary<string, string> data = new()
        {
            { "language", Language.slug },
            { "codeSrc", Code },
            { "style", langToStyle[Language.slug] },
            { "indentWidth", "1" },
            { "columnLimit", "160" }
        };

        using var form = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await http.PostAsync("https://formatter.org/admin/format", form);
        if(response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<JObject>(json);
            var parsedCode = result.Value<string>("codeDst");
            var lines = parsedCode.Split("\n");
            return lines.Select(l => new CSLine(l.Trim()) { Indent = l.TakeWhile(Char.IsWhiteSpace).Count() }).ToArray();
        }

        return null;
    }

    private CSLine[] DefaultFormatter()
    {
        var lines = Code
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

        return lines.ToArray();
    }
}

public class CSLine
{
    public string Text { get; set; }
    public int Indent { get; set; } = 0;

    public CSLine(string text) => Text = text;

    public override string ToString() => new string(Enumerable.Range(0, Indent).Select(_ => '\t').ToArray()) + Text;
}