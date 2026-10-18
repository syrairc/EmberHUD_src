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
    public void ReportsAnOpenGameMinimapThatZoomsAboutPointZeroThreeSevenAt1920()
    {
        var minimap = At(0).GameMinimap;
        Assert.True(minimap!.Open);
        Assert.InRange(1920f / minimap.WorldWidth, 0.036f, 0.038f);
    }

    [Fact]
    public void ChatIsClosedAndTheGameRunning()
    {
        var state = At(0);
        Assert.False(state.ChatOpen);
        Assert.False(state.Paused);
    }

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
        Assert.Equal(entities.Length, entities.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void EveryFakeEntityHasADistinctName()
    {
        var entities = At(0).Entities;
        Assert.All(entities, e => Assert.NotEqual("", e.Name));
        Assert.Equal(entities.Length, entities.Select(e => e.Name).Distinct().Count());
    }

    [Fact]
    public void CombatGrowsInBurstsAndPlateausEachCycle()
    {
        var second = Stopwatch.Frequency;
        var samples = Enumerable.Range(0, 61).Select(s => FakeGameSource.CombatAt(s * second)).ToArray();
        Assert.All(samples.Zip(samples.Skip(1)), pair =>
        {
            Assert.True(pair.Second.Effective >= pair.First.Effective);
            Assert.True(pair.Second.Hits >= pair.First.Hits);
        });
        Assert.All(samples, stats => Assert.True(stats.Raw >= stats.Effective));
        Assert.True(samples[^1].Effective > 0);
        for (var cycle = 0; cycle < 5; cycle++)
            Assert.Contains(Enumerable.Range(cycle * 12, 12), s => samples[s].Effective == samples[s + 1].Effective);
    }

    [Fact]
    public void TryReadPublishesTheFakeCombat()
    {
        var now = 5 * Stopwatch.Frequency;
        Assert.Equal(FakeGameSource.CombatAt(now), At(now).Combat);
    }

    [Fact]
    public void FakeInventoryIsNullWhenNobodyAsked()
    {
        Game.InventoryAsked = 0;
        Assert.Null(At(Stopwatch.GetTimestamp()).Inventory);
    }

    [Fact]
    public void FakeInventoryHasItemsInEveryBagWhenWanted()
    {
        try
        {
            Game.WantInventory();
            var inventory = At(Stopwatch.GetTimestamp()).Inventory!;

            Assert.Equal([InventoryBag.Equipment, InventoryBag.Skills, InventoryBag.Commodities, InventoryBag.Other], inventory.Select(i => i.Bag).Distinct());
            Assert.Equal(2, inventory.Count(i => i.Bag == InventoryBag.Commodities));
            Assert.Equal(inventory.Length, inventory.Select(i => (i.Bag, i.Slot)).Distinct().Count());
            Assert.All(inventory, i => Assert.NotEqual("", i.Id));
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void FakeGameSourceServesTartarusOnlyWhenAsked()
    {
        Game.TartarusAsked = 0;
        Assert.Null(At(Stopwatch.GetTimestamp()).Tartarus);
        try
        {
            Game.WantTartarus();
            Assert.NotNull(At(Stopwatch.GetTimestamp()).Tartarus);
            var board = FakeGameSource.TartarusAt(0);

            Assert.Equal(16, board.Cells.Length);
            Assert.Contains(board.Cells, c => c.Open);
            Assert.Contains(board.Cells, c => !c.Open && c.Tier < 0);
            Assert.Contains(board.Cells, c => !c.Open && c.Tier >= 0);
            Assert.Equal(TartarusChoiceKind.Doors, board.Choice!.Kind);
            Assert.Equal(3, board.Choice.Options.Length);
            Assert.Equal(TartarusChoiceKind.Operation, FakeGameSource.TartarusAt(3 * Stopwatch.Frequency).Choice!.Kind);
        }
        finally { Game.TartarusAsked = 0; }
    }

    [Fact]
    public void FakeTarotIsABoard()
    {
        Game.TarotAsked = 0;
        Assert.Null(At(Stopwatch.GetTimestamp()).Tarot);
        try
        {
            Game.WantTarot();
            Assert.NotNull(At(Stopwatch.GetTimestamp()).Tarot);
            var board = FakeGameSource.TarotAt(0);

            Assert.Equal(TarotPhase.Board, board.Phase);
            Assert.Equal(40, board.Tiles.Length);
            Assert.Equal(7, board.Tiles[30].Chest);
            Assert.Equal(0, board.Position);
            Assert.Equal(1, FakeGameSource.TarotAt(Stopwatch.Frequency).Position);
        }
        finally { Game.TarotAsked = 0; }
    }

    [Fact]
    public void FakeStashFollowsTheOpenStashPageAndIsGated()
    {
        var open = 6 * Stopwatch.Frequency;
        var bagOpen = Stopwatch.Frequency;
        try
        {
            Game.InventoryAsked = 0;
            Assert.Equal((null, null), (At(open).Stash, At(open).StashItems));

            Game.InventoryAsked = open;
            var state = At(open);
            Assert.Contains(state.Ui.Panels, p => p.Kind == PanelKind.Stash);
            Assert.Equal(Enumerable.Range(0, 35), state.Stash!.Cells.Select(c => c.Slot));
            Assert.Equal(state.Stash.Cells.Length, state.Stash.Cells.Select(c => c.Min).Distinct().Count());
            Assert.Contains(state.StashItems!, i => i.Bag == InventoryBag.Equipment);
            Assert.DoesNotContain(state.StashItems!, i => i.Slot == 3);
            Assert.All(state.StashItems!, i => Assert.Contains(state.Stash.Cells, c => c.Slot == i.Slot));

            Game.InventoryAsked = bagOpen;
            Assert.Equal((null, null), (At(bagOpen).Stash, At(bagOpen).StashItems));
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void FakeBagFollowsTheOpenInventory()
    {
        var open = Stopwatch.Frequency;
        var closed = 5 * Stopwatch.Frequency;
        try
        {
            Game.InventoryAsked = closed;
            Assert.Null(At(closed).Bag);

            Game.InventoryAsked = open;
            var bag = At(open).Bag!;
            Assert.Equal(InventoryBag.Commodities, bag.Tab);
            Assert.Equal(Enumerable.Range(0, 27), bag.Cells.Select(c => c.Slot));
            Assert.Contains(bag.Cells, c => c.Slot == 0);
            Assert.Contains(bag.Cells, c => c.Slot == 1);
            Assert.All(bag.Cells, c => Assert.True(c.Min.X >= 2400 && c.Min.Y >= 200 && c.Max.X <= 3300 && c.Max.Y <= 1200));

            Game.InventoryAsked = 0;
            Assert.Null(At(open).Bag);
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void FakeMonstersCastOnlyWhenWanted()
    {
        Game.CastsAsked = 0;
        Assert.All(At(Stopwatch.GetTimestamp()).Entities, e => Assert.Null(e.Cast));
        try
        {
            Game.WantCasts();
            var casting = At(Stopwatch.GetTimestamp()).Entities.Where(e => e.Cast is not null).ToArray();
            Assert.NotEmpty(casting);
            Assert.All(casting, e => Assert.Equal(EntityKind.Monster, e.Kind));
            Assert.All(casting, e => Assert.InRange(e.Cast!.Value.Progress, 0f, 1f));
        }
        finally { Game.CastsAsked = 0; }
    }

    [Fact]
    public void FakeGroundHazardsAreGatedByTheWant()
    {
        Game.GroundHazardsAsked = 0;
        Assert.Empty(At(Stopwatch.GetTimestamp()).GroundHazards);
        try
        {
            Game.WantGroundHazards();
            var state = At(Stopwatch.GetTimestamp());
            var player = new System.Numerics.Vector2(state.Player!.Position.X, state.Player.Position.Y);
            var area = Assert.Single(state.GroundHazards, h => h.Kind == HazardKind.Area);
            var damage = Assert.Single(state.GroundHazards, h => h.Kind == HazardKind.Damage);
            Assert.Equal(400, area.Radius);
            Assert.Equal(175, damage.Radius);
            Assert.Equal(600, System.Numerics.Vector2.Distance(area.Centre, player), 1);
            Assert.Equal(300, System.Numerics.Vector2.Distance(damage.Centre, player), 1);
        }
        finally { Game.GroundHazardsAsked = 0; }
    }

    [Fact]
    public void FakeSkillsFollowTheSameWantAsBuffs()
    {
        Game.CharacterAsked = 0;
        Assert.Null(At(Stopwatch.GetTimestamp()).Skills);
        try
        {
            Game.WantCharacter();
            var skills = At(Stopwatch.GetTimestamp()).Skills!;

            Assert.Contains(skills, s => !s.Ready);
            Assert.Contains(skills, s => s.MaxCharges > 0);
            Assert.Contains(skills, s => s.Slot < 0);
        }
        finally { Game.CharacterAsked = 0; }
    }

    [Fact]
    public void FakeBuffsAreNullWhenNobodyAsked()
    {
        Game.CharacterAsked = 0;
        Assert.Null(At(Stopwatch.GetTimestamp()).Buffs);
    }

    [Fact]
    public void FakeBuffsHaveATimedAndAnUntimedBuffWhenWanted()
    {
        try
        {
            Game.WantCharacter();
            var buffs = At(Stopwatch.GetTimestamp()).Buffs!;

            Assert.Contains(buffs, b => b.Remaining < b.Total);
            Assert.Contains(buffs, b => b.Remaining == b.Total);
            Assert.All(buffs, b => Assert.NotNull(b.Name));
        }
        finally { Game.CharacterAsked = 0; }
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
        Assert.Equal([new Vector2(0, 0)], source.Sample(1, new Vector2(0, 0), 0).PointArray());
        Assert.Single(source.Sample(1, new Vector2(10, 0), HalfSecond - 1).PointArray());
        Assert.Equal([new Vector2(0, 0), new Vector2(10, 0)], source.Sample(1, new Vector2(10, 0), HalfSecond).PointArray());
    }

    [Fact]
    public void HalfAUnitOrLessOfMovementAddsNothing()
    {
        using var source = new FakeGameSource(() => 0);
        source.Sample(1, new Vector2(0, 0), 0);
        Assert.Single(source.Sample(1, new Vector2(0.5f, -0.5f), HalfSecond).PointArray());
        Assert.Equal(2, source.Sample(1, new Vector2(0.6f, 0), 2 * HalfSecond).PointArray().Length);
    }

    [Fact]
    public void TheSameTrailComesBackUntilAPointIsAdded()
    {
        using var source = new FakeGameSource(() => 0);
        var first = source.Sample(1, Vector2.Zero, 0);
        Assert.Same(first, source.Sample(1, new Vector2(50, 0), HalfSecond / 2));
        Assert.NotSame(first, source.Sample(1, new Vector2(50, 0), HalfSecond));
        Assert.Single(first.PointArray());
    }

    [Fact]
    public void AnAreaChangeStartsANewTrail()
    {
        using var source = new FakeGameSource(() => 0);
        source.Sample(1, Vector2.Zero, 0);
        source.Sample(1, new Vector2(50, 0), HalfSecond);
        Assert.Equal([new Vector2(70, 0)], source.Sample(2, new Vector2(70, 0), HalfSecond + 1).PointArray());
    }

    [Fact]
    public void TheFakeReadCarriesTheTrailWithAFewHundredUnitRadius()
    {
        var state = At(0);
        Assert.Equal([new Vector2(state.Player!.Position.X, state.Player.Position.Y)], state.Explored!.PointArray());
        Assert.Equal(250f, state.Explored!.Radius);
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
        var samples = Enumerable.Range(0, 60).Select(s => FakeGameSource.UiAt(s * second)).ToList();

        Assert.Contains(samples, ui => ui.Panels.Any(p => p.Kind == PanelKind.Inventory));
        Assert.Contains(samples, ui => ReferenceEquals(ui, GameUi.Empty));
        Assert.Contains(samples, ui => ui.Panels.Any(p => p.Kind == PanelKind.Stash));
        Assert.All(samples.SelectMany(ui => ui.Panels).Where(p => p.Kind == PanelKind.Inventory), p =>
        {
            Assert.False(p.FullScreen);
            Assert.True(p.Max.X > p.Min.X);
        });
    }

    [Fact]
    public void FakeMapCyclesEveryContentStateAndCountsEnemiesDown()
    {
        var f = Stopwatch.Frequency;
        var start = FakeGameSource.MapAt(0);
        Assert.NotEqual("", start.Name);
        Assert.True(start.MonsterLevel > 0);
        Assert.True(start.Remaining > 0);
        Assert.False(start.IsTown);
        Assert.NotEmpty(start.Modifiers);
        Assert.All(start.Content, row => Assert.NotEqual("", row.Name));

        var states = Enumerable.Range(0, 600).Select(s => FakeGameSource.MapAt(s * f).Content[0].State).Distinct().ToList();
        Assert.Contains(ContentState.Pending, states);
        Assert.Contains(ContentState.Active, states);
        Assert.Contains(ContentState.Done, states);
        Assert.True(FakeGameSource.MapAt(10 * f).Remaining < start.Remaining);
    }

    [Fact]
    public void TryReadPublishesTheFakeMap()
    {
        using var source = new FakeGameSource(() => 0);
        Assert.True(source.TryRead(out var state, out _));
        Assert.Equal(FakeGameSource.MapAt(0).Name, state.Area!.Name);
    }

    [Fact]
    public void TheFakeMarketFlipsThroughPagesWithAShortLastPageAndWraps()
    {
        var second = Stopwatch.Frequency;
        var trades = Enumerable.Range(0, 300).Select(s => FakeGameSource.TradeAt(s * second)).ToList();
        var pages = trades.Select(t => t.Page).Distinct().Order().ToList();

        Assert.Equal(Enumerable.Range(1, pages.Count), pages);
        Assert.True(pages.Count > 1);
        var last = trades.First(t => t.LastPage);
        Assert.Equal(pages[^1], last.Page);
        Assert.True(last.Listings.Length < last.CellsPerPage);
        Assert.All(trades.Where(t => !t.LastPage), t => Assert.Equal(t.CellsPerPage, t.Listings.Length));
        Assert.Equal(1, trades[0].Page);
        Assert.Contains(trades.SkipWhile(t => !t.LastPage).SkipWhile(t => t.LastPage), t => t.Page == 1);
        Assert.Equal(EmberHUD.Draw.GameIconPrefix + Offsets.TradePriceTexture, trades[0].PriceIcon);
    }

    [Fact]
    public void TheFakeMarketPagesAreStableAndTheirListingsWellFormed()
    {
        var second = Stopwatch.Frequency;
        var first = FakeGameSource.TradeAt(1 * second).Listings;
        Assert.Equal(first, FakeGameSource.TradeAt(2 * second).Listings);
        Assert.Equal(first.Length, first.Select(l => l.Slot).Distinct().Count());
        Assert.All(first, l =>
        {
            Assert.True(l.Max.X > l.Min.X && l.Max.Y > l.Min.Y);
            Assert.True(l.Quantity > 0 && l.Price > 0);
            Assert.NotEqual("", l.Name);
        });
    }

    [Fact]
    public void QuestTargetsAreSeveralWalkableCellsAllCycle()
    {
        var terrain = At(0).Terrain!;
        Assert.True(FakeGameSource.QuestTargetsAt(0).Length >= 3);
        foreach (var second in Enumerable.Range(0, 60))
            Assert.All(FakeGameSource.QuestTargetsAt(second * Stopwatch.Frequency), target =>
                Assert.True(terrain.IsWalkable((int)(target.Position.X / terrain.CellSize), (int)(target.Position.Y / terrain.CellSize))));
    }

    [Fact]
    public void OneQuestTargetMoves()
    {
        var first = FakeGameSource.QuestTargetsAt(0);
        var later = FakeGameSource.QuestTargetsAt(10 * Stopwatch.Frequency);
        Assert.Contains(first, a => later.Any(b => b.Id == a.Id && b.Position != a.Position));
    }

    [Fact]
    public void OneQuestTargetComesAndGoes()
    {
        var present = Enumerable.Range(0, 60).Select(s => FakeGameSource.QuestTargetsAt(s * Stopwatch.Frequency).Select(t => t.Id).ToHashSet()).ToList();
        var ids = present.SelectMany(p => p).Distinct();
        Assert.Contains(ids, id => present.Any(p => p.Contains(id)) && present.Any(p => !p.Contains(id)));
    }

    [Fact]
    public void TryReadPublishesTheFakeQuestTargets()
    {
        var now = 5 * Stopwatch.Frequency;
        Assert.Equal(FakeGameSource.QuestTargetsAt(now), At(now).QuestTargets);
    }

    [Fact]
    public void SomeLapsOfPageOneHaveASoldListingAndSomeDoNot()
    {
        var second = Stopwatch.Frequency;
        var pageOne = Enumerable.Range(0, 600).Select(s => FakeGameSource.TradeAt(s * second)).Where(t => t.Page == 1).ToList();

        Assert.Contains(pageOne, t => t.Listings.Any(l => l.Status is not null));
        Assert.Contains(pageOne, t => t.Listings.All(l => l.Status is null));
    }
}
