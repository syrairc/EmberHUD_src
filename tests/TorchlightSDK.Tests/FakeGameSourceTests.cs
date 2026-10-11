using System.Diagnostics;

namespace TorchlightSDK.Tests;

public class FakeGameSourceTests
{
    private static GameState At(long ticks)
    {
        using var source = new FakeGameSource(() => ticks);
        Assert.True(source.TryRead(out var state, out var failure));
        Assert.Null(failure);
        return state;
    }

    [Fact]
    public void ReportsTheMinimapRotationTheFakeOverlayLooksRightWith() => Assert.Equal(-90f, At(0).MinimapRotation);

    [Fact]
    public void SameClockGivesTheSameState() => Assert.Equal(At(5_000).Player, At(5_000).Player);

    [Fact]
    public void PlayerMovesAsTimePasses() => Assert.NotEqual(At(0).Player!.Position, At(100_000_000).Player!.Position);

    [Fact]
    public void ProvidesEveryKindTheMinimapDraws()
    {
        var state = At(0);
        foreach (var kind in new[] { EntityKind.Monster, EntityKind.Portal, EntityKind.Npc, EntityKind.Interactive })
            Assert.Contains(state.Entities, e => e.Kind == kind);
        Assert.Contains(state.Entities, e => e.Rarity == Rarity.Boss);
    }

    [Fact]
    public void EntitiesAreSortedByDrawRank()
    {
        var entities = At(0).Entities;
        Assert.Equal(entities.OrderBy(e => e.Kind).ThenBy(e => e.Rarity), entities);
        Assert.Equal(12, entities.Length);
        Assert.Equal(Enumerable.Range(1, 12), entities.Select(e => e.Id).Order());
    }

    [Fact]
    public void TerrainHasWalkableAndBlockedCells()
    {
        var terrain = At(0).Terrain!;
        var walkable = 0;
        for (var row = 0; row < terrain.Rows; row++)
            for (var col = 0; col < terrain.Cols; col++)
                if (terrain.IsWalkable(col, row)) walkable++;
        Assert.InRange(walkable, 1, terrain.Cols * terrain.Rows - 1);
        Assert.Equal(terrain.Cols * terrain.Rows, terrain.Walkable.Length);
    }

    [Fact]
    public void IconisesPortalsInteractivesAndRareAndBossMonsters()
    {
        var state = At(0);
        var iconised = state.Entities.Where(e => e.Kind is EntityKind.Portal or EntityKind.Interactive || e.Rarity is Rarity.Rare or Rarity.Boss).Select(e => e.Id).Order();

        Assert.Equal(iconised, state.MapIcons.Select(i => i.EntityId).Order());
        Assert.All(state.MapIcons, icon => Assert.NotEqual("", icon.Texture));
        Assert.Contains(state.MapIcons, i => i.Texture == "UI_MiniMap_Portal" && i.Type == 10);
        Assert.Contains(state.MapIcons, i => i.Texture == "UI_MiniMap_BOSS" && i.Type == 2);
    }

    [Fact]
    public void FakeIconsSitOnTheirEntities()
    {
        var state = At(0);

        foreach (var icon in state.MapIcons)
        {
            var entity = state.Entities.Single(e => e.Id == icon.EntityId);
            Assert.Equal(new Vector2(entity.Position.X, entity.Position.Y), icon.Position);
            Assert.Equal(1f, icon.Size);
        }
    }

    private static readonly long HalfSecond = Stopwatch.Frequency / 2;

    [Fact]
    public void TheTrailStartsOnTheFirstReadAndGrowsEveryHalfSecondOfMovement()
    {
        using var source = new FakeGameSource(() => 0);
        Assert.Equal([new Vector2(0, 0)], source.Sample(1, new Vector2(0, 0), 0).Points);
        Assert.Single(source.Sample(1, new Vector2(10, 0), HalfSecond - 1).Points);
        Assert.Equal([new Vector2(0, 0), new Vector2(10, 0)], source.Sample(1, new Vector2(10, 0), HalfSecond).Points);
    }

    [Fact]
    public void HalfAUnitOrLessOfMovementAddsNothing()
    {
        using var source = new FakeGameSource(() => 0);
        source.Sample(1, new Vector2(0, 0), 0);
        Assert.Single(source.Sample(1, new Vector2(0.5f, -0.5f), HalfSecond).Points);
        Assert.Equal(2, source.Sample(1, new Vector2(0.6f, 0), 2 * HalfSecond).Points.Length);
    }

    [Fact]
    public void TheSameTrailComesBackUntilAPointIsAdded()
    {
        using var source = new FakeGameSource(() => 0);
        var first = source.Sample(1, Vector2.Zero, 0);
        Assert.Same(first, source.Sample(1, new Vector2(50, 0), HalfSecond / 2));
        Assert.NotSame(first, source.Sample(1, new Vector2(50, 0), HalfSecond));
        Assert.Single(first.Points);
    }

    [Fact]
    public void AnAreaChangeStartsANewTrail()
    {
        using var source = new FakeGameSource(() => 0);
        source.Sample(1, Vector2.Zero, 0);
        source.Sample(1, new Vector2(50, 0), HalfSecond);
        Assert.Equal([new Vector2(70, 0)], source.Sample(2, new Vector2(70, 0), HalfSecond + 1).Points);
    }

    [Fact]
    public void TheFakeReadCarriesTheTrailWithAFewHundredUnitRadius()
    {
        var state = At(0);
        Assert.Equal([new Vector2(state.Player!.Position.X, state.Player.Position.Y)], state.Explored!.Points);
        Assert.Equal(250f, state.Explored.Radius);
    }
}
