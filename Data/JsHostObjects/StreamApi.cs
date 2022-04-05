namespace Kanawanagasaki.TwitchHub.Data.JsHostObjects;

public class StreamApi
{
    public AfkSceneApi afk { get; private set; }

    private JsEngine _engine;

    public StreamApi(JsEngine engine)
    {
        _engine = engine;
        afk = new AfkSceneApi(engine);
    }

    public override string ToString()
        => $@"{{ afk: AfkSceneApi }}";
}