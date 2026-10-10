using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class FightMapReader(IReadBackend memory)
{
    public (int Key, nint Value)[] Read(nint map)
    {
        if (!memory.TryRead(map + Offsets.TMapNum, out int num) || num <= 0 || num > Offsets.MaxMapElements ||
            !memory.TryReadPtr(map, out var data) ||
            !memory.TryReadArray(data, num * Offsets.TMapElementStride, out byte[] elements) ||
            !TryReadAllocationBits(map, num, out var bits))
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
        if (num <= Offsets.TMapInlineBitCount)
            return memory.TryReadArray(map + Offsets.TMapInlineBits, Offsets.TMapInlineBitCount / 32, out bits);

        bits = [];
        return memory.TryReadPtr(map + Offsets.TMapSecondaryBits, out var secondary) &&
               memory.TryReadArray(secondary, (num + 31) / 32, out bits);
    }
}
