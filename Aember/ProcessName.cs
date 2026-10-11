using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Aember.Host;

internal static unsafe partial class ProcessName
{
    private const int LdrOffset = 0x18;
    private const int ProcessParametersOffset = 0x20;
    private const int InLoadOrderModuleListOffset = 0x10;
    private const int ImagePathNameOffset = 0x60;
    private const int CommandLineOffset = 0x70;
    private const int FullDllNameOffset = 0x48;
    private const int BaseDllNameOffset = 0x58;

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(SafeProcessHandle process, int informationClass, nint* information, int length, out int returned);

    public static void Spoof(string name)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, name);
        using var self = new SafeProcessHandle(-1, ownsHandle: false);
        var information = stackalloc nint[6];
        if (NtQueryInformationProcess(self, 0, information, 6 * nint.Size, out _) < 0) return;
        var peb = information[1];
        if (peb == 0) return;

        var ldrEntry = *(nint*)(*(nint*)(peb + LdrOffset) + InLoadOrderModuleListOffset);
        Write(ldrEntry + BaseDllNameOffset, name);
        Write(ldrEntry + FullDllNameOffset, fullPath);

        var parameters = *(nint*)(peb + ProcessParametersOffset);
        Write(parameters + ImagePathNameOffset, fullPath);
        Write(parameters + CommandLineOffset, fullPath);
    }

    private static void Write(nint unicodeString, string value)
    {
        var bytes = checked((ushort)(value.Length * 2));
        var buffer = Marshal.AllocHGlobal(bytes + 2);
        fixed (char* source = value) Buffer.MemoryCopy(source, (void*)buffer, bytes + 2, bytes);
        ((char*)buffer)[value.Length] = '\0';
        *(ushort*)unicodeString = bytes;
        *(ushort*)(unicodeString + 2) = (ushort)(bytes + 2);
        *(nint*)(unicodeString + 8) = buffer;
    }
}
