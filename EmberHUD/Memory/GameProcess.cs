using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EmberHUD.Host.Memory;

internal sealed partial class GameProcess : IDisposable
{
    private const uint QueryLimitedAndVmRead = 0x1000 | 0x0010;
    private const uint StillActive = 259;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* buffer, ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    private readonly SafeProcessHandle _handle;

    private GameProcess(SafeProcessHandle handle, nint moduleBase, NativeReadBackend backend)
    {
        _handle = handle;
        ModuleBase = moduleBase;
        Backend = backend;
    }

    public IReadBackend Backend { get; }

    public nint ModuleBase { get; }

    public bool IsAlive => !_handle.IsClosed && GetExitCodeProcess(_handle, out var code) && code == StillActive;

    public static bool TryFindPid(out int pid, out string? failure)
    {
        var candidates = Process.GetProcessesByName(Offsets.GameProcessName);
        var count = candidates.Length;
        pid = count == 1 ? candidates[0].Id : 0;
        foreach (var candidate in candidates) candidate.Dispose();
        failure = count switch
        {
            1 => null,
            0 => $"{Offsets.GameProcessName}.exe is not running",
            _ => $"{count} processes are named {Offsets.GameProcessName}.exe, the target is ambiguous",
        };
        return count == 1;
    }

    public static bool TryAttach(int pid, ref (int Pid, string? Failure) rejectedBuild, out GameProcess? game, out string? failure)
    {
        game = null;
        if (pid == rejectedBuild.Pid)
        {
            failure = rejectedBuild.Failure;
            return false;
        }

        var handle = OpenProcess(QueryLimitedAndVmRead, false, pid);
        var error = Marshal.GetLastWin32Error();
        if (handle.IsInvalid)
        {
            handle.Dispose();
            failure = $"OpenProcess failed for pid {pid}, win32 error {error}";
            return false;
        }

        var attached = false;
        try
        {
            if (!TryReadImagePath(handle, out var path))
            {
                failure = $"could not query the image path of pid {pid}";
                return false;
            }

            var size = new FileInfo(path).Length;
            if (size != Offsets.KnownExeSize)
            {
                failure = $"unknown build - exe is {size} bytes, offsets are for {Offsets.KnownExeSize}";
                rejectedBuild = (pid, failure);
                return false;
            }

            var backend = new NativeReadBackend(handle);
            if (!ProcessName.TryReadPebAddress(handle, out var peb) ||
                !backend.TryReadPtr(peb + Offsets.PebImageBase, out var moduleBase))
            {
                failure = $"could not find the module base of pid {pid}";
                return false;
            }

            game = new GameProcess(handle, moduleBase, backend);
            attached = true;
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            failure = $"could not attach to pid {pid} - {ex.GetBaseException().Message}";
            return false;
        }
        finally
        {
            if (!attached) handle.Dispose();
        }
    }

    public void Dispose() => _handle.Dispose();

    private static unsafe bool TryReadImagePath(SafeProcessHandle handle, out string path)
    {
        var buffer = new char[32768];
        var length = (uint)buffer.Length;
        fixed (char* p = buffer)
        {
            var ok = QueryFullProcessImageNameW(handle, 0, p, ref length);
            path = ok ? new string(p, 0, (int)length) : "";
            return ok;
        }
    }
}

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
