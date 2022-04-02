using System.Collections.ObjectModel;

namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

public class AfkScreenJs
{
    public string bg { get; set; } = "#1e1e1e";

    public ArrayJs<SymbolJs> symbols = new ArrayJs<SymbolJs>(Array.Empty<SymbolJs>());

    public void SetContent(string content)
    {
        if(!string.IsNullOrWhiteSpace(content))
        {
            symbols = new ArrayJs<SymbolJs>(content.Select((ch, i)
                =>
                {
                    var l = new SymbolJs(ch);
                    l.x = (-(content.Length * 24) / 2 + 12) + i * 24;
                    return l;
                }).ToArray());
        }
        else symbols = new ArrayJs<SymbolJs>(Array.Empty<SymbolJs>());
    }
}

public class SymbolJs
{
    public readonly char symbol;

    public float x = 0;
    public float y = 0;

    public float size = 36;

    public string color = "#ffffff";
    public string shadow = "none";

    public SymbolJs(char ch)
    {
        symbol = ch;
    }
}

public class ArrayJs<T> : ReadOnlyCollection<T>
{
    public ArrayJs(IList<T> list) : base(list) {}
    public int length => this.Count;
}
