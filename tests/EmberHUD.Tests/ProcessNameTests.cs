using System.Runtime.InteropServices;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public partial class ProcessNameTests
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial uint GetModuleFileNameW(nint module, char* buffer, uint size);

    private static unsafe string ModuleFileName()
    {
        var buffer = stackalloc char[260];
        var length = GetModuleFileNameW(0, buffer, 260);
        return new string(buffer, 0, (int)length);
    }

    [Fact]
    public void SpoofRewritesPebImageName()
    {
        var original = ModuleFileName();
        try
        {
            ProcessName.Spoof("totally-not-ember.exe");

            Assert.EndsWith("totally-not-ember.exe", ModuleFileName());
        }
        finally
        {
            ProcessName.Spoof(Path.GetFileName(original));
        }
        Assert.Equal(original, ModuleFileName(), ignoreCase: true);
    }
}
