using System.Runtime.InteropServices;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

internal sealed class FakeMemory : IReadBackend
{
    private readonly List<(nint Base, byte[] Bytes)> _regions = [];
    private readonly Lock _gate = new();

    public List<(nint Address, int Length)> Reads { get; } = [];

    public void Map(nint address, int size)
    {
        var at = Locate(address);
        if (at >= 0)
        {
            if (_regions[at].Base == address && _regions[at].Bytes.Length == size) return;
            throw new InvalidOperationException($"map 0x{address:X} overlaps an existing region");
        }

        at = ~at;
        if (at < _regions.Count && address + size > _regions[at].Base) throw new InvalidOperationException($"map 0x{address:X} overlaps an existing region");
        _regions.Insert(at, (address, new byte[size]));
    }

    public bool TryRead(nint address, Span<byte> target)
    {
        lock (_gate) Reads.Add((address, target.Length));
        var at = Locate(address);
        if (at < 0) return false;
        var (start, bytes) = _regions[at];
        if (address + target.Length > start + bytes.Length) return false;
        bytes.AsSpan((int)(address - start), target.Length).CopyTo(target);
        return true;
    }

    public void Write<T>(nint address, T value) where T : unmanaged => WriteArray(address, [value]);

    public void WriteArray<T>(nint address, T[] values) where T : unmanaged
    {
        var source = MemoryMarshal.AsBytes(values.AsSpan());
        var at = Locate(address);
        if (at < 0 || address + source.Length > _regions[at].Base + _regions[at].Bytes.Length) throw new InvalidOperationException($"write to unmapped 0x{address:X}");
        source.CopyTo(_regions[at].Bytes.AsSpan((int)(address - _regions[at].Base)));
    }

    private int Locate(nint address)
    {
        int low = 0, high = _regions.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) >>> 1;
            var (start, bytes) = _regions[mid];
            if (address < start) high = mid - 1;
            else if (address >= start + bytes.Length) low = mid + 1;
            else return mid;
        }

        return ~low;
    }
}
