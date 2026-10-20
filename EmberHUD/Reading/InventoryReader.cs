using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class InventoryReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private InventoryItem[]? _items;
    public int[]? Capacities { get; private set; }
    private static readonly HashSet<int> StashGearTypes = [.. Offsets.StashGearTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)];
    private long _stashNextRefresh;
    private int _stashTab = -1;
    private InventoryItem[]? _stashItems;
    private long _tradeNextRefresh;
    private TradeGood[]? _tradeGoods;
    private readonly Dictionary<nint, TradeGood> _tradeCache = [];

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

    public TradeGood[]? ReadTradeGoods()
    {
        var now = _clock();
        if (now < _tradeNextRefresh) return _tradeGoods;
        _tradeNextRefresh = now + Stopwatch.Frequency * Offsets.TradeGoodsRefreshMilliseconds / 1000;
        return _tradeGoods = BuildTradeGoods();
    }

    private TradeGood[]? BuildTradeGoods()
    {
        if (!_lua.TryOpen(out var globals) || !_lua.TryGetTable(globals, Offsets.LuaAuctionManager, out var manager))
            return null;

        var hasSimple = _lua.TryGetTable(manager, Offsets.LuaAuctionListings, out var simple);
        var hasAdvanced = _lua.TryGetTable(manager, Offsets.LuaAuctionAdvancedListings, out var advanced);
        if (!hasSimple && !hasAdvanced) return null;

        var goods = new List<TradeGood>();
        var seen = new HashSet<nint>();
        var goodIds = new HashSet<long>();
        foreach (var (store, isAdvanced) in new[] { (simple, false), (advanced, true) })
        {
            if (store.Address == 0) continue;
            foreach (var (key, entry) in _lua.IntKeyedTables(store).OrderBy(l => l.Key))
            {
                if (goods.Count >= Offsets.MaxTradeGoods) break;
                seen.Add(entry.Address);
                var good = _tradeCache.TryGetValue(entry.Address, out var cached) ? Refresh(entry, cached) : ReadTradeGood(key, entry, isAdvanced);
                if (good is null) continue;
                _tradeCache[entry.Address] = good;
                if (goodIds.Add(good.GoodId)) goods.Add(good);
            }
        }

        foreach (var address in _tradeCache.Keys.Where(a => !seen.Contains(a)).ToArray()) _tradeCache.Remove(address);
        return [.. goods];
    }

    private TradeGood? Refresh(LuaTable entry, TradeGood cached)
    {
        if (!_lua.TryGetTable(entry, Offsets.LuaItemRecord, out var record) ||
            !_lua.TryGetInt(record, Offsets.LuaAuctionGoodId, out var goodId) ||
            !_lua.TryGetInt(record, Offsets.LuaAuctionPrice, out var price) ||
            !_lua.TryGetString(record, Offsets.LuaAuctionStatus, out var status))
            return cached;
        if (goodId != cached.GoodId) return null;
        return price == cached.Price && status == cached.Status ? cached : cached with { Price = price, Status = status };
    }

    private TradeGood? ReadTradeGood(long key, LuaTable entry, bool advanced)
    {
        if (!_lua.TryGetTable(entry, Offsets.LuaItemRecord, out var record) ||
            !_lua.TryGetInt(record, Offsets.LuaAuctionGoodId, out var goodId) ||
            !TryGetInt(record, Offsets.LuaAuctionPriceType, out var priceType) ||
            !_lua.TryGetInt(record, Offsets.LuaAuctionPrice, out var price) ||
            !_lua.TryGetString(record, Offsets.LuaAuctionStatus, out var status) ||
            !_lua.TryGetTable(record, Offsets.LuaAuctionItem, out var itemData) ||
            !TryReadItem(InventoryBag.Commodities, key, itemData, out var item))
            return null;

        var good = new TradeGood(goodId, priceType, price, status, StashGearTypes.Contains(item.Type) ? item with { Bag = InventoryBag.Equipment } : item) { Advanced = advanced, ItemLevel = item.Level };
        return _lua.TryGetTable(itemData, Offsets.LuaItemRecord, out var raw) ? AddDetails(good, raw) : good;
    }

    private TradeGood AddDetails(TradeGood good, LuaTable raw)
    {
        var affixes = new List<TradeAffix>();
        if (_lua.TryGetTable(raw, Offsets.LuaItemAffixList, out var list))
        {
            affixes.AddRange(ReadTradeAffixes(list, Offsets.LuaItemBaseAffixList, TradeAffixKind.Implicit));
            affixes.AddRange(ReadTradeAffixes(list, Offsets.LuaItemPrefixInfoList, TradeAffixKind.Prefix));
            affixes.AddRange(ReadTradeAffixes(list, Offsets.LuaItemSuffixInfoList, TradeAffixKind.Suffix));
            affixes.AddRange(ReadTradeAffixes(list, Offsets.LuaItemBaseAttrInfoList, TradeAffixKind.BaseAttribute));
        }

        var flags = _lua.TryGetTable(raw, Offsets.LuaItemFlags, out var status)
            ? _lua.Entries(status, Offsets.MaxTradeFlags, 0, "", out _).Where(e => e.Type == "bool" && e.Value == "true").Select(e => e.Key).ToArray()
            : [];
        var (skillId, skillType, holeNum) = (0, 0, 0);
        if (_lua.TryGetTable(raw, Offsets.LuaItemSpecialInfo, out var special))
            (skillId, skillType, holeNum) = (Int(special, Offsets.LuaSpecialSkillId), Int(special, Offsets.LuaSpecialSkillType), Int(special, Offsets.LuaSpecialHoleNum));

        var itemLevel = good.ItemLevel;
        if (good.Item.Type == Offsets.TradeSkillItemType && _lua.TryGetTable(raw, Offsets.LuaItemServerData, out var server) &&
            TryGetInt(server, Offsets.LuaServerSkillItemLevel, out var skillItemLevel))
            itemLevel = skillItemLevel;

        return good with
        {
            Affixes = [.. affixes],
            Flags = flags,
            ItemLevel = itemLevel,
            SpecialId = Int(raw, Offsets.LuaItemSpecialId),
            SkillId = skillId,
            SkillType = skillType,
            HoleNum = holeNum,
            EnhanceCount = Int(raw, Offsets.LuaItemEnhanceCount),
            Forged = _lua.TryGetBool(raw, Offsets.LuaItemForged, out var forged) && forged,
            Corrode = _lua.TryGetBool(raw, Offsets.LuaItemCorrode, out var corrode) && corrode,
            Unidentified = _lua.TryGetBool(raw, Offsets.LuaItemEvaluated, out var evaluated) && !evaluated,
        };
    }

    private IEnumerable<TradeAffix> ReadTradeAffixes(LuaTable list, string name, TradeAffixKind kind)
    {
        if (!_lua.TryGetTable(list, name, out var table)) yield break;
        foreach (var (_, row) in _lua.IntKeyedTables(table).OrderBy(r => r.Key).Take(Offsets.MaxTradeAffixes))
        {
            if (!TryGetInt(row, Offsets.LuaGroundAffixId, out var id)) continue;
            var rolls = new List<float>();
            if (_lua.TryGetTable(row, Offsets.LuaAffixRolls, out var args))
                for (var i = 1; i <= Offsets.MaxTradeAffixes && _lua.TryGetFloatAt(args, i, out var roll); i++) rolls.Add((float)roll);
            yield return new TradeAffix(kind, id, Int(row, Offsets.LuaAffixTier), [.. rolls], _lua.TryGetStringCut(row, Offsets.LuaAffixComplex, out var text) ? text : "");
        }
    }

    private int Int(LuaTable table, string key) => TryGetInt(table, key, out var value) ? value : 0;

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

        Capacities = ReadCapacities(globals);
        return [.. items];
    }

    private int[]? ReadCapacities(LuaTable globals)
    {
        if (!_lua.TryGetTable(globals, Offsets.LuaBagManager, out var manager) ||
            !_lua.TryGetTable(manager, Offsets.LuaBagCapacity, out var table))
            return null;

        var sizes = new int[Offsets.InventoryTabCount];
        for (var bag = 0; bag < sizes.Length; bag++)
        {
            if (!_lua.TryGetIntAt(table, Offsets.InventoryTabFirst + bag, out var size) || size < 0 || size > Offsets.MaxInventoryItems) return null;
            sizes[bag] = (int)size;
        }

        return sizes;
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
        item = new InventoryItem(bag, (int)slot, id, baseId, level, quality, rareValue, count, stackMax, type) { PrefixIds = prefixes, SuffixIds = suffixes, Legendary = TryGetInt(record, Offsets.LuaItemSpecialId, out var specialId) && specialId != 0 };
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
