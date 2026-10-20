using EmberHUD.Host.Memory;

namespace EmberHUD.Host;

internal sealed class ItemInfoReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<int, ItemInfo> _bases = [];
    private readonly Dictionary<int, ItemInfo> _uniques = [];

    public ItemInfo? ByBase(int baseId)
    {
        if (_bases.TryGetValue(baseId, out var cached)) return cached;
        if (ReadBase(baseId) is not { } info) return null;
        return _bases[baseId] = info;
    }

    public ItemInfo? ByUnique(int uniqueId)
    {
        if (_uniques.TryGetValue(uniqueId, out var cached)) return cached;
        if (!TryReadUniqueBase(uniqueId, out var baseId) || ReadBase(baseId) is not { } info) return null;
        return _uniques[uniqueId] = info with { IsUnique = true };
    }

    public bool IsUnique(int uniqueId) => _uniques.ContainsKey(uniqueId) || TryReadUniqueBase(uniqueId, out _);

    private ItemInfo? ReadBase(int baseId)
    {
        if (baseId <= 0 ||
            !memory.TryFindHashValue(moduleBase + Offsets.ItemBaseBucketCountRva, moduleBase + Offsets.ItemBaseBucketsRva, (uint)baseId, out var row) ||
            !memory.TryRead(row + Offsets.ItemRowId, out int id) || id != baseId ||
            !memory.TryRead(row + Offsets.ItemRowRarity, out int rarity) ||
            !memory.TryRead(row + Offsets.ItemRowType1, out int type1) ||
            !memory.TryRead(row + Offsets.ItemRowType2, out int type2) ||
            !memory.TryRead(row + Offsets.ItemRowType3, out int type3) ||
            !memory.TryRead(row + Offsets.ItemRowType4, out int type4) ||
            !memory.TryRead(row + Offsets.ItemRowTier, out int tier) ||
            !memory.TryRead(row + Offsets.ItemRowRequireLevel, out int requireLevel))
            return null;

        return new ItemInfo(baseId, rarity, type1, type2, type3, type4, tier, requireLevel, false);
    }

    private bool TryReadUniqueBase(int uniqueId, out int baseId)
    {
        baseId = 0;
        return uniqueId > 0 &&
               memory.TryFindHashValue(moduleBase + Offsets.ItemGoldBucketCountRva, moduleBase + Offsets.ItemGoldBucketsRva, (uint)uniqueId, out var row) &&
               memory.TryRead(row + Offsets.ItemRowId, out int id) && id == uniqueId &&
               memory.TryRead(row + Offsets.ItemGoldBaseId, out baseId) && baseId > 0;
    }
}
