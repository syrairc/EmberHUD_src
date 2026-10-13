using System.Runtime.InteropServices;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public partial class ProcessNameTests
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial uint GetModuleFileNameW(nint module, char* buffer, uint size);

    [Fact]
    public unsafe void SpoofRewritesPebImageName()
    {
        ProcessName.Spoof("totally-not-ember.exe");

        var buffer = stackalloc char[260];
        var length = GetModuleFileNameW(0, buffer, 260);
        var path = new string(buffer, 0, (int)length);

        Assert.EndsWith("totally-not-ember.exe", path);
    }
}
