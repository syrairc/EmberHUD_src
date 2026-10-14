using System.Runtime.InteropServices;

namespace EmberHUD.Tests;

public class Win32WindowTests
{
    [Fact]
    public void MinTrackSizeSitsWhereWindowsPutsItInMinMaxInfo() =>
        Assert.Equal(24, (int)Marshal.OffsetOf<Win32.MinMaxInfo>(nameof(Win32.MinMaxInfo.MinTrackSize)));

    static readonly Win32.WndProcDelegate Proc = Win32.DefWindowProc;

    [Fact]
    public void DestroyWindowAndClassFreesTheClassName()
    {
        var (handle, className) = Win32.CreateWindow("EmberHUDTest", Proc, 0, 0, "test", 0, 0, 0, 0);
        Assert.True(handle != 0);
        Win32.DestroyWindowAndClass(handle, className);
        Assert.False(Win32.UnregisterClass(className, Win32.GetModuleHandle(null)));
    }

    [Fact]
    public void StayingRegisteredWithoutItIsRealControl()
    {
        var (handle, className) = Win32.CreateWindow("EmberHUDTest", Proc, 0, 0, "test", 0, 0, 0, 0);
        Win32.DestroyWindow(handle);
        Assert.True(Win32.UnregisterClass(className, Win32.GetModuleHandle(null)));
    }
}
