namespace EmberHUD.Tests;

public class Win32Tests
{
    [Fact]
    public void AppIconComesFromTheExeItIsHandedNotTheProcessPath()
    {
        var icon = TrayIcon.LoadAppIcon(Path.Combine(AppContext.BaseDirectory, "Ember.exe"));
        try
        {
            Assert.NotEqual(0, icon);
        }
        finally
        {
            Win32.DestroyIcon(icon);
        }
    }

    [Fact]
    public void PumpKeepsRunningWhileNothingQuits()
    {
        var quit = false;
        Assert.True(Win32.PumpMessages(ref quit));
        Assert.False(quit);
    }

    [Fact]
    public void PumpStopsAndStaysStoppedOnceQuitArrives()
    {
        var quit = false;
        Win32.PostQuitMessage(0);
        Assert.False(Win32.PumpMessages(ref quit));
        Assert.True(quit);
        Assert.False(Win32.PumpMessages(ref quit));
    }
}
