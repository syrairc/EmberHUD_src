using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal readonly record struct GroundAffixes(int DropId, bool Owned, int[] PrefixIds, int[] SuffixIds, bool Legendary, int BaseId, int Count, int RareValue, bool Equipment);

internal sealed class GroundItemReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? System.Diagnostics.Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private readonly Names _names = new(memory, moduleBase);
    private nint _dropMgr;
    private long _nextScan;
    private long _nextRefresh;
    private GroundAffixes[]? _affixes;

    internal static (Vector2 Min, Vector2 Max) LabelRect(Vector2 screenPos, (Vector2 Min, Vector2 Max) box) =>
        (screenPos + box.Min, screenPos + box.Max);

    internal GroundItem[]? Read()
    {
        var rects = ReadRects();
        if (rects is null)
            return null;

        var now = _clock();
        if (_affixes is null || now >= _nextRefresh)
        {
            _affixes = ReadLua();
            _nextRefresh = now + System.Diagnostics.Stopwatch.Frequency * Offsets.GroundItemsRefreshMilliseconds / 1000;
        }

        List<GroundItem> items = [];
        foreach (var affixes in _affixes)
        {
            if (rects.TryGetValue(affixes.DropId, out var rect))
                items.Add(new GroundItem(affixes.DropId, rect.Min, rect.Max, rect.Position, affixes.Owned, affixes.PrefixIds, affixes.SuffixIds) { Legendary = affixes.Legendary, BaseId = affixes.BaseId, Count = affixes.Count, RareValue = affixes.RareValue, Equipment = affixes.Equipment });
        }

        return [.. items];
    }

    private Dictionary<int, (Vector2 Min, Vector2 Max, Vector3 Position)>? ReadRects()
    {
        if (!TryFindDropMgr(out var dropMgr))
            return null;

        var map = dropMgr + Offsets.DropMgrNameTagGroups;
        if (memory.TryRead(map + Offsets.TArrayNum, out int num) && num == 0)
            return [];
        if (!memory.TryReadTMapElements(map, Offsets.NameTagGroupStride, Offsets.MaxNameTagGroups, out var data, out var groups, out var groupBits))
            return null;

        Dictionary<int, (Vector2, Vector2, Vector3)> rects = [];
        for (var i = 0; i < groups.Length / Offsets.NameTagGroupStride; i++)
        {
            var value = i * Offsets.NameTagGroupStride + Offsets.NameTagGroupValue;
            var screenPos = new Vector2(BitConverter.ToSingle(groups, value + Offsets.NameTagGroupScreenPos), BitConverter.ToSingle(groups, value + Offsets.NameTagGroupScreenPos + sizeof(float)));
            var position = new Vector3(BitConverter.ToSingle(groups, value + Offsets.NameTagGroupWorldPos), BitConverter.ToSingle(groups, value + Offsets.NameTagGroupWorldPos + sizeof(float)), BitConverter.ToSingle(groups, value + Offsets.NameTagGroupWorldPos + 2 * sizeof(float)));
            if (!ReadExtensions.IsAllocated(groupBits, i) || groups[value + Offsets.NameTagGroupInScreen] == 0 || !IsFinite(screenPos) || !IsFinite(position))
                continue;

            if (memory.TryReadTMapElements(data + value + Offsets.NameTagGroupScreenRects, Offsets.ScreenRectStride, Offsets.MaxScreenRects, out _, out var labels, out var labelBits))
                AddRects(rects, labels, labelBits, screenPos, position);
        }

        return rects;
    }

    private static void AddRects(Dictionary<int, (Vector2, Vector2, Vector3)> into, byte[] labels, uint[] labelBits, Vector2 screenPos, Vector3 position)
    {
        for (var i = 0; i < labels.Length / Offsets.ScreenRectStride; i++)
        {
            var at = i * Offsets.ScreenRectStride;
            var box = at + Offsets.ScreenRectBox;
            var min = new Vector2(BitConverter.ToSingle(labels, box), BitConverter.ToSingle(labels, box + sizeof(float)));
            var max = new Vector2(BitConverter.ToSingle(labels, box + 2 * sizeof(float)), BitConverter.ToSingle(labels, box + 3 * sizeof(float)));
            if (!ReadExtensions.IsAllocated(labelBits, i) || labels[box + 4 * sizeof(float)] == 0 || !IsFinite(min) || !IsFinite(max))
                continue;

            var (labelMin, labelMax) = LabelRect(screenPos, (min, max));
            var size = labelMax - labelMin;
            if (size.X > 0 && size.Y > 0 && size.X <= Offsets.MaxLabelPixels && size.Y <= Offsets.MaxLabelPixels)
                into.TryAdd(BitConverter.ToInt32(labels, at), (labelMin, labelMax, position));
        }
    }

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private bool TryFindDropMgr(out nint dropMgr)
    {
        dropMgr = _dropMgr;
        if (dropMgr != 0 && IsDropMgr(dropMgr))
            return true;

        var now = _clock();
        if (now < _nextScan)
            return false;

        dropMgr = _dropMgr = Scan();
        if (dropMgr == 0)
            _nextScan = now + System.Diagnostics.Stopwatch.Frequency * Offsets.DropMgrRescanSeconds;
        return dropMgr != 0;
    }

    private nint Scan()
    {
        var table = moduleBase + Offsets.GObjectsGlobalRva;
        if (!memory.TryReadPtr(table, out var chunks) ||
            !memory.TryRead(table + Offsets.GObjectsNumElements, out int count) ||
            count <= 0 || count > Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk)
            return 0;

        for (var chunk = (count - 1) / Offsets.GObjectsPerChunk; chunk >= 0; chunk--)
        {
            var inChunk = Math.Min(Offsets.GObjectsPerChunk, count - chunk * Offsets.GObjectsPerChunk);
            if (!memory.TryReadPtr(chunks + chunk * nint.Size, out var items) ||
                !memory.TryReadArray(items, inChunk * Offsets.GObjectsItemSize, out byte[] bytes))
                continue;

            for (var i = inChunk - 1; i >= 0; i--)
            {
                var obj = (nint)BitConverter.ToInt64(bytes, i * Offsets.GObjectsItemSize + Offsets.GObjectsItemObject);
                if (obj != 0 && IsDropMgr(obj))
                    return obj;
            }
        }

        return 0;
    }

    private bool IsDropMgr(nint obj) =>
        _names.TryReadClassName(obj, out var className) && className == Offsets.DropMgrClassName &&
        memory.TryRead(obj + Offsets.UObjectName, out uint nameIndex) && _names.TryRead(nameIndex, out var name) &&
        !name.StartsWith(Offsets.DefaultObjectPrefix, StringComparison.Ordinal);

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
        var legendary = false;
        var baseId = 0;
        var count = 1;
        var rareValue = 0;
        var equipment = false;
        if (_lua.TryGetTable(entry, Offsets.LuaGroundDropItem, out var drop))
        {
            if (_lua.TryGetInt(drop, Offsets.LuaGroundDropId, out var id) && id is >= int.MinValue and <= int.MaxValue) dropId = (int)id;
            if (_lua.TryGetTable(drop, Offsets.LuaGroundItem, out var item))
            {
                prefixes = _lua.AffixIds(item, Offsets.LuaGroundPrefix);
                suffixes = _lua.AffixIds(item, Offsets.LuaGroundSuffix);
                legendary = _lua.TryGetInt(item, Offsets.LuaGroundSpecialId, out var specialId) && specialId != 0 ||
                    _lua.TryGetString(item, Offsets.LuaGroundUniqueId, out var uniqueId) && uniqueId.Length > 0 && uniqueId != "0";
                if (_lua.TryGetString(item, Offsets.LuaGroundBaseId, out var baseText)) int.TryParse(baseText, out baseId);
                if (_lua.TryGetInt(item, Offsets.LuaGroundCount, out var num) && num is >= 1 and <= int.MaxValue) count = (int)num;
                if (_lua.TryGetInt(item, Offsets.LuaGroundRareValue, out var rare) && rare is >= int.MinValue and <= int.MaxValue) rareValue = (int)rare;
                equipment = _lua.TryGetInt(item, Offsets.LuaGroundLevel, out var level) && level > Offsets.GroundCommodityMaxLevel;
            }
        }

        return new GroundAffixes(dropId, owned, prefixes, suffixes, legendary, baseId, count, rareValue, equipment);
    }
}
