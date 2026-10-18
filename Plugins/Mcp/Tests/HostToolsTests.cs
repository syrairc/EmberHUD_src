using EmberHUD;
using EmberHUD.Testing;

namespace Mcp;

public sealed class HostToolsTests
{
    [Test]
    public void ShutdownRepliesFirstAndQuitsOnceAfterTheDelay()
    {
        var quits = 0;
        TestGame.UseQuit(() => quits++);
        try
        {
            long[] clock = [1000];
            var main = new Main { Clock = () => clock[0] };
            var tools = HostTools.Create(main.RequestShutdown);
            Check.That((bool)Wire.Run(tools, "shutdown")["quitting"]!, "replies quitting");
            main.Tick();
            Check.Equal(0, quits);
            clock[0] = 1600;
            main.Tick();
            main.Tick();
            Check.Equal(1, quits);
        }
        finally
        {
            TestGame.UseQuit(null);
        }
    }

    [Test]
    public void QuitWithoutAHostDoesNothing()
    {
        TestGame.UseQuit(null);
        App.Quit();
    }
}
