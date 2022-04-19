using System.Collections.Concurrent;
using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services;

public class JsEnginesService : IDisposable
{
    private SQLiteContext _db = new();
    private ConcurrentDictionary<string, JsEngine> _engines = new();

    public JsEngine GetEngine(string channel)
    {
        if(_engines.TryGetValue(channel, out var ret))
            return ret;
        
        var engine = new JsEngine(_db, channel);
        _engines.TryAdd(channel, engine);
        return engine;
    }

    public void Dispose()
    {
        foreach(var kv in _engines)
            kv.Value.Dispose();
        _engines.Clear();
    }
}
