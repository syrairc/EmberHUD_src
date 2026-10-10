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
}
