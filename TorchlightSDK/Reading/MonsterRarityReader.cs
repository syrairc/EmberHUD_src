using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class MonsterRarityReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<uint, Rarity> _found = [];

    public Rarity Read(uint configId)
    {
        if (_found.TryGetValue(configId, out var cached))
            return cached;
        if (!TryReadTier(configId, out var tier))
            return Rarity.Normal;

        var rarity = tier switch
        {
            2 => Rarity.Rare,
            4 => Rarity.Boss,
            _ => Rarity.Normal,
        };
        _found[configId] = rarity;
        return rarity;
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
