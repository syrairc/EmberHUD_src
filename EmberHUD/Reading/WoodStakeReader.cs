using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class WoodStakeReader(IReadBackend memory, nint moduleBase)
{
    public WoodStakeStats? Read()
    {
        if (!memory.TryReadPtr(moduleBase + Offsets.WoodStakeMgrGlobalRva, out var mgr) || mgr == 0 ||
            !memory.TryRead(mgr + Offsets.WoodStakeRunning, out byte running))
            return null;

        var stat = mgr + Offsets.WoodStakeStatistic;
        if (!memory.TryRead(stat + Offsets.WoodStakeStatSeconds, out int seconds) || seconds < 0 || seconds > Offsets.MaxWoodStakeSeconds ||
            !TryReadCount(stat + Offsets.WoodStakeStatAvg, out var avg) ||
            !TryReadCount(stat + Offsets.WoodStakeStatCur, out var cur) ||
            !TryReadCount(stat + Offsets.WoodStakeStatMax, out var max) ||
            !TryReadCount(stat + Offsets.WoodStakeStatCurUnlimited, out var curUnlimited) ||
            !TryReadCount(stat + Offsets.WoodStakeStatMaxUnlimited, out var maxUnlimited) ||
            !TryReadBigNumber(mgr + Offsets.WoodStakeUnlimitedTotal, out var total))
            return null;

        return new WoodStakeStats(running != 0, seconds, avg, cur, max, curUnlimited, maxUnlimited, total / Math.Max(seconds, 1));
    }

    private bool TryReadCount(nint at, out long value) => memory.TryRead(at, out value) && value >= 0;

    private bool TryReadBigNumber(nint at, out long value)
    {
        value = 0;
        if (!memory.TryReadArray(at, Offsets.BigNumberLimbs, out ulong[] limbs) || !memory.TryRead(at + Offsets.BigNumberCount, out int count))
            return false;

        if (count <= 0) return true;
        if (count > 2)
        {
            value = long.MaxValue;
            return true;
        }

        var scaled = ((UInt128)limbs[0] | (count == 2 ? (UInt128)limbs[1] << 64 : 0)) / (UInt128)Offsets.BigNumberScale;
        value = scaled > long.MaxValue ? long.MaxValue : (long)scaled;
        return true;
    }
}
