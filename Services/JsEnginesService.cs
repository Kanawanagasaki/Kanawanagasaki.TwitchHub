using System.Collections.Concurrent;
using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services;

public class JsEnginesService : IDisposable
{
    private SQLiteContext _db = new();
    private Dictionary<string, JsEngine> _engines = new();

    public JsEngine GetEngine(string channel)
    {
        JsEngine engine;
        lock(_engines) lock(_db)
        {
            if(_engines.ContainsKey(channel))
                return _engines[channel];
            
            engine = new JsEngine(_db, channel);
            _engines[channel] = engine;
        }
        return engine;
    }

    public void Dispose()
    {
        lock(_engines)
        {
            foreach(var kv in _engines)
                kv.Value.Dispose();
            _engines.Clear();
        }
    }
}
