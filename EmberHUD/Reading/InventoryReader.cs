using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class InventoryReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private InventoryItem[]? _items;
    private static readonly HashSet<int> StashGearTypes = [.. Offsets.StashGearTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)];
    private long _stashNextRefresh;
    private int _stashTab = -1;
    private InventoryItem[]? _stashItems;

    public InventoryItem[]? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _items;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.InventoryRefreshMilliseconds / 1000;
        return _items = Build();
    }

    public InventoryItem[]? ReadStash(int tab)
    {
        var now = _clock();
        if (tab == _stashTab && now < _stashNextRefresh) return _stashItems;
        _stashTab = tab;
        _stashNextRefresh = now + Stopwatch.Frequency * Offsets.StashItemsRefreshMilliseconds / 1000;
        return _stashItems = BuildStash(tab);
    }

    private InventoryItem[]? BuildStash(int tab)
    {
        if (!_lua.TryOpen(out var globals) ||
            !_lua.TryGetTable(globals, Offsets.LuaItemManager, out var manager) ||
            !_lua.TryGetTable(manager, Offsets.LuaItemBags, out var bags) ||
            !_lua.TryGetTableAt(bags, Offsets.StashTabFirstKey + tab, out var slots))
            return null;

        var items = new List<InventoryItem>();
        foreach (var (key, entry) in _lua.IntKeyedTables(slots).OrderBy(s => s.Key).Take(Offsets.StashCells))
            if (TryReadItem(InventoryBag.Commodities, key, entry, out var item))
                items.Add(StashGearTypes.Contains(item.Type) ? item with { Bag = InventoryBag.Equipment } : item);
        return [.. items];
    }

    private InventoryItem[]? Build()
    {
        if (!_lua.TryOpen(out var globals) ||
            !_lua.TryGetTable(globals, Offsets.LuaItemManager, out var manager) ||
            !_lua.TryGetTable(manager, Offsets.LuaItemBags, out var bags))
            return null;

        var items = new List<InventoryItem>();
        var attempts = 0;
        for (var bag = 0; bag < Offsets.InventoryTabCount; bag++)
        {
            if (!_lua.TryGetTableAt(bags, Offsets.InventoryTabFirst + bag, out var slots)) continue;
            foreach (var (key, entry) in _lua.IntKeyedTables(slots).OrderBy(s => s.Key).Take(Offsets.MaxInventoryItems - attempts))
            {
                attempts++;
                if (TryReadItem((InventoryBag)bag, key, entry, out var item)) items.Add(item);
            }
        }

        return [.. items];
    }

    private bool TryReadItem(InventoryBag bag, long slot, LuaTable entry, out InventoryItem item)
    {
        item = null!;
        if (slot < 0 || slot > int.MaxValue ||
            !_lua.TryGetTable(entry, Offsets.LuaItemRecord, out var record) ||
            !_lua.TryGetString(record, Offsets.LuaItemId, out var id) || id.Length == 0 ||
            !TryGetInt(record, Offsets.LuaItemBaseId, out var baseId) ||
            !TryGetInt(record, Offsets.LuaItemLevel, out var level) ||
            !TryGetInt(record, Offsets.LuaItemQuality, out var quality) ||
            !TryGetInt(record, Offsets.LuaItemRareValue, out var rareValue) ||
            !TryGetInt(record, Offsets.LuaItemCount, out var count) || count < 1 ||
            !TryGetInt(record, Offsets.LuaItemStackMax, out var stackMax) ||
            !TryGetInt(record, Offsets.LuaItemType, out var type))
            return false;

        var (prefixes, suffixes) = ReadAffixes(record, entry);
        item = new InventoryItem(bag, (int)slot, id, baseId, level, quality, rareValue, count, stackMax, type) { PrefixIds = prefixes, SuffixIds = suffixes };
        return true;
    }

    private (int[] Prefixes, int[] Suffixes) ReadAffixes(LuaTable record, LuaTable entry)
    {
        LuaTable[] sources = [record, entry];
        foreach (var source in sources)
        {
            var (prefixes, suffixes) = (_lua.AffixIds(source, Offsets.LuaGroundPrefix), _lua.AffixIds(source, Offsets.LuaGroundSuffix));
            if (prefixes.Length + suffixes.Length > 0) return (prefixes, suffixes);
        }

        foreach (var source in sources)
        {
            if (!_lua.TryGetTable(source, Offsets.LuaItemAffixList, out var list)) continue;
            var (prefixes, suffixes) = (_lua.AffixIds(list, Offsets.LuaItemPrefixInfoList), _lua.AffixIds(list, Offsets.LuaItemSuffixInfoList));
            if (prefixes.Length + suffixes.Length > 0) return (prefixes, suffixes);
        }

        return ([], []);
    }

    private bool TryGetInt(LuaTable record, string key, out int value)
    {
        value = 0;
        if (!_lua.TryGetInt(record, key, out var raw) || raw < int.MinValue || raw > int.MaxValue) return false;
        value = (int)raw;
        return true;
    }
}
