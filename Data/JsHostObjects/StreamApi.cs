namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

public class StreamApi
{
    public AfkSceneApi afk { get; private set; }

    private JsEngine _engine;

    public StreamApi(SQLiteContext db, JsEngine engine, string channel)
    {
        _engine = engine;
        afk = new AfkSceneApi(db, engine, channel);
    }

    public string toString()
        => ToString();
    public override string ToString()
        => $@"{{ afk: AfkSceneApi }}";
}
