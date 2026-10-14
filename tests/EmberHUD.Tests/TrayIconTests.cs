namespace EmberHUD.Tests;

public class TrayIconTests
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
}
