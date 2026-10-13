using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class FightMapReaderTests
{
    private static (int Key, nint Value)[] Read(FakeWorld world, int mapOffset) =>
        new FightMapReader(world.Memory).Read(unchecked((nint)(world.FightMgr + mapOffset)));

    [Fact]
    public void ReadsInlineBitMapAndSkipsHoles()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (10, 0x1000), null, (12, 0x3000), null, (14, 0x5000));
        Assert.Equal([(10, (nint)0x1000), (12, (nint)0x3000), (14, (nint)0x5000)], Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void ReadsExactly128ElementsFromTheInlineBits()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(1, 128).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        Assert.Equal(128, Read(world, Offsets.FightMapMonster).Length);
    }

    [Fact]
    public void ReadsMoreThan128ElementsFromTheSecondaryBitsWithHoles()
    {
        var world = new FakeWorld();
        var slots = Enumerable.Range(0, 300).Select(i => i % 3 == 1 ? null : ((int, long)?)(i, 0x1000 + i)).ToArray();
        world.WriteMap(Offsets.FightMapPlayer, slots);
        var result = Read(world, Offsets.FightMapPlayer);
        Assert.Equal(Enumerable.Range(0, 300).Where(i => i % 3 != 1), result.Select(e => e.Key));
        Assert.All(result, e => Assert.Equal(0x1000 + e.Key, (int)e.Value));
    }

    [Fact]
    public void AllBitsClearGivesNothing()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, null, null, null);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void EmptyMapGivesNothing()
    {
        Assert.Empty(Read(new FakeWorld(), Offsets.FightMapNpc));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20001)]
    [InlineData(int.MaxValue)]
    public void AbsurdElementCountGivesNothing(int num)
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0x1000));
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapNum, num);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void UnmappedDataOrSecondaryBitsGiveNothing()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0x1000));
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster, 0x7777_0000_0000L);
        Assert.Empty(Read(world, Offsets.FightMapMonster));

        world.WriteMap(Offsets.FightMapPlayer, Enumerable.Range(0, 200).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapPlayer + Offsets.TMapSecondaryBits, 0x7777_0000_0000L);
        Assert.Empty(Read(world, Offsets.FightMapPlayer));
    }

    [Fact]
    public void NullValuePointersAreSkipped()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0), (2, 0x2000));
        Assert.Equal([2], Read(world, Offsets.FightMapMonster).Select(e => e.Key));
    }

    [Theory]
    [InlineData(127)]
    [InlineData(129)]
    [InlineData(20000)]
    public void ReadsEveryElementAroundTheInlineLimitAndAtTheMaximum(int count)
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(1, count).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        Assert.Equal(count, Read(world, Offsets.FightMapMonster).Length);
    }

    [Fact]
    public void OneMoreThanTheMaximumIsRejected()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(1, 20001).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void ANonNullSecondaryPointerWinsOverTheInlineBitsEvenForSmallMaps()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        world.UseSecondaryBits(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => i == 3 || i == 40).ToArray());
        Assert.Equal([3, 40], Read(world, Offsets.FightMapMonster).Select(e => e.Key));
    }

    [Fact]
    public void UnreadableSecondaryBitsGiveNothingEvenWhenTheHeaderAndDataRead()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapSecondaryBits, 0x7777_0000_0000L);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void MoreThanTheInlineBitsWithoutASecondaryPointerGivesNothing()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 200).Select(i => ((int, long)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapSecondaryBits, 0L);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }
}
