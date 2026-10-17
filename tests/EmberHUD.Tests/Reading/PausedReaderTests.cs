using EmberHUD.Host;

namespace EmberHUD.Tests;

public class PausedReaderTests
{
    private sealed class Rig
    {
        public FakeWorld World { get; } = new();
        public long Now { get; set; } = 1000;
        public PausedReader Reader { get; }

        public Rig(uint frame = 500)
        {
            World.SetFightFrame(frame);
            Reader = new PausedReader(World.Memory, FakeWorld.Module, () => Now);
        }
    }

    [Fact]
    public void TheFirstReadHasNothingToCompareAndIsUnknown()
    {
        var rig = new Rig();
        Assert.Null(rig.Reader.Read());
    }

    [Fact]
    public void ACounterThatMovedIsNotPausedAndAnyChangeCounts()
    {
        var rig = new Rig(500);
        rig.Reader.Read();
        rig.World.SetFightFrame(501);
        Assert.False(rig.Reader.Read());
        rig.World.SetFightFrame(3);
        Assert.False(rig.Reader.Read());
    }

    [Fact]
    public void AFrozenCounterIsPausedOnceTheWindowHasPassed()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.Now += Offsets.PausedAfterMilliseconds - 1;
        Assert.Null(rig.Reader.Read());
        rig.Now += 1;
        Assert.True(rig.Reader.Read());
        rig.Now += 5000;
        Assert.True(rig.Reader.Read());
    }

    [Fact]
    public void ShortStillnessKeepsThePreviousValue()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.World.SetFightFrame(501);
        Assert.False(rig.Reader.Read());
        rig.Now += Offsets.PausedAfterMilliseconds - 1;
        Assert.False(rig.Reader.Read());
        rig.Now += 1;
        Assert.True(rig.Reader.Read());
    }

    [Fact]
    public void ResumingFlipsBackToFalseAndRestartsTheWindow()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.Now += Offsets.PausedAfterMilliseconds;
        Assert.True(rig.Reader.Read());
        rig.World.SetFightFrame(501);
        Assert.False(rig.Reader.Read());
        rig.Now += Offsets.PausedAfterMilliseconds - 1;
        Assert.False(rig.Reader.Read());
    }

    [Fact]
    public void AnUnresolvedChainIsUnknown()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.Now += Offsets.PausedAfterMilliseconds;
        Assert.True(rig.Reader.Read());
        rig.World.SetFightMsgResolvable(false);
        Assert.Null(rig.Reader.Read());
    }

    [Fact]
    public void AfterAFailedReadTheNextCleanReadStartsFresh()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.Now += Offsets.PausedAfterMilliseconds;
        Assert.True(rig.Reader.Read());
        rig.World.SetFightMsgResolvable(false);
        Assert.Null(rig.Reader.Read());
        rig.World.SetFightMsgResolvable(true);
        Assert.Null(rig.Reader.Read());
        rig.Now += Offsets.PausedAfterMilliseconds;
        Assert.True(rig.Reader.Read());
    }

    [Fact]
    public void TheFightMsgPointerIsResolvedOnEveryRead()
    {
        var rig = new Rig();
        rig.Reader.Read();
        rig.World.Memory.Reads.Clear();
        rig.Reader.Read();
        Assert.Contains(rig.World.Memory.Reads, r => r.Address == FakeWorld.Module + Offsets.FightMgrGlobalRva);
        Assert.Contains(rig.World.Memory.Reads, r => r.Address == rig.World.FightMgr + Offsets.FightWorldFirst);
    }
}
