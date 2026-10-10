using TorchlightSDK.Host;

namespace TorchlightSDK.Tests.Reading;

public class LogicWorldReaderTests
{
    private static LogicWorldReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    private static Entity[] Read(FakeWorld world)
    {
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        return reader.ReadEntities(scene);
    }

    [Fact]
    public void ReadsAliveEntitiesWithKeyAsIdCfgIdAndPosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(41, 1001, true, 10.5, -20.25, 3), world.AddEntity(42, 1002, true, 100, 200, 0));
        var entities = Read(world).OrderBy(e => e.Id).ToArray();
        Assert.Equal(2, entities.Length);
        Assert.Equal(41, entities[0].Id);
        Assert.Equal(1001, entities[0].CfgId);
        Assert.Equal(new Vector3(10.5f, -20.25f, 3), entities[0].Position);
        Assert.Equal(42, entities[1].Id);
        Assert.Equal(1002, entities[1].CfgId);
        Assert.All(entities, e => Assert.Equal(EntityKind.Monster, e.Kind));
        Assert.All(entities, e => Assert.Equal(Rarity.Normal, e.Rarity));
    }

    [Fact]
    public void WalksEveryChainIncludingCollidingKeys()
    {
        var world = new FakeWorld();
        var entities = Enumerable.Range(1, 10).Select(i => world.AddEntity((uint)i, (uint)(100 + i), true, i, i, 0)).ToArray();
        world.PublishEntities(3, entities);
        Assert.Equal(Enumerable.Range(1, 10), Read(world).Select(e => e.Id).Order());
    }

    [Fact]
    public void KeysAboveIntMaxStillGiveAnId()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(0xFFFF_FFF0, 1, true, 1, 1, 0));
        Assert.Equal(unchecked((int)0xFFFF_FFF0), Assert.Single(Read(world)).Id);
    }

    [Fact]
    public void SkipsDeadEntitiesAndEntitiesWithoutPositionOrRole()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 1, true, 1, 1, 0),
            world.AddEntity(2, 2, false, 2, 2, 0),
            world.AddEntity(3, 3, true, 3, 3, 0, hasPosition: false),
            world.AddEntity(4, 4, true, 4, 4, 0, hasRole: false));
        Assert.Equal([1], Read(world).Select(e => e.CfgId));
    }

    [Fact]
    public void SkipsNonFiniteAndFloatOverflowPositionsButKeepsTheRest()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 1, true, double.NaN, 0, 0),
            world.AddEntity(2, 2, true, 5, double.PositiveInfinity, 0),
            world.AddEntity(3, 3, true, 1e200, 0, 0),
            world.AddEntity(4, 4, true, 0, 0, -1e200),
            world.AddEntity(5, 5, true, 7, 8, 9));
        Assert.Equal([5], Read(world).Select(e => e.CfgId));
    }

    [Fact]
    public void NullAndUnmappedEntityPointersAreSkipped()
    {
        var world = new FakeWorld();
        world.PublishEntities(new FakeEntity(1, 0), world.AddEntity(2, 5, true, 1, 2, 3), new FakeEntity(3, 0x7777_0000_0000));
        Assert.Equal([5], Read(world).Select(e => e.CfgId));
    }

    [Fact]
    public void BucketCountOfZeroOrNothingPublishedGivesNoEntities()
    {
        var world = new FakeWorld();
        Assert.Empty(Read(world));
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBucketCount, 0u);
        Assert.Empty(Read(world));
    }

    [Theory]
    [InlineData(100001u)]
    [InlineData(uint.MaxValue)]
    public void AbsurdBucketCountGivesNoEntities(uint bucketCount)
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBucketCount, bucketCount);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void UnmappedBucketArrayGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBuckets, 0x7777_0000_0000L);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void ChainLongerThanTheLimitIsTornAndChainAtTheLimitIsFine()
    {
        var world = new FakeWorld();
        FakeEntity[] Make(int count) => Enumerable.Range(1, count).Select(i => world.AddEntity((uint)i, (uint)i, true, i, i, 0)).ToArray();
        world.PublishEntities(1, Make(Offsets.MaxChain));
        Assert.Equal(Offsets.MaxChain, Read(world).Length);
        world.PublishEntities(1, Make(Offsets.MaxChain + 1));
        Assert.Empty(Read(world));
    }

    [Fact]
    public void CyclicChainGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(1, 1, true, 1, 1, 0), world.AddEntity(2, 2, true, 2, 2, 0));
        world.Memory.Write(world.Nodes[0] + Offsets.NodeNext, world.Nodes[0]);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void UnmappedNodeInAChainGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(1, 1, true, 1, 1, 0), world.AddEntity(2, 2, true, 2, 2, 0));
        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, 0x7777_0000_0000L);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void MoreNodesThanTheLimitGivesNoEntities()
    {
        var world = new FakeWorld();
        var entities = Enumerable.Range(1, Offsets.MaxEntities + 1).Select(i => world.AddEntity((uint)i, (uint)i, true, i, i, 0)).ToArray();
        world.PublishEntities(512, entities);
        Assert.Empty(Read(world));
        world.PublishEntities(512, entities[..Offsets.MaxEntities]);
        Assert.Equal(Offsets.MaxEntities, Read(world).Length);
    }

    [Fact]
    public void NoWorldMeansAFailureTextNotAnException()
    {
        var world = new FakeWorld();
        world.BreakWorldPointer();
        var reader = ReaderFor(world);
        Assert.False(reader.TryReadScene(out _, out var failure));
        Assert.Contains("world", failure);
    }

    [Fact]
    public void NoSceneMeansNotInAMap()
    {
        var world = new FakeWorld();
        world.BreakScenePointer();
        var reader = ReaderFor(world);
        Assert.False(reader.TryReadScene(out _, out var failure));
        Assert.Contains("map", failure);
    }
}
