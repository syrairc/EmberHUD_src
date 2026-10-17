using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal readonly record struct GroundAffixes(int DropId, bool Owned, int[] PrefixIds, int[] SuffixIds);

internal sealed class GroundItemReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? System.Diagnostics.Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);

    internal GroundAffixes[] ReadLua()
    {
        if (!_lua.TryOpen(out var globals) ||
            !_lua.TryGetTable(globals, Offsets.LuaGroundManager, out var manager) ||
            !_lua.TryGetTable(manager, Offsets.LuaGroundItems, out var items))
            return [];

        return [.. _lua.IntKeyedTables(items).Select(e => ReadEntry(e.Key, e.Value)).OrderBy(g => g.DropId).Take(Offsets.MaxGroundItems)];
    }

    private GroundAffixes ReadEntry(long key, LuaTable entry)
    {
        _lua.TryGetBool(entry, Offsets.LuaGroundBelong, out var owned);
        var dropId = (int)key;
        int[] prefixes = [], suffixes = [];
        if (_lua.TryGetTable(entry, Offsets.LuaGroundDropItem, out var drop))
        {
            if (_lua.TryGetInt(drop, Offsets.LuaGroundDropId, out var id) && id is >= int.MinValue and <= int.MaxValue) dropId = (int)id;
            if (_lua.TryGetTable(drop, Offsets.LuaGroundItem, out var item))
            {
                prefixes = ReadAffixes(item, Offsets.LuaGroundPrefix);
                suffixes = ReadAffixes(item, Offsets.LuaGroundSuffix);
            }
        }

        return new GroundAffixes(dropId, owned, prefixes, suffixes);
    }

    private int[] ReadAffixes(LuaTable item, string key) =>
        _lua.TryGetTable(item, key, out var table)
            ? [.. _lua.IntKeyedTables(table).OrderBy(r => r.Key).Take(Offsets.MaxGroundAffixes)
                .Select(r => (ok: _lua.TryGetInt(r.Value, Offsets.LuaGroundAffixId, out var id) && id is >= int.MinValue and <= int.MaxValue, id))
                .Where(r => r.ok).Select(r => (int)r.id)]
            : [];
}
