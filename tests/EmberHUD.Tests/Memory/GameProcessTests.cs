using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class GameProcessTests
{
    private const int NoSuchPid = int.MaxValue;

    [Fact]
    public void AnUnopenablePidFailsWithTheWin32Error()
    {
        Assert.False(GameProcess.TryAttach(NoSuchPid, out var game, out var failure));

        Assert.Null(game);
        Assert.StartsWith("OpenProcess failed", failure);
    }

    [Fact]
    public void AnAccessDeniedPidSaysTheGameIsElevated()
    {
        using var elevated = Process.GetProcessesByName("winlogon").FirstOrDefault();
        if (elevated is null) return;
        if (!GameProcess.TryAttach(elevated.Id, out var game, out var failure)) Assert.EndsWith("win32 error 5 - the game is running elevated, start it from EmberLauncher or run Ember as administrator", failure);
        else game?.Dispose();
    }

    [Fact]
    public void AnExeOfUnknownSizeStillAttaches()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(GameProcess.TryAttach(Environment.ProcessId, out var game, out var failure), failure);
            using (game)
            {
                Assert.NotEqual(nint.Zero, game!.ModuleBase);
                Assert.False(game.ExactBuild);
                Assert.Equal(new FileInfo(Process.GetCurrentProcess().MainModule!.FileName).Length, game.ExeSize);
                Assert.Contains(Offsets.BuildSizes[Offsets.Build], Offsets.BuildSizes);
            }
        }
        finally
        {
            Offsets.Build = original;
        }
    }
}
