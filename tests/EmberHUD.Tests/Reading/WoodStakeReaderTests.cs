using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class WoodStakeReaderTests
{
    private const long Module = 0x1_4000_0000;
    private const long Mgr = 0x2_0000_0000;

    private static (FakeMemory Memory, WoodStakeReader Reader) Fixture()
    {
        var memory = new FakeMemory();
        memory.Map(Module + Offsets.WoodStakeMgrGlobalRva, 8);
        memory.Map(Mgr, 0x200);
        memory.Write(Module + Offsets.WoodStakeMgrGlobalRva, Mgr);
        return (memory, new WoodStakeReader(memory, unchecked((nint)Module)));
    }

    private static void WriteStatistic(FakeMemory memory, int seconds, long avg, long cur, long max, long curUnlimited, long maxUnlimited)
    {
        var stat = Mgr + Offsets.WoodStakeStatistic;
        memory.Write(stat + Offsets.WoodStakeStatSeconds, seconds);
        memory.Write(stat + Offsets.WoodStakeStatAvg, avg);
        memory.Write(stat + Offsets.WoodStakeStatCur, cur);
        memory.Write(stat + Offsets.WoodStakeStatMax, max);
        memory.Write(stat + Offsets.WoodStakeStatCurUnlimited, curUnlimited);
        memory.Write(stat + Offsets.WoodStakeStatMaxUnlimited, maxUnlimited);
    }

    private static void WriteTotal(FakeMemory memory, UInt128 raw, int count)
    {
        var at = Mgr + Offsets.WoodStakeUnlimitedTotal;
        var limbs = new ulong[Offsets.BigNumberLimbs];
        limbs[0] = (ulong)(raw & ulong.MaxValue);
        limbs[1] = (ulong)(raw >> 64);
        memory.WriteArray(at, limbs);
        memory.Write(at + Offsets.BigNumberCount, count);
    }

    private static UInt128 Scaled(long real) => (UInt128)real * (UInt128)Offsets.BigNumberScale;

    [Fact]
    public void ReadsTheStatisticAndAveragesTheUnlimitedTotalOverTheDuration()
    {
        var (memory, reader) = Fixture();
        memory.Write(Mgr + Offsets.WoodStakeRunning, (byte)1);
        WriteStatistic(memory, 100, 10, 20, 30, 40, 50);
        WriteTotal(memory, Scaled(8_000_000_000_000), 2);
        var stats = reader.Read();
        Assert.NotNull(stats);
        Assert.True(stats.Running);
        Assert.Equal(100, stats.Seconds);
        Assert.Equal((10L, 20L, 30L), (stats.Avg, stats.Cur, stats.Max));
        Assert.Equal((40L, 50L), (stats.CurUnlimited, stats.MaxUnlimited));
        Assert.Equal(80_000_000_000L, stats.AvgUnlimited);
    }

    [Fact]
    public void ASingleLimbTotalDecodes()
    {
        var (memory, reader) = Fixture();
        WriteStatistic(memory, 257, 1, 1, 1, 1, 1);
        WriteTotal(memory, 0x098C09AB2E331300UL, 1);
        var stats = reader.Read();
        Assert.NotNull(stats);
        Assert.False(stats.Running);
        Assert.Equal(6_879_354_739L / 257, stats.AvgUnlimited);
    }

    [Fact]
    public void ZeroSecondsDividesByOne()
    {
        var (memory, reader) = Fixture();
        WriteStatistic(memory, 0, 0, 0, 0, 0, 0);
        WriteTotal(memory, Scaled(123), 1);
        Assert.Equal(123, reader.Read()!.AvgUnlimited);
    }

    [Fact]
    public void ABigNumberBeyondTwoLimbsSaturates()
    {
        var (memory, reader) = Fixture();
        WriteStatistic(memory, 1, 0, 0, 0, 0, 0);
        WriteTotal(memory, 1, 3);
        Assert.Equal(long.MaxValue, reader.Read()!.AvgUnlimited);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveLimbCountIsZero(int count)
    {
        var (memory, reader) = Fixture();
        WriteStatistic(memory, 5, 0, 0, 0, 0, 0);
        WriteTotal(memory, Scaled(999), count);
        Assert.Equal(0, reader.Read()!.AvgUnlimited);
    }

    [Fact]
    public void NoManagerMeansNothing()
    {
        var (memory, reader) = Fixture();
        memory.Write(Module + Offsets.WoodStakeMgrGlobalRva, 0L);
        Assert.Null(reader.Read());
    }

    [Theory]
    [InlineData(-1, 0L)]
    [InlineData(86401, 0L)]
    [InlineData(10, -1L)]
    public void GarbageIsRejected(int seconds, long avg)
    {
        var (memory, reader) = Fixture();
        WriteStatistic(memory, seconds, avg, 0, 0, 0, 0);
        WriteTotal(memory, 0, 0);
        Assert.Null(reader.Read());
    }

    [Fact]
    public void AnUnreadableManagerMeansNothing()
    {
        var memory = new FakeMemory();
        memory.Map(Module + Offsets.WoodStakeMgrGlobalRva, 8);
        memory.Write(Module + Offsets.WoodStakeMgrGlobalRva, Mgr);
        Assert.Null(new WoodStakeReader(memory, unchecked((nint)Module)).Read());
    }
}
