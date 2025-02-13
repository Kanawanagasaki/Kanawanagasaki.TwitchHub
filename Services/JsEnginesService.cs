using System.Collections.Concurrent;
using Kanawanagasaki.TwitchHub.Data;

namespace Kanawanagasaki.TwitchHub.Services;

public class JsEnginesService : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly SQLiteContext _db;

    private ConcurrentDictionary<string, JsEngine> _engines = new();

    public JsEnginesService(IServiceScopeFactory serviceScopeFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
    }

    public JsEngine GetEngine(string channel)
    {
        JsEngine? engine;
        if (_engines.TryGetValue(channel, out engine))
            return engine;

        engine = new JsEngine(_db, channel);
        _engines.AddOrUpdate(channel, engine, (_, _) => engine);
        return engine;
    }

    public void Dispose()
    {
        foreach (var kv in _engines.Values)
            kv.Dispose();
        _engines.Clear();
    }
}
