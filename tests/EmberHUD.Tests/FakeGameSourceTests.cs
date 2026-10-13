using System.Diagnostics;
using EmberHUD.Host;

namespace EmberHUD.Tests;

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

    [Fact]
    public void TheFakeCameraLooksAtThePlayer()
    {
        new FakeGameSource(() => 0).TryRead(out var state, out _);

        var at = state.Camera!.ToScreen(state.Player!.Position, new Vector2(1920, 1080))!.Value;

        Assert.Equal(960, at.X, 0.5f);
        Assert.Equal(540, at.Y, 0.5f);
    }

    [Fact]
    public void FakeInventoryOpensAndCloses()
    {
        var second = Stopwatch.Frequency;

        var open = FakeGameSource.UiAt(1 * second).Get(PanelKind.Inventory);
        Assert.NotNull(open);
        Assert.False(open.Value.FullScreen);
        Assert.True(open.Value.Max.X > open.Value.Min.X);
        Assert.Same(GameUi.Empty, FakeGameSource.UiAt(6 * second));
        Assert.NotNull(FakeGameSource.UiAt(11 * second).Get(PanelKind.Inventory));
    }

    [Fact]
    public void FakeMapCyclesVigilAndCountsEnemiesDown()
    {
        var f = Stopwatch.Frequency;
        var start = FakeGameSource.MapAt(0);
        Assert.Equal("Rainforest of Divine Legacy", start.Name);
        Assert.Equal(78, start.MonsterLevel);
        Assert.Equal(272, start.Remaining);
        Assert.False(start.IsTown);
        Assert.Equal(ContentState.Pending, start.Content[0].State);
        Assert.Equal(ContentState.Active, FakeGameSource.MapAt(12 * f).Content[0].State);
        Assert.Equal(ContentState.Done, FakeGameSource.MapAt(25 * f).Content[0].State);
        Assert.True(FakeGameSource.MapAt(10 * f).Remaining < 272);
        Assert.Equal(2, start.Modifiers.Length);
        Assert.Equal(["Vigil", "The Devil", "The Hermit"], start.Content.Select(c => c.Name).ToArray());
        Assert.Equal((1, 2), (start.Content[1].Done, start.Content[1].Total));
    }

    [Fact]
    public void TryReadPublishesTheFakeMap()
    {
        using var source = new FakeGameSource(() => 0);
        Assert.True(source.TryRead(out var state, out _));
        Assert.Equal("Rainforest of Divine Legacy", state.Map!.Name);
    }

    [Fact]
    public void TheFakeMarketFlipsThroughThreePagesWithAShortLastPage()
    {
        var second = Stopwatch.Frequency;
        var first = FakeGameSource.TradeAt(1 * second);
        var middle = FakeGameSource.TradeAt(16 * second);
        var last = FakeGameSource.TradeAt(31 * second);
        Assert.Equal((1, false, 72, 72), (first.Page, first.LastPage, first.CellsPerPage, first.Listings.Length));
        Assert.Equal(2, middle.Page);
        Assert.Equal((3, true, 72, 50), (last.Page, last.LastPage, last.CellsPerPage, last.Listings.Length));
        Assert.Equal(1, FakeGameSource.TradeAt(46 * second).Page);
        Assert.Equal(EmberHUD.Draw.GameIconPrefix + Offsets.TradePriceTexture, first.PriceIcon);
    }

    [Fact]
    public void TheFakeMarketPagesAreStableAndLaidOutLikeTheGame()
    {
        var second = Stopwatch.Frequency;
        Assert.Equal(FakeGameSource.TradeAt(1 * second).Listings, FakeGameSource.TradeAt(10 * second).Listings);
        var cell = FakeGameSource.TradeAt(1 * second).Listings[19];
        Assert.Equal((19, new Vector2(203 + 169, 150 + 274), new Vector2(203 + 169 + 163, 150 + 274 + 267)), (cell.Slot, cell.Min, cell.Max));
        Assert.All(FakeGameSource.TradeAt(1 * second).Listings, l => Assert.True(l.Quantity > 0 && l.Price > 0 && l.Name == "Precious Ember"));
    }

    [Fact]
    public void OnEverySecondLapPageOneSlotSevenIsSold()
    {
        var second = Stopwatch.Frequency;
        Assert.Null(FakeGameSource.TradeAt(1 * second).Listings[7].Status);
        var sold = FakeGameSource.TradeAt(46 * second).Listings[7];
        Assert.Equal(("", "Sold"), (sold.Name, sold.Status));
        Assert.Null(FakeGameSource.TradeAt(91 * second).Listings[7].Status);
    }
}
