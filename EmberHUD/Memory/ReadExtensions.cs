using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace EmberHUD.Host.Memory;

internal interface IReadBackend
{
    bool TryRead(nint address, Span<byte> target);
}

internal delegate bool NodeVisitor(nint node, ReadOnlySpan<byte> bytes);

internal enum HashFind { Found, Absent, Unreadable }

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

    public static bool TryRead<T>(this IReadBackend backend, nint address, Span<T> values) where T : unmanaged =>
        backend.TryRead(address, MemoryMarshal.AsBytes(values));

    public static bool TryReadPtr(this IReadBackend backend, nint address, out nint value) =>
        backend.TryRead(address, out value) && value != 0;

    public static bool TryReadConfigString(this IReadBackend backend, nint at, out string text)
    {
        text = "";
        if (!backend.TryReadPtr(at + Offsets.ConfigStringData, out var data) ||
            !backend.TryRead(at + Offsets.ConfigStringLength, out long length) ||
            length <= 0 || length > Offsets.MaxIconNameLength ||
            !backend.TryReadArray(data, (int)length, out byte[] bytes) ||
            Array.Exists(bytes, b => b < 0x20 || b > 0x7E))
            return false;
        text = Encoding.ASCII.GetString(bytes);
        return true;
    }

    public static bool TryReadIconName(this IReadBackend backend, nint at, out string? name)
    {
        name = null;
        if (!backend.TryRead(at + Offsets.ConfigStringLength, out long length)) return false;
        if (length == 0) return true;
        if (!backend.TryReadConfigString(at, out var text)) return false;
        name = text == "-1" ? null : text;
        return true;
    }

    public static string? ReadIconName(this IReadBackend backend, nint at) =>
        backend.TryReadIconName(at, out var name) ? name : null;

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
        Span<byte> header = stackalloc byte[Offsets.TMapHeaderSize];
        if (!backend.TryRead(map, header))
            return false;

        var num = BitConverter.ToInt32(header[Offsets.TArrayNum..]);
        data = (nint)BitConverter.ToInt64(header);
        return num > 0 && num <= max && backend.TryReadTMapAllocationBits(header, num, out bits) && data != 0 &&
               backend.TryReadArray(data, num * stride, out elements);
    }

    public static bool IsAllocated(uint[] bits, int i) => (bits[i >> 5] >> (i & 31) & 1) != 0;

    public static void ReadTMapPointers(this IReadBackend backend, nint map, List<(int Key, nint Value)> into)
    {
        if (!backend.TryReadTMapElements(map, Offsets.TMapElementStride, Offsets.MaxMapElements, out _, out var elements, out var bits))
            return;

        for (var i = 0; i < elements.Length / Offsets.TMapElementStride; i++)
        {
            var at = i * Offsets.TMapElementStride;
            var value = (nint)BitConverter.ToInt64(elements, at + Offsets.TMapElementValue);
            if (IsAllocated(bits, i) && value != 0)
                into.Add((BitConverter.ToInt32(elements, at), value));
        }
    }

    public static (int Key, nint Value)[] ReadTMapPointers(this IReadBackend backend, nint map)
    {
        List<(int Key, nint Value)> live = [];
        backend.ReadTMapPointers(map, live);
        return [.. live];
    }

    public static bool TryWalkBuckets(this IReadBackend backend, nint countAt, nint bucketsAt, int maxBuckets, int nodeSize, int nextOffset, int maxNodes, NodeVisitor visit)
    {
        if (!backend.TryReadBucketTable(countAt, bucketsAt, maxBuckets, out var count, out var array))
            return false;

        var heads = ArrayPool<nint>.Shared.Rent((int)count);
        try
        {
            if (!backend.TryRead(array, heads.AsSpan(0, (int)count)))
                return false;

            Span<byte> node = stackalloc byte[nodeSize];
            var visited = 0;
            for (var bucket = 0; bucket < count; bucket++)
            {
                var chain = 0;
                for (var at = heads[bucket]; at != 0;)
                {
                    if (chain++ >= Offsets.MaxChain || visited >= maxNodes || !backend.TryRead(at, node))
                        return false;

                    visited++;
                    if (!visit(at, node))
                        return true;
                    at = (nint)BitConverter.ToInt64(node[nextOffset..]);
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<nint>.Shared.Return(heads);
        }
    }

    public static bool TryFindHashNode(this IReadBackend backend, nint countAt, nint bucketsAt, int max, ulong hash, int nextOffset, Func<nint, bool> isMatch, out nint node) =>
        backend.FindHashNode(countAt, bucketsAt, max, hash, nextOffset, isMatch, out node) == HashFind.Found;

    public static HashFind FindHashNode(this IReadBackend backend, nint countAt, nint bucketsAt, int max, ulong hash, int nextOffset, Func<nint, bool> isMatch, out nint node)
    {
        node = 0;
        if (!backend.TryReadBucketTable(countAt, bucketsAt, max, out var count, out var array) ||
            !backend.TryRead(array + (nint)(hash % count) * nint.Size, out nint at))
            return HashFind.Unreadable;

        for (var hops = 0; hops < Offsets.MaxChain && at != 0; hops++)
        {
            if (isMatch(at))
            {
                node = at;
                return HashFind.Found;
            }
            if (!backend.TryRead(at + nextOffset, out at))
                return HashFind.Unreadable;
        }

        return at == 0 ? HashFind.Absent : HashFind.Unreadable;
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
        Span<byte> item = stackalloc byte[Offsets.GObjectsItemSize];
        if (index / Offsets.GObjectsPerChunk >= Offsets.MaxGObjectChunks ||
            !backend.TryReadPtr(moduleBase + Offsets.GObjectsGlobalRva, out var chunks) ||
            !backend.TryReadPtr(chunks + (nint)(index / Offsets.GObjectsPerChunk) * nint.Size, out var chunk) ||
            !backend.TryRead(chunk + (nint)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize, item))
            return false;

        obj = (nint)BitConverter.ToInt64(item[Offsets.GObjectsItemObject..]);
        serial = BitConverter.ToInt32(item[Offsets.GObjectsItemSerial..]);
        return obj != 0;
    }

    private static bool TryReadBucketTable(this IReadBackend backend, nint countAt, nint bucketsAt, int max, out uint count, out nint buckets)
    {
        buckets = 0;
        return backend.TryRead(countAt, out count) && count != 0 && count <= max &&
               backend.TryReadPtr(bucketsAt, out buckets);
    }

    private static bool TryReadTMapAllocationBits(this IReadBackend backend, ReadOnlySpan<byte> header, int num, out uint[] bits)
    {
        bits = [];
        var secondary = (nint)BitConverter.ToInt64(header[Offsets.TMapSecondaryBits..]);
        if (secondary != 0)
            return backend.TryReadArray(secondary, (num + 31) / 32, out bits);
        if (num > Offsets.TMapInlineBitCount)
            return false;

        bits = new uint[Offsets.TMapInlineBitCount / 32];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = BitConverter.ToUInt32(header[(Offsets.TMapInlineBits + i * sizeof(uint))..]);
        return true;
    }
}

internal sealed class Names(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<uint, string> _known = [];

    public bool TryRead(uint index, out string name)
    {
        if (_known.TryGetValue(index, out var cached))
        {
            name = cached;
            return true;
        }

        if (!TryReadFromPool(index, out name))
            return false;

        _known[index] = name;
        return true;
    }

    public bool TryReadClassName(nint obj, out string name)
    {
        name = "";
        return memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && memory.TryRead(cls + Offsets.UObjectName, out uint index) && TryRead(index, out name);
    }

    private bool TryReadFromPool(uint index, out string name)
    {
        name = "";
        var block = index / Offsets.FNameBlockSize;
        if (block >= Offsets.MaxFNameBlocks ||
            !memory.TryReadPtr(moduleBase + Offsets.FNamePoolRva + Offsets.FNameBlocks + (nint)block * nint.Size, out var blockStart))
            return false;

        var entry = blockStart + (nint)(index % Offsets.FNameBlockSize) * Offsets.FNameEntryStride;
        if (!memory.TryRead(entry, out ushort header) || (header & Offsets.FNameWideBit) != 0)
            return false;

        var length = header >> Offsets.FNameLengthShift;
        if (length == 0 || length > Offsets.MaxNameLength || !memory.TryReadArray(entry + Offsets.FNameHeaderSize, length, out byte[] chars))
            return false;

        name = Encoding.ASCII.GetString(chars);
        return true;
    }
}
