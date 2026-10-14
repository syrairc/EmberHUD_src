namespace EmberHUD.Tests;

public class Win32PumpTests
{
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
