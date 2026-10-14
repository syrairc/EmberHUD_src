using System.Runtime.InteropServices;

namespace EmberHUD.Host.Memory;

internal static class ReadExtensions
{
    public static unsafe bool TryRead<T>(this IReadBackend backend, nint address, out T value) where T : unmanaged
    {
        value = default;
        fixed (T* p = &value)
        {
            return backend.TryRead(address, new Span<byte>(p, sizeof(T)));
        }
    }

    public static bool TryReadPtr(this IReadBackend backend, nint address, out nint value) =>
        backend.TryRead(address, out value) && value != 0;

    public static bool TryReadArray<T>(this IReadBackend backend, nint address, int count, out T[] values) where T : unmanaged
    {
        values = [];
        if (count < 0) return false;
        values = new T[count];
        return count == 0 || backend.TryRead(address, MemoryMarshal.AsBytes(values.AsSpan()));
    }

    public static bool TryReadTMapElements(this IReadBackend backend, nint map, int stride, int max, out nint data, out byte[] elements, out uint[] bits)
    {
        data = 0;
        elements = [];
        bits = [];
        return backend.TryRead(map + Offsets.TArrayNum, out int num) && num > 0 && num <= max &&
               backend.TryReadTMapAllocationBits(map, num, out bits) &&
               backend.TryReadPtr(map, out data) &&
               backend.TryReadArray(data, num * stride, out elements);
    }

    public static bool IsAllocated(uint[] bits, int i) => (bits[i >> 5] >> (i & 31) & 1) != 0;

    public static (int Key, nint Value)[] ReadTMapPointers(this IReadBackend backend, nint map)
    {
        if (!backend.TryReadTMapElements(map, Offsets.TMapElementStride, Offsets.MaxMapElements, out _, out var elements, out var bits))
            return [];

        var live = new List<(int, nint)>();
        for (var i = 0; i < elements.Length / Offsets.TMapElementStride; i++)
        {
            var at = i * Offsets.TMapElementStride;
            var value = (nint)BitConverter.ToInt64(elements, at + Offsets.TMapElementValue);
            if (IsAllocated(bits, i) && value != 0)
                live.Add((BitConverter.ToInt32(elements, at), value));
        }

        return [.. live];
    }

    public static bool TryReadBuckets(this IReadBackend backend, nint countAt, nint bucketsAt, int max, out nint[] buckets)
    {
        buckets = [];
        return backend.TryReadBucketTable(countAt, bucketsAt, max, out var count, out var array) &&
               backend.TryReadArray(array, (int)count, out buckets);
    }

    public static bool TryFindHashNode(this IReadBackend backend, nint countAt, nint bucketsAt, int max, ulong hash, int nextOffset, Func<nint, bool> isMatch, out nint node)
    {
        node = 0;
        if (!backend.TryReadBucketTable(countAt, bucketsAt, max, out var count, out var array) ||
            !backend.TryRead(array + (nint)(hash % count) * nint.Size, out nint at))
            return false;

        for (var hops = 0; hops < Offsets.MaxChain && at != 0; hops++)
        {
            if (isMatch(at))
            {
                node = at;
                return true;
            }
            if (!backend.TryRead(at + nextOffset, out at))
                return false;
        }

        return false;
    }

    public static bool TryFindHashValue(this IReadBackend backend, nint countAt, nint bucketsAt, uint key, out nint value)
    {
        value = 0;
        return backend.TryFindHashNode(countAt, bucketsAt, Offsets.MaxBuckets, key, Offsets.NodeNext,
                   candidate => backend.TryRead(candidate + Offsets.NodeKey, out uint found) && found == key, out var node) &&
               backend.TryReadPtr(node + Offsets.NodeEntity, out value);
    }

    public static bool TryReadGObject(this IReadBackend backend, nint moduleBase, uint index, out nint obj, out int serial)
    {
        obj = 0;
        serial = 0;
        if (index / Offsets.GObjectsPerChunk >= Offsets.MaxGObjectChunks ||
            !backend.TryReadPtr(moduleBase + Offsets.GObjectsGlobalRva, out var chunks) ||
            !backend.TryReadPtr(chunks + (nint)(index / Offsets.GObjectsPerChunk) * nint.Size, out var chunk) ||
            !backend.TryReadArray(chunk + (nint)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize, Offsets.GObjectsItemSize, out byte[] item))
            return false;

        obj = (nint)BitConverter.ToInt64(item, Offsets.GObjectsItemObject);
        serial = BitConverter.ToInt32(item, Offsets.GObjectsItemSerial);
        return obj != 0;
    }

    private static bool TryReadBucketTable(this IReadBackend backend, nint countAt, nint bucketsAt, int max, out uint count, out nint buckets)
    {
        buckets = 0;
        return backend.TryRead(countAt, out count) && count != 0 && count <= max &&
               backend.TryReadPtr(bucketsAt, out buckets);
    }

    private static bool TryReadTMapAllocationBits(this IReadBackend backend, nint map, int num, out uint[] bits)
    {
        bits = [];
        if (!backend.TryRead(map + Offsets.TMapSecondaryBits, out nint secondary))
            return false;
        if (secondary != 0)
            return backend.TryReadArray(secondary, (num + 31) / 32, out bits);

        return num <= Offsets.TMapInlineBitCount &&
               backend.TryReadArray(map + Offsets.TMapInlineBits, Offsets.TMapInlineBitCount / 32, out bits);
    }
}
