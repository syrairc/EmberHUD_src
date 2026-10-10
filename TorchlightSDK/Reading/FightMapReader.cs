using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class FightMapReader(IReadBackend memory)
{
    public (int Key, nint Value)[] Read(nint map)
    {
        if (!memory.TryRead(map + Offsets.TMapNum, out int num) || num <= 0 || num > Offsets.MaxMapElements ||
            !TryReadAllocationBits(map, num, out var bits) ||
            !memory.TryReadPtr(map, out var data) ||
            !memory.TryReadArray(data, num * Offsets.TMapElementStride, out byte[] elements))
            return [];

        var live = new List<(int, nint)>();
        for (var i = 0; i < num; i++)
        {
            if ((bits[i >> 5] >> (i & 31) & 1) == 0) continue;
            var value = (nint)BitConverter.ToInt64(elements, i * Offsets.TMapElementStride + Offsets.TMapElementValue);
            if (value != 0)
                live.Add((BitConverter.ToInt32(elements, i * Offsets.TMapElementStride), value));
        }

        return [.. live];
    }

    private bool TryReadAllocationBits(nint map, int num, out uint[] bits)
    {
        bits = [];
        if (!memory.TryRead(map + Offsets.TMapSecondaryBits, out nint secondary))
            return false;
        if (secondary != 0)
            return memory.TryReadArray(secondary, (num + 31) / 32, out bits);

        return num <= Offsets.TMapInlineBitCount &&
               memory.TryReadArray(map + Offsets.TMapInlineBits, Offsets.TMapInlineBitCount / 32, out bits);
    }
}
