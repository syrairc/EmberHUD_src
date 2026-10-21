using System.Collections.Concurrent;

namespace EmberHUD.Host.Reading;

internal sealed class CfgIconCache
{
    private readonly ConcurrentDictionary<int, string> _byId = new();

    public void Add(int id, string? icon)
    {
        if (id != 0 && icon is not null) _byId[id] = icon;
    }

    public string? Get(int id) => _byId.GetValueOrDefault(id);
}
