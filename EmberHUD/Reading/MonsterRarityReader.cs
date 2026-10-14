using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class MonsterRarityReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<uint, uint> _tiers = [];

    public Rarity Resolve(uint tier, nint roleBlock)
    {
        var role = 0;
        if (roleBlock != 0)
            memory.TryRead(roleBlock + Offsets.RoleRarity, out role);
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

    public bool TryGetTier(uint configId, out uint tier)
    {
        if (_tiers.TryGetValue(configId, out tier))
            return true;
        if (!TryReadTier(configId, out tier))
            return false;

        _tiers[configId] = tier;
        return true;
    }

    private bool TryReadTier(uint configId, out uint tier)
    {
        tier = 0;
        return memory.TryFindHashValue(moduleBase + Offsets.MonsterCfgBucketCountRva, moduleBase + Offsets.MonsterCfgBucketsRva, configId, out var row) && memory.TryRead(row + Offsets.MonsterCfgRarity, out tier);
    }
}
