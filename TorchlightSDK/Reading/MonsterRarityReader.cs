using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class MonsterRarityReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<uint, uint> _tiers = [];

    public Rarity Read(uint configId, nint entity)
    {
        var tier = ReadTier(configId);
        var role = memory.TryReadPtr(entity + Offsets.EntityRole, out var block) && memory.TryRead(block + Offsets.RoleRarity, out int value) ? value : 0;
        return (tier, role) switch
        {
            (4 or 5, _) => Rarity.Boss,
            (_, 1) => Rarity.Normal,
            (_, 2) => Rarity.Magic,
            (_, 3) => Rarity.Rare,
            (_, >= 4) => Rarity.Boss,
            (2 or 3, _) => Rarity.Rare,
            _ => Rarity.Normal,
        };
    }

    private uint ReadTier(uint configId)
    {
        if (_tiers.TryGetValue(configId, out var cached))
            return cached;
        if (!TryReadTier(configId, out var tier))
            return 0;

        _tiers[configId] = tier;
        return tier;
    }

    private bool TryReadTier(uint configId, out uint tier)
    {
        tier = 0;
        if (!memory.TryRead(moduleBase + Offsets.MonsterCfgBucketCountRva, out uint bucketCount) ||
            bucketCount == 0 || bucketCount > Offsets.MaxBuckets ||
            !memory.TryReadPtr(moduleBase + Offsets.MonsterCfgBucketsRva, out var buckets) ||
            !memory.TryReadPtr(buckets + (nint)(configId % bucketCount) * nint.Size, out var node))
            return false;

        for (var length = 0; length < Offsets.MaxChain; length++)
        {
            if (!memory.TryRead(node + Offsets.NodeKey, out uint key))
                return false;
            if (key == configId)
                return memory.TryReadPtr(node + Offsets.NodeEntity, out var row) && memory.TryRead(row + Offsets.MonsterCfgRarity, out tier);
            if (!memory.TryReadPtr(node + Offsets.NodeNext, out node))
                return false;
        }

        return false;
    }
}
