using System.Runtime.InteropServices;
using Aember.Host.Memory;

namespace Aember.Tests;

internal sealed class FakeMemory : IReadBackend
{
    private readonly List<(long Base, byte[] Bytes)> _regions = [];

    public void Map(long address, int size) => _regions.Add((address, new byte[size]));

    public List<(nint Address, int Length)> Reads { get; } = [];

    public bool TryRead(nint address, Span<byte> target)
    {
        Reads.Add((address, target.Length));
        foreach (var (start, bytes) in _regions)
        {
            if (address < start || address + target.Length > start + bytes.Length) continue;
            bytes.AsSpan((int)(address - start), target.Length).CopyTo(target);
            return true;
        }

        return false;
    }

    public void Write<T>(long address, T value) where T : unmanaged => WriteArray(address, [value]);

    public void WriteArray<T>(long address, T[] values) where T : unmanaged
    {
        var source = MemoryMarshal.AsBytes(values.AsSpan());
        foreach (var (start, bytes) in _regions)
        {
            if (address < start || address + source.Length > start + bytes.Length) continue;
            source.CopyTo(bytes.AsSpan((int)(address - start)));
            return;
        }

        throw new InvalidOperationException($"write to unmapped 0x{address:X}");
    }
}
