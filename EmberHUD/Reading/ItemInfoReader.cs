using EmberHUD.Host.Memory;

namespace EmberHUD.Host;

internal sealed class ItemInfoReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<int, ItemInfo> _bases = [];
    private readonly Dictionary<int, ItemInfo> _uniques = [];

    private const int Shared = -1;
    private const int MaxItemRows = 1_000_000;
    private readonly Dictionary<string, int> _byIcon = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (int UniqueId, int BaseId)> _byUniqueIcon = new(StringComparer.OrdinalIgnoreCase);

    public int? ByIcon(string icon)
    {
        if (_byIcon.Count == 0) BuildIconIndex(_byIcon, moduleBase + Offsets.ItemBaseBucketCountRva, moduleBase + Offsets.ItemBaseBucketsRva, Offsets.ItemRowIcon, false, (_, id) => id, Shared);
        return _byIcon.TryGetValue(icon, out var baseId) && baseId != Shared ? baseId : null;
    }

    public (int UniqueId, int BaseId)? ByUniqueIcon(string icon)
    {
        if (_byUniqueIcon.Count == 0)
            BuildIconIndex(_byUniqueIcon, moduleBase + Offsets.ItemGoldBucketCountRva, moduleBase + Offsets.ItemGoldBucketsRva, Offsets.ItemGoldRowIcon, true,
                (row, id) => memory.TryRead(row + Offsets.ItemGoldBaseId, out int baseId) && baseId > 0 ? (id, baseId) : default, (Shared, Shared));
        return _byUniqueIcon.TryGetValue(StripSizeSuffix(icon), out var pair) && pair.UniqueId != Shared ? pair : null;
    }

    private static string StripSizeSuffix(string icon)
    {
        var cut = icon.LastIndexOf('_');
        return cut > 0 && cut < icon.Length - 1 && icon.AsSpan(cut + 1).IndexOfAnyExceptInRange('0', '9') < 0 ? icon[..cut] : icon;
    }

    private void BuildIconIndex<T>(Dictionary<string, T> index, nint countAt, nint bucketsAt, int iconOffset, bool ignoreSizeSuffix, Func<nint, int, T> value, T shared) where T : struct
    {
        var built = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        var complete = memory.TryWalkBuckets(countAt, bucketsAt, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, MaxItemRows, (_, node) =>
        {
            var row = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
            if (row != 0 && memory.TryRead(row + Offsets.ItemRowId, out int id) && id > 0 &&
                memory.TryReadIconName(row + iconOffset, out var icon) && icon is not null && value(row, id) is var v && !v.Equals(default(T)))
            {
                var key = ignoreSizeSuffix ? StripSizeSuffix(icon) : icon;
                built[key] = built.ContainsKey(key) ? shared : v;
            }
            return true;
        });
        if (!complete) return;
        foreach (var (key, v) in built) index[key] = v;
    }

    public ItemInfo? ByBase(int baseId)
    {
        if (_bases.TryGetValue(baseId, out var cached)) return cached;
        if (ReadBase(baseId, out var iconRead) is not { } info) return null;
        return iconRead ? _bases[baseId] = info : info;
    }

    public ItemInfo? ByUnique(int uniqueId)
    {
        if (_uniques.TryGetValue(uniqueId, out var cached)) return cached;
        if (!TryReadUniqueBase(uniqueId, out var baseId, out var uniqueIcon, out var uniqueIconRead) || ReadBase(baseId, out var baseIconRead) is not { } info) return null;
        var unique = info with { IsUnique = true, Icon = uniqueIcon ?? info.Icon };
        return uniqueIconRead && (uniqueIcon is not null || baseIconRead) ? _uniques[uniqueId] = unique : unique;
    }

    public bool IsUnique(int uniqueId) => _uniques.ContainsKey(uniqueId) || TryReadUniqueBase(uniqueId, out _, out _, out _);

    private ItemInfo? ReadBase(int baseId, out bool iconRead)
    {
        iconRead = false;
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

        iconRead = memory.TryReadIconName(row + Offsets.ItemRowIcon, out var icon);
        return new ItemInfo(baseId, rarity, type1, type2, type3, type4, tier, requireLevel, false, icon);
    }

    private bool TryReadUniqueBase(int uniqueId, out int baseId, out string? icon, out bool iconRead)
    {
        baseId = 0;
        icon = null;
        iconRead = false;
        if (uniqueId <= 0 ||
            !memory.TryFindHashValue(moduleBase + Offsets.ItemGoldBucketCountRva, moduleBase + Offsets.ItemGoldBucketsRva, (uint)uniqueId, out var row) ||
            !memory.TryRead(row + Offsets.ItemRowId, out int id) || id != uniqueId ||
            !memory.TryRead(row + Offsets.ItemGoldBaseId, out baseId) || baseId <= 0)
            return false;
        iconRead = memory.TryReadIconName(row + Offsets.ItemGoldRowIcon, out icon);
        return true;
    }
}
