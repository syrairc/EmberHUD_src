using TorchlightSDK.Host;
using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class MonsterRarityReaderTests
{
    private static MonsterRarityReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    [Fact]
    public void MapsTheConfirmedTiersAndTreatsUnknownOnesAsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1), (2200080, 2), (333, 3), (190200, 4), (555, 5), (777, 0), (888, 99));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(1180001));
        Assert.Equal(Rarity.Rare, reader.Read(2200080));
        Assert.Equal(Rarity.Normal, reader.Read(333));
        Assert.Equal(Rarity.Boss, reader.Read(190200));
        Assert.Equal(Rarity.Normal, reader.Read(555));
        Assert.Equal(Rarity.Normal, reader.Read(777));
        Assert.Equal(Rarity.Normal, reader.Read(888));
    }

    [Fact]
    public void FindsRowsAcrossCollidingChains()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(3, Enumerable.Range(1, 30).Select(i => ((uint)(i * 3), i % 2 == 0 ? 4u : 1u)).ToArray());
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Boss, reader.Read(6));
        Assert.Equal(Rarity.Normal, reader.Read(3));
        Assert.Equal(Rarity.Boss, reader.Read(90));
    }

    [Fact]
    public void MissingRowIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1, 1));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(2200080));
        world.SetMonsterConfigTable(7, (1, 1), (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080));
    }

    [Fact]
    public void FoundRowsAreCachedAndNotReadAgain()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(2200080));
        world.BreakMonsterConfigTable();
        Assert.Equal(Rarity.Rare, reader.Read(2200080));
    }

    [Fact]
    public void NoTableMeansNormal()
    {
        Assert.Equal(Rarity.Normal, ReaderFor(new FakeWorld()).Read(1));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(100001u)]
    [InlineData(uint.MaxValue)]
    public void AbsurdBucketCountIsNormal(uint bucketCount)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.MonsterCfgBucketCountRva, bucketCount);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(2200080));
    }

    [Fact]
    public void UnmappedBucketArrayIsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.MonsterCfgBucketsRva, 0x7777_0000_0000L);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(2200080));
    }

    [Fact]
    public void ChainLongerThanTheLimitIsNormalAndCycleIsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(1, Enumerable.Range(1, Offsets.MaxChain + 1).Select(i => ((uint)i, 4u)).ToArray());
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(1));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read((uint)Offsets.MaxChain + 1));

        world.SetMonsterConfigTable(1, (1, 4), (2, 4));
        world.Memory.Write(world.ConfigNodes[1] + Offsets.NodeNext, world.ConfigNodes[1]);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(99));
    }

    [Fact]
    public void UnmappedRowIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(world.ConfigNodes[0] + Offsets.NodeEntity, 0x7777_0000_0000L);
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(2200080));
        world.SetMonsterConfigTable(7, (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080));
    }
}
