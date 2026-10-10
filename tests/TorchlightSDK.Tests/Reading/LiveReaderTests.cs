using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class LiveReaderTests
{
    private static LiveReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    [Fact]
    public void ReadsTerrainEntitiesAndAreaFromTheFakeWorld()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(5, 77, true, 50, 60, 0));
        using var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out var failure));
        Assert.Null(failure);
        Assert.Equal(1, state.AreaId);
        Assert.NotNull(state.Terrain);
        Assert.Equal(77, Assert.Single(state.Entities).CfgId);
        Assert.Null(state.Player);
        Assert.Contains("live", state.Status);
    }

    [Fact]
    public void KeepsTheSameTerrainInstanceAcrossReads()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        using var reader = ReaderFor(world);
        reader.TryRead(out var first, out _);
        reader.TryRead(out var second, out _);
        Assert.Same(first.Terrain, second.Terrain);
        Assert.Equal(first.AreaId, second.AreaId);
    }

    [Fact]
    public void ChangingGridBumpsTheAreaId()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        using var reader = ReaderFor(world);
        reader.TryRead(out var first, out _);
        world.SetGrid(5, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        reader.TryRead(out var second, out _);
        Assert.Equal(first.AreaId + 1, second.AreaId);
    }

    [Fact]
    public void BufferSwapWithTheSameGridKeepsTheAreaIdAndTerrain()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        using var reader = ReaderFor(world);
        reader.TryRead(out var first, out _);
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        reader.TryRead(out var second, out _);
        Assert.Same(first.Terrain, second.Terrain);
        Assert.Equal(first.AreaId, second.AreaId);
    }

    [Fact]
    public void SeededReaderContinuesTheAreaIdAfterAReattach()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        using var reader = new LiveReader(world.Memory, unchecked((nint)FakeWorld.ModuleBase), 5);
        reader.TryRead(out var state, out _);
        Assert.Equal(6, state.AreaId);
    }

    [Fact]
    public void NoSceneIsAFailureWithTheReason()
    {
        var world = new FakeWorld();
        world.BreakScenePointer();
        using var reader = ReaderFor(world);
        Assert.False(reader.TryRead(out _, out var failure));
        Assert.Contains("map", failure);
    }

    [Fact]
    public void NoGridStillGivesEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 1, true, 1, 2, 3));
        using var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Null(state.Terrain);
        Assert.Single(state.Entities);
    }
}
