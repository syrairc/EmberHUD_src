using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class FightMapReader(IReadBackend memory)
{
    public (int Key, nint Value)[] Read(nint map)
    {
        if (!memory.TryRead(map + Offsets.TMapNum, out int num) || num <= 0 || num > Offsets.MaxMapElements ||
            !memory.TryReadTMapAllocationBits(map, num, out var bits) ||
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
}
