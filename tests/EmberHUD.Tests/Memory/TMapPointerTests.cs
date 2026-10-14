using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class TMapPointerTests
{
    private static (int Key, nint Value)[] Read(FakeWorld world, int mapOffset) =>
        world.Memory.ReadTMapPointers(world.FightMgr + mapOffset);

    [Fact]
    public void ReadsInlineBitMapAndSkipsHoles()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (10, 0x1000), null, (12, 0x3000), null, (14, 0x5000));
        Assert.Equal([(10, 0x1000), (12, 0x3000), (14, 0x5000)], Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void ReadsMoreThan128ElementsFromTheSecondaryBitsWithHoles()
    {
        var world = new FakeWorld();
        var slots = Enumerable.Range(0, 300).Select(i => i % 3 == 1 ? null : ((int, nint)?)(i, 0x1000 + i)).ToArray();
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

    public static TheoryData<int> AbsurdCounts => new() { -1, Offsets.MaxMapElements + 1, int.MaxValue };

    [Theory]
    [MemberData(nameof(AbsurdCounts))]
    public void AbsurdElementCountGivesNothing(int num)
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0x1000));
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TArrayNum, num);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void UnmappedDataOrSecondaryBitsGiveNothing()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0x1000));
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster, FakeWorld.Unmapped);
        Assert.Empty(Read(world, Offsets.FightMapMonster));

        world.WriteMap(Offsets.FightMapPlayer, Enumerable.Range(0, 200).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapPlayer + Offsets.TMapSecondaryBits, FakeWorld.Unmapped);
        Assert.Empty(Read(world, Offsets.FightMapPlayer));
    }

    [Fact]
    public void NullValuePointersAreSkipped()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (1, 0), (2, 0x2000));
        Assert.Equal([2], Read(world, Offsets.FightMapMonster).Select(e => e.Key));
    }

    public static TheoryData<int> CountsAroundTheInlineLimit => new() { Offsets.TMapInlineBitCount - 1, Offsets.TMapInlineBitCount, Offsets.TMapInlineBitCount + 1, Offsets.MaxMapElements };

    [Theory]
    [MemberData(nameof(CountsAroundTheInlineLimit))]
    public void ReadsEveryElementAroundTheInlineLimitAndAtTheMaximum(int count)
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(1, count).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        Assert.Equal(count, Read(world, Offsets.FightMapMonster).Length);
    }

    [Fact]
    public void ANonNullSecondaryPointerWinsOverTheInlineBitsEvenForSmallMaps()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        world.UseSecondaryBits(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => i == 3 || i == 40).ToArray());
        Assert.Equal([3, 40], Read(world, Offsets.FightMapMonster).Select(e => e.Key));
    }

    [Fact]
    public void UnreadableSecondaryBitsGiveNothingEvenWhenTheHeaderAndDataRead()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 50).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapSecondaryBits, FakeWorld.Unmapped);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void MoreThanTheInlineBitsWithoutASecondaryPointerGivesNothing()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, Enumerable.Range(0, 200).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapSecondaryBits, 0L);
        Assert.Empty(Read(world, Offsets.FightMapMonster));
    }

    [Fact]
    public void AMapWithInlineBitsCostsItsHeaderAndItsElements()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapMonster, (10, 0x1000), (12, 0x3000));
        var map = world.FightMgr + Offsets.FightMapMonster;
        world.Memory.Reads.Clear();

        Assert.Equal(2, world.Memory.ReadTMapPointers(map).Length);

        Assert.Equal(2, world.Memory.Reads.Count);
        Assert.Equal((map, Offsets.TMapHeaderSize), world.Memory.Reads[0]);
    }

    [Fact]
    public void AMapWithSecondaryBitsCostsOneMoreRead()
    {
        var world = new FakeWorld();
        world.WriteMap(Offsets.FightMapPlayer, Enumerable.Range(0, 200).Select(i => ((int, nint)?)(i, 0x1000 + i)).ToArray());
        var map = world.FightMgr + Offsets.FightMapPlayer;
        world.Memory.Reads.Clear();

        Assert.Equal(200, world.Memory.ReadTMapPointers(map).Length);

        Assert.Equal(3, world.Memory.Reads.Count);
    }

    [Fact]
    public void AMapWhoseHeaderRunsOffItsMemoryGivesNothing()
    {
        var world = new FakeWorld();
        var truncated = world.Alloc(Offsets.TMapHeaderSize - 1);

        Assert.Empty(world.Memory.ReadTMapPointers(truncated));
    }
}
