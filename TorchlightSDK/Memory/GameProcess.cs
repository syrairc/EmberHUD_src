using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TorchlightSDK.Host.Memory;

internal sealed partial class GameProcess : IDisposable
{
    private const string ProcessName = "torchlight_infinite";

    private const uint QueryLimitedAndVmRead = 0x1000 | 0x0010;
    private const uint StillActive = 259;
    private const int ProcessBasicInformation = 0;
    private const int PebBaseAddressIndex = 1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* buffer, ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("ntdll.dll")]
    private static unsafe partial int NtQueryInformationProcess(SafeProcessHandle process, int informationClass, nint* information, int length, out int returned);

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

    public static bool TryAttach(out GameProcess? game, out string? failure)
    {
        game = null;
        var candidates = Process.GetProcessesByName(ProcessName);
        var count = candidates.Length;
        var pid = count == 1 ? candidates[0].Id : 0;
        foreach (var candidate in candidates) candidate.Dispose();
        if (count != 1)
        {
            failure = count == 0 ? $"{ProcessName}.exe is not running" : $"{count} processes are named {ProcessName}.exe, the target is ambiguous";
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
                return false;
            }

            var backend = new NativeReadBackend(handle);
            if (!TryReadPebAddress(handle, out var peb) ||
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
            failure = $"could not attach to pid {pid} - {ex.Message}";
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

    private static unsafe bool TryReadPebAddress(SafeProcessHandle handle, out nint peb)
    {
        var information = stackalloc nint[6];
        var ok = NtQueryInformationProcess(handle, ProcessBasicInformation, information, 6 * nint.Size, out _) >= 0;
        peb = ok ? information[PebBaseAddressIndex] : 0;
        return ok;
    }
}
