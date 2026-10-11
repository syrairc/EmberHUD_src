using System.Runtime.InteropServices;

namespace TorchlightSDK.Host.Memory;

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

    public static bool TryReadTMapAllocationBits(this IReadBackend backend, nint map, int num, out uint[] bits)
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
