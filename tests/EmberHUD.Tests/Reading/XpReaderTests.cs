using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class XpReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
        public readonly FakeLua Lua;
        public long Now;

        public Fixture(bool withManager = true, long level = 92, long exp = 87255834, long next = 90077482)
        {
            Lua = new FakeLua(Memory, Module);
            var manager = Lua.NewTable(0, 3);
            Lua.SetInt(manager, Offsets.LuaExpLevel, level);
            Lua.SetInt(manager, Offsets.LuaExpCurrent, exp);
            Lua.SetInt(manager, Offsets.LuaExpNext, next);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaExpManager, manager);
        }

        public XpReader Reader() => new(Memory, Module, () => Now);
    }

    [Fact]
    public void ReadsLevelExpAndNext()
    {
        var xp = new Fixture().Reader().Read()!;
        Assert.Equal(92, xp.Level);
        Assert.Equal(87255834, xp.Exp);
        Assert.Equal(90077482, xp.ToNext);
    }

    [Fact]
    public void MissingManagerIsNull() => Assert.Null(new Fixture(withManager: false).Reader().Read());

    [Fact]
    public void GarbageReadIsNull() => Assert.Null(new Fixture(level: 0).Reader().Read());

    [Fact]
    public void ReadIsCachedUntilTheRefreshInterval()
    {
        var f = new Fixture();
        var reader = f.Reader();
        var first = reader.Read()!;
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.XpRefreshMilliseconds - 1) / 1000;
        Assert.Same(first, reader.Read());
        f.Now = System.Diagnostics.Stopwatch.Frequency * Offsets.XpRefreshMilliseconds / 1000;
        Assert.NotSame(first, reader.Read());
    }

    [Theory]
    [InlineData(92, 87255834L, 90077482L, true)]
    [InlineData(0, 10L, 100L, false)]
    [InlineData(201, 10L, 100L, false)]
    [InlineData(50, 101L, 100L, false)]
    [InlineData(50, 0L, 0L, false)]
    [InlineData(50, -1L, 100L, false)]
    public void ValidRejectsGarbage(int level, long exp, long toNext, bool valid) => Assert.Equal(valid, XpReader.Valid(level, exp, toNext));

    [Fact]
    public void TornTripleIsRejected()
    {
        Assert.Null(XpReader.Stable((92, 100L, 1000L), (93, 100L, 1000L)));
        Assert.Null(XpReader.Stable((92, 100L, 1000L), (92, 5L, 1000L)));
        Assert.Null(XpReader.Stable(null, (92, 100L, 1000L)));
        Assert.Null(XpReader.Stable((92, 100L, 1000L), null));
    }

    [Fact]
    public void MatchingTripleIsPublished()
    {
        var xp = XpReader.Stable((92, 100L, 1000L), (92, 100L, 1000L))!;
        Assert.Equal(new XpState(92, 100, 1000), xp);
        Assert.Null(XpReader.Stable((0, 100L, 1000L), (0, 100L, 1000L)));
    }
}
