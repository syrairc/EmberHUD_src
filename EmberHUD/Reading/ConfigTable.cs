using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal static class ConfigTable
{
    public static bool TryFindRow(IReadBackend memory, nint moduleBase, int bucketsRva, int bucketCountRva, uint key, out nint row)
    {
        row = 0;
        if (!memory.TryRead(moduleBase + bucketCountRva, out uint bucketCount) ||
            bucketCount == 0 || bucketCount > Offsets.MaxBuckets ||
            !memory.TryReadPtr(moduleBase + bucketsRva, out var buckets) ||
            !memory.TryReadPtr(buckets + (nint)(key % bucketCount) * nint.Size, out var node))
            return false;

        for (var length = 0; length < Offsets.MaxChain && node != 0; length++)
        {
            if (!memory.TryRead(node + Offsets.NodeKey, out uint found))
                return false;
            if (found == key)
                return memory.TryReadPtr(node + Offsets.NodeEntity, out row) && row != 0;
            if (!memory.TryReadPtr(node + Offsets.NodeNext, out node))
                return false;
        }

        return false;
    }
}
