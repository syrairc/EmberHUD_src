using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Aember.Host.Memory;

internal sealed partial class NativeReadBackend(SafeProcessHandle process) : IReadBackend
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool ReadProcessMemory(SafeProcessHandle process, nint address, byte* buffer, nint size, out nint read);

    public unsafe bool TryRead(nint address, Span<byte> target)
    {
        if (target.IsEmpty) return true;
        fixed (byte* p = target)
        {
            return ReadProcessMemory(process, address, p, target.Length, out var read) && read == target.Length;
        }
    }
}
