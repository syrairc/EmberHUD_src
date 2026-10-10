using TorchlightSDK.Host;

namespace TorchlightSDK.Tests.Reading;

public class LogicWorldReaderTests
{
    private static LogicWorldReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    [Fact]
    public void ReadsAliveEntitiesWithCfgIdAndPosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1001, true, 10.5, -20.25, 3), world.AddEntity(1002, true, 100, 200, 0));
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        var entities = reader.ReadEntities(scene);
        Assert.Equal(2, entities.Length);
        Assert.Equal(1001, entities[0].CfgId);
        Assert.Equal(new Vector3(10.5f, -20.25f, 3), entities[0].Position);
        Assert.Equal(1002, entities[1].CfgId);
        Assert.NotEqual(entities[0].Id, entities[1].Id);
        Assert.All(entities, e => Assert.Equal(EntityKind.Monster, e.Kind));
        Assert.All(entities, e => Assert.Equal(Rarity.Normal, e.Rarity));
    }

    [Fact]
    public void SkipsDeadEntitiesAndEntitiesWithoutPosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, true, 1, 1, 0),
            world.AddEntity(2, false, 2, 2, 0),
            world.AddEntity(3, true, 3, 3, 0, hasPosition: false));
        var reader = ReaderFor(world);
        reader.TryReadScene(out var scene, out _);
        Assert.Equal([1], reader.ReadEntities(scene).Select(e => e.CfgId));
    }

    [Fact]
    public void SkipsNonFinitePositionsButKeepsTheRest()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, true, double.NaN, 0, 0),
            world.AddEntity(2, true, 5, double.PositiveInfinity, 0),
            world.AddEntity(3, true, 7, 8, 9));
        var reader = ReaderFor(world);
        reader.TryReadScene(out var scene, out _);
        Assert.Equal([3], reader.ReadEntities(scene).Select(e => e.CfgId));
    }

    [Fact]
    public void NullAndUnmappedEntityPointersAreSkipped()
    {
        var world = new FakeWorld();
        world.PublishEntities(0, world.AddEntity(5, true, 1, 2, 3), 0x7777_0000_0000);
        var reader = ReaderFor(world);
        reader.TryReadScene(out var scene, out _);
        Assert.Equal([5], reader.ReadEntities(scene).Select(e => e.CfgId));
    }

    [Fact]
    public void AbsurdCountGivesNoEntitiesAndNeverThrows()
    {
        var world = new FakeWorld();
        var one = world.AddEntity(9, true, 1, 1, 1);
        world.PublishEntities(one);
        world.Memory.Write(world.Scene + Offsets.SceneEntityCount, uint.MaxValue);
        var reader = ReaderFor(world);
        reader.TryReadScene(out var scene, out _);
        Assert.Empty(reader.ReadEntities(scene));
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
