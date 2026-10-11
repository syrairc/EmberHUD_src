using TorchlightSDK.Host;
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
        Assert.Equal("live - no local player", state.Status);
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

    private static (FakeWorld World, FakeEntity Player) WorldWithPlayerAt(double x, double y)
    {
        var world = new FakeWorld();
        var player = world.AddEntity(10, 52, true, x, y, 0, mapOffset: Offsets.FightMapPlayer);
        world.PublishEntities(player, world.AddEntity(11, 48, true, 5, 5, 0));
        world.SetLocalPlayer(10);
        return (world, player);
    }

    [Fact]
    public void LocalPlayerBecomesThePlayerWithAnInitialForwardAndLeavesTheEntityList()
    {
        var (world, _) = WorldWithPlayerAt(100, 200);
        using var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Equal("live", state.Status);
        Assert.Equal(new Vector3(100, 200, 0), state.Player!.Position);
        Assert.Equal(new Vector2(0, 1), state.Player.Forward);
        Assert.Equal([11], state.Entities.Select(e => e.Id));
    }

    [Fact]
    public void ForwardFollowsMovementAndStaysWhenStill()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 0, 0);
        reader.TryRead(out var moved, out _);
        Assert.Equal(new Vector2(1, 0), moved.Player!.Forward);
        world.MoveEntity(player, 30.2, 0.1, 0);
        reader.TryRead(out var still, out _);
        Assert.Equal(new Vector2(1, 0), still.Player!.Forward);
        Assert.Equal(new Vector3(30.2f, 0.1f, 0), still.Player.Position);
        world.MoveEntity(player, 30.2, -40, 0);
        reader.TryRead(out var down, out _);
        Assert.Equal(-1, down.Player!.Forward.Y, 3);
        Assert.Equal(1, down.Player.Forward.Length(), 3);
    }

    [Fact]
    public void SlowMovementStillTurnsForwardOnceItAddsUp()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 0.3, 0, 0);
        reader.TryRead(out var first, out _);
        Assert.Equal(new Vector2(0, 1), first.Player!.Forward);
        world.MoveEntity(player, 0.6, 0, 0);
        reader.TryRead(out var second, out _);
        Assert.Equal(new Vector2(1, 0), second.Player!.Forward);
    }

    [Fact]
    public void LosingThePlayerGivesNullAndStatusNote()
    {
        var (world, _) = WorldWithPlayerAt(0, 0);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.BreakPlayerMgr();
        reader.TryRead(out var state, out _);
        Assert.Null(state.Player);
        Assert.Equal("live - no local player", state.Status);
        Assert.Equal(2, state.Entities.Length);
    }

    [Fact]
    public void ATeleportDoesNotRotateForward()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 0, 0);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 5000, 0);
        reader.TryRead(out var teleported, out _);
        Assert.Equal(new Vector2(1, 0), teleported.Player!.Forward);
        world.MoveEntity(player, 30, 5030, 0);
        reader.TryRead(out var walked, out _);
        Assert.Equal(new Vector2(0, 1), walked.Player!.Forward);
    }

    [Fact]
    public void AreaChangeTakesAFreshAnchorWithoutRotating()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 0, 0);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 100, 100, 0);
        world.SetGrid(5, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        reader.TryRead(out var changed, out _);
        Assert.Equal(new Vector2(1, 0), changed.Player!.Forward);
        world.MoveEntity(player, 100, 130, 0);
        reader.TryRead(out var walked, out _);
        Assert.Equal(new Vector2(0, 1), walked.Player!.Forward);
    }

    [Fact]
    public void LosingThePlayerResetsTheAnchor()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        using var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 0, 0);
        reader.TryRead(out _, out _);
        world.BreakPlayerMgr();
        reader.TryRead(out _, out _);
        world.SetLocalPlayer(10);
        world.MoveEntity(player, 40, 40, 0);
        reader.TryRead(out var back, out _);
        Assert.Equal(new Vector2(1, 0), back.Player!.Forward);
    }

    [Fact]
    public void MinimapRotationComesFromTheGameWidgetAndIsNullWithout()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        using var reader = ReaderFor(world);
        reader.TryRead(out var without, out _);
        Assert.Null(without.MinimapRotation);

        world.AddMinimap(0.25f);
        using var again = ReaderFor(world);
        again.TryRead(out var with, out _);
        Assert.Equal(-90f, with.MinimapRotation);
    }

    [Fact]
    public void ExploredStaysNullUntilTheProbeConfirmsTheChain()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        world.AddMinimap(0.25f);
        using var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Null(state.Explored);
    }
}
