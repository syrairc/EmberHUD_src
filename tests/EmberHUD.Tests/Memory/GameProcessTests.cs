using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class GameProcessTests
{
    private const int NoSuchPid = int.MaxValue;

    [Fact]
    public void ARejectedBuildPidFailsWithTheSameTextWithoutOpeningTheProcess()
    {
        var rejected = (Pid: NoSuchPid, Failure: (string?)"unknown build - exe is 1 bytes, offsets are for 2");

        Assert.False(GameProcess.TryAttach(NoSuchPid, ref rejected, out var game, out var failure));

        Assert.Null(game);
        Assert.Equal("unknown build - exe is 1 bytes, offsets are for 2", failure);
    }

    [Fact]
    public void ADifferentPidIsAttemptedAgain()
    {
        var rejected = (Pid: 4, Failure: (string?)"unknown build - exe is 1 bytes, offsets are for 2");

        Assert.False(GameProcess.TryAttach(NoSuchPid, ref rejected, out _, out var failure));

        Assert.StartsWith("OpenProcess failed", failure);
        Assert.Equal(4, rejected.Pid);
    }
}
