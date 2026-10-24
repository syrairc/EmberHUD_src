using System.Numerics;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class LiveReaderTests
{
    private static LiveReader ReaderFor(FakeWorld world) => new(world.Memory, FakeWorld.Module);

    [Fact]
    public void ReadsTerrainEntitiesAndAreaFromTheFakeWorld()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(5, 77, true, 50, 60, 0));
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out var failure));
        Assert.Null(failure);
        Assert.Equal(1, state.AreaId);
        Assert.NotNull(state.Terrain);
        Assert.Equal(77, Assert.Single(state.Entities).CfgId);
        Assert.Null(state.Player);
        Assert.Equal("live - no local player - entities 1, map icons 0, explored off", state.Status);
    }

    [Fact]
    public void MonstersWithAShownHpBarCarryTheGameBar()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        var ui = new FakeUi(world, new Vector2(100, 50));
        var monster = world.AddEntity(5, 77, true, 0, 0, 0, monsterCfgId: 77) with { Value = world.Alloc(0x800) };
        ui.AddHpBar(monster, Offsets.HpBarNormalClassName, Offsets.HpBarNormalCanvas, Offsets.HpBarNormalTrack, new Vector2(1745.5f, 499.1f), new Vector2(255.36f, 23.04f), 0.2464f, 0.383f);
        world.PublishEntities(monster);
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        var entity = Assert.Single(state.Entities);
        Assert.True(entity.HasGameBar);
        Assert.Equal(new Vector2(1645.5f, 449.1f), entity.GameBar!.Value.Min);
    }

    [Fact]
    public void MonstersWithoutAHpBarHaveNoGameBar()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(5, 77, true, 0, 0, 0));
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.False(Assert.Single(state.Entities).HasGameBar);
    }

    [Fact]
    public void WithoutAUiChainTheUiIsEmptyAndTheTradeIsNull()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Same(GameUi.Empty, state.Ui);
        Assert.Null(state.Trade);
        Assert.Null(state.Compare);
    }

    [Fact]
    public void KeepsTheSameTerrainInstanceAcrossReads()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
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
        var reader = new LiveReader(world.Memory, FakeWorld.Module, 5);
        reader.TryRead(out var state, out _);
        Assert.Equal(6, state.AreaId);
    }

    [Fact]
    public void PublishesTheCombatSnapshotWhenGivenACombatReader()
    {
        var world = new FakeWorld();
        world.SetLocalPlayer(15);
        world.PublishEntities();
        var combat = new CombatReader(world.Memory, FakeWorld.Module, () => 7);
        combat.Poll();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, 0, combat);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.NotNull(combat.Snapshot);
        Assert.Same(combat.Snapshot, state.Combat);
    }

    [Fact]
    public void MonstersCarryTheirCastWhenWanted()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(5, 77, true, 50, 60, 0);
        world.PublishEntities(monster);
        world.AddCast(monster, 170203, 1, 0.5, 1);
        Game.WantCasts();
        try
        {
            Assert.True(ReaderFor(world).TryRead(out var state, out _));
            Assert.Equal(170203, Assert.Single(state.Entities).Cast?.StoneId);
        }
        finally { Game.CastsAsked = 0; }
    }

    [Fact]
    public void CastsAreNotReadWhenNobodyAsked()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(5, 77, true, 50, 60, 0);
        world.PublishEntities(monster);
        world.AddCast(monster, 170203, 1, 0.5, 1);
        Game.CastsAsked = 0;
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.Null(Assert.Single(state.Entities).Cast);
    }

    [Fact]
    public void CombatIsNullWithoutACombatReader()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.Null(state.Combat);
    }

    [Fact]
    public void PublishesTheInventoryWhenWantedAndGivenAReader()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var f = new Reading.InventoryReaderTests.Fixture(true, world.Memory, FakeWorld.Module);
        f.Lua.SetIntKeyTable(f.Bag(2, 1), 1, f.Item("5_a", 5, 3, 9));
        Game.WantInventory();
        try
        {
            Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, f.Reader()).TryRead(out var state, out _));
            var item = Assert.Single(state.Inventory!);
            Assert.Equal(("5_a", 3), (item.Id, item.Count));
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void InventoryIsNullWhenNobodyAsked()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        Game.InventoryAsked = 0;
        Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, new InventoryReader(world.Memory, FakeWorld.Module)).TryRead(out var state, out _));
        Assert.Null(state.Inventory);
    }

    [Fact]
    public void StashIsNullWhenNobodyAskedOrNoStashPageIsOpen()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var inventory = new InventoryReader(world.Memory, FakeWorld.Module);
        Game.InventoryAsked = 0;
        Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, inventory).TryRead(out var state, out _));
        Assert.Equal((null, null), (state.Stash, state.StashItems));
        Game.WantInventory();
        try
        {
            Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, inventory).TryRead(out state, out _));
            Assert.Equal((null, null), (state.Stash, state.StashItems));
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void OpenStashPublishesViewAndItemsOfTheSameTab()
    {
        var stash = GameUiReaderStashTests.Open([1, 4, 1]);
        stash.World.PublishEntities();
        var f = new Reading.InventoryReaderTests.Fixture(true, stash.World.Memory, FakeWorld.Module);
        var tab0 = f.Lua.NewTable(0, 3);
        f.Lua.SetIntKeyTable(tab0, 0, f.Item("1_a", 1, 1, 1));
        f.Lua.SetIntKeyTable(f.Bags, Offsets.StashTabFirstKey, tab0);
        var tab1 = f.Lua.NewTable(0, 3);
        f.Lua.SetIntKeyTable(tab1, 9, f.Item("2_b", 2, 4, 9));
        f.Lua.SetIntKeyTable(f.Bags, Offsets.StashTabFirstKey + 1, tab1);
        Game.WantInventory();
        try
        {
            Assert.True(new LiveReader(stash.World.Memory, FakeWorld.Module, 0, null, f.Reader()).TryRead(out var state, out _));
            Assert.Equal(1, state.Stash!.Tab);
            var item = Assert.Single(state.StashItems!);
            Assert.Equal(("2_b", 9), (item.Id, item.Slot));
            Assert.Contains(state.Stash.Cells, c => c.Slot == item.Slot);
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void InventoryIsNullWithoutAReader()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        Game.WantInventory();
        try
        {
            Assert.True(ReaderFor(world).TryRead(out var state, out _));
            Assert.Null(state.Inventory);
        }
        finally { Game.InventoryAsked = 0; }
    }

    [Fact]
    public void TartarusIsOnlyReadWhenAsked()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var f = new Reading.TartarusReaderTests.Fixture();
        f.Cell(2, "P7", 101, 205);
        var reader = new LiveReader(world.Memory, FakeWorld.Module, 0, null, null, null, f.Reader());
        try
        {
            Game.TartarusAsked = 0;
            Assert.True(reader.TryRead(out var idle, out _));
            Assert.Null(idle.Tartarus);

            Game.WantTartarus();
            Assert.True(reader.TryRead(out var asked, out _));
            Assert.Equal(16, asked.Tartarus!.Size);
            Assert.Equal(2, Assert.Single(asked.Tartarus.Cells).Index);
        }
        finally { Game.TartarusAsked = 0; }
    }

    [Fact]
    public void TarotOnlyWhileWanted()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, tarot: Reading.TarotReaderTests.Fixture.Board(hp: 3).Reader());
        try
        {
            Game.TarotAsked = 0;
            Assert.True(reader.TryRead(out var idle, out _));
            Assert.Null(idle.Tarot);

            Game.WantTarot();
            Assert.True(reader.TryRead(out var asked, out _));
            Assert.Equal(TarotPhase.Board, asked.Tarot!.Phase);
            Assert.Equal(3, asked.Tarot.Hp);
        }
        finally { Game.TarotAsked = 0; }
    }

    [Fact]
    public void VoraxIsNullUntilAPluginAsks()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, vorax: origin => new Reading.VoraxReaderTests.Fixture().Reader(origin: origin));
        Game.VoraxAsked = 0;
        Assert.True(reader.TryRead(out var idle, out _));
        Assert.Null(idle.Vorax);
    }

    [Fact]
    public void VoraxIsReadAfterWantVorax()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, vorax: origin => new Reading.VoraxReaderTests.Fixture().Reader(origin: origin));
        try
        {
            Game.VoraxAsked = 0;
            Game.WantVorax();
            Assert.True(reader.TryRead(out var asked, out _));
            Assert.Equal(VoraxPhase.Round, asked.Vorax!.Phase);
            Assert.Equal(2, asked.Vorax.Round);
        }
        finally { Game.VoraxAsked = 0; }
    }

    [Fact]
    public void FrozenCanvasIsNullUntilAPluginAsks()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, frozenCanvas: new Reading.FrozenCanvasReaderTests.Fixture().Reader());
        Game.FrozenCanvasAsked = 0;
        Assert.True(reader.TryRead(out var idle, out _));
        Assert.Null(idle.FrozenCanvas);
    }

    [Fact]
    public void FrozenCanvasIsReadAfterWantFrozenCanvas()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = new LiveReader(world.Memory, FakeWorld.Module, frozenCanvas: new Reading.FrozenCanvasReaderTests.Fixture().Reader());
        try
        {
            Game.FrozenCanvasAsked = 0;
            Game.WantFrozenCanvas();
            Assert.True(reader.TryRead(out var asked, out _));
            Assert.Equal(FrozenCanvasPhase.Battle, asked.FrozenCanvas!.Phase);
            Assert.Equal(11, asked.FrozenCanvas.PlotsTotal);
        }
        finally { Game.FrozenCanvasAsked = 0; }
    }

    [Fact]
    public void GroundItemsAreOnlyReadWhenAsked()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        world.AddDropMgr([new FakeNameTagGroup(1, new(5, 6, 7), new(500, 400), [new FakeNameTagLabel(3, new(10, -20), new(110, 10))])]);
        var f = new Reading.GroundItemReaderTests.Fixture(world: world);
        f.Drop(3, [106060407], []);
        var reader = new LiveReader(world.Memory, FakeWorld.Module, 0, null, null, null, null, f.Reader());
        try
        {
            Game.GroundItemsAsked = 0;
            Assert.True(reader.TryRead(out var idle, out _));
            Assert.Null(idle.GroundItems);

            Game.WantGroundItems();
            Assert.True(reader.TryRead(out var asked, out _));
            Assert.Equal(3, Assert.Single(asked.GroundItems!).DropId);
        }
        finally { Game.GroundItemsAsked = 0; }
    }

    private static CharacterReader CharacterWith(FakeWorld world)
    {
        world.SetLocalPlayer(11);
        world.AddFightEntity(11, (Offsets.ComponentBuff, world.AddBuffComponent(world.AddBuff(15010, 1, 1))));
        return new CharacterReader(world.Memory, FakeWorld.Module);
    }

    [Fact]
    public void PublishesBuffsWhenWantedAndGivenAReader()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var character = CharacterWith(world);
        Game.WantCharacter();
        try
        {
            Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, null, character).TryRead(out var state, out _));
            Assert.Equal(15010, Assert.Single(state.Buffs!).Id);
        }
        finally { Game.CharacterAsked = 0; }
    }

    [Fact]
    public void BuffsAreNullWhenNobodyAsked()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var character = CharacterWith(world);
        Game.CharacterAsked = 0;
        Assert.True(new LiveReader(world.Memory, FakeWorld.Module, 0, null, null, character).TryRead(out var state, out _));
        Assert.Null(state.Buffs);
    }

    [Fact]
    public void BuffsAreNullWithoutAReader()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        Game.WantCharacter();
        try
        {
            Assert.True(ReaderFor(world).TryRead(out var state, out _));
            Assert.Null(state.Buffs);
        }
        finally { Game.CharacterAsked = 0; }
    }

    [Fact]
    public void ChatOpenIsNullWithoutAUiWalk()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.Null(state.ChatOpen);
    }

    [Fact]
    public void ChatOpenIsFalseOnACleanWalkWithoutTheChatPage()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        new FakeUi(world).AddPad([1, 2]);
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.False(state.ChatOpen);
    }

    [Fact]
    public void ChatOpenIsTrueWhileTheChatPageIsUp()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var ui = new FakeUi(world);
        ui.AddPad([1, 2]);
        ui.AddChat();
        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.True(state.ChatOpen);
        Assert.False(state.Ui.AnyFullScreen);
    }

    private static (FakeWorld World, FakeUi Ui, LiveReader Reader, Func<long> Now, Action<long> Advance) ChatRig()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        long now = 1000;
        return (world, new FakeUi(world), new LiveReader(world.Memory, FakeWorld.Module, clockMilliseconds: () => now), () => now, by => now += by);
    }

    private static bool? ChatOf(LiveReader reader)
    {
        reader.TryRead(out var state, out _);
        return state.ChatOpen;
    }

    [Fact]
    public void ChatOpenHoldsTrueForTheWindowAfterTheChatPageVanishes()
    {
        var (world, ui, reader, _, advance) = ChatRig();
        ui.AddPad([1, 2]);
        var chat = ui.AddChat();
        Assert.True(ChatOf(reader));
        world.Memory.Write(chat + Offsets.UWidgetVisibility, (byte)1);
        advance(Offsets.ChatHoldMilliseconds - 1);
        Assert.True(ChatOf(reader));
        advance(1);
        Assert.False(ChatOf(reader));
    }

    [Fact]
    public void TheHoldIsNotExtendedByTicksWithoutTheChatPage()
    {
        var (world, ui, reader, _, advance) = ChatRig();
        ui.AddPad([1, 2]);
        var chat = ui.AddChat();
        ChatOf(reader);
        world.Memory.Write(chat + Offsets.UWidgetVisibility, (byte)1);
        advance(200);
        Assert.True(ChatOf(reader));
        advance(100);
        Assert.False(ChatOf(reader));
    }

    [Fact]
    public void ChatWithAFailedRectReadIsStillOpenOnTheFirstTick()
    {
        var (world, ui, reader, _, _) = ChatRig();
        ui.AddPad([1, 2]);
        var chat = ui.AddChat();
        world.Memory.Write(chat + Offsets.UWidgetSlate, 0L);
        reader.TryRead(out var state, out _);
        Assert.True(state.ChatOpen);
        Assert.DoesNotContain(state.Ui.Panels, p => p.Name == Offsets.ChatPageClassName);
    }

    [Fact]
    public void ChatWithAnUnreadableVisibilityIsNeverClosed()
    {
        var (world, ui, reader, _, advance) = ChatRig();
        ui.AddPad([1, 2]);
        var chat = ui.AddChat();
        world.Memory.MakeUnreadable(chat + Offsets.UWidgetVisibility, 1);
        foreach (var step in new long[] { 0, 499, 1, 1500 })
        {
            advance(step);
            Assert.NotEqual(false, ChatOf(reader));
        }
    }

    [Fact]
    public void ARootThatFailsToResolveMakesChatUnknownNotClosed()
    {
        var (_, ui, reader, _, _) = ChatRig();
        ui.AddPad([1, 2]);
        ui.AddRoot(0);
        Assert.Null(ChatOf(reader));
    }

    [Fact]
    public void AnUnresolvedRootInsideTheHoldWindowOfASeenChatIsOpen()
    {
        var (world, ui, reader, _, advance) = ChatRig();
        var chat = ui.AddChat();
        Assert.True(ChatOf(reader));
        world.Memory.Write(chat + Offsets.UWidgetVisibility, (byte)1);
        ui.AddRoot(0);
        advance(Offsets.ChatHoldMilliseconds - 1);
        Assert.True(ChatOf(reader));
    }

    [Fact]
    public void AnInconclusiveTickHoldsTheNextCleanNoChatTicksForTheWindow()
    {
        var (world, ui, reader, _, advance) = ChatRig();
        ui.AddPad([1, 2]);
        Assert.False(ChatOf(reader));
        var slots = ui.Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum;
        world.Memory.Write(slots, 0);
        Assert.Null(ChatOf(reader));
        world.Memory.Write(slots, 1);
        advance(Offsets.ChatHoldMilliseconds - 1);
        Assert.True(ChatOf(reader));
        advance(1);
        Assert.False(ChatOf(reader));
    }

    [Fact]
    public void PausedFollowsTheFrameCounterAndTheClock()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        world.SetFightFrame(100);
        long now = 1000;
        var reader = new LiveReader(world.Memory, FakeWorld.Module, clockMilliseconds: () => now);
        reader.TryRead(out var first, out _);
        Assert.Null(first.Paused);
        now += Offsets.PausedAfterMilliseconds;
        reader.TryRead(out var still, out _);
        Assert.True(still.Paused);
        world.SetFightFrame(101);
        reader.TryRead(out var moving, out _);
        Assert.False(moving.Paused);
    }

    [Fact]
    public void PausedAndChatAreNullWhenTheWorldIsNotLoaded()
    {
        var world = new FakeWorld();
        world.BreakScenePointer();
        Assert.False(ReaderFor(world).TryRead(out var state, out _));
        Assert.Null(state.Paused);
        Assert.Null(state.ChatOpen);
    }

    [Fact]
    public void NoSceneIsAFailureWithTheReason()
    {
        var world = new FakeWorld();
        world.BreakScenePointer();
        var reader = ReaderFor(world);
        Assert.False(reader.TryRead(out _, out var failure));
        Assert.Contains("map", failure);
    }

    [Fact]
    public void NoGridStillGivesEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 1, true, 1, 2, 3));
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Equal("live - entities 1, map icons 0, explored off, scans 1", state.Status);
        Assert.Equal(new Vector3(100, 200, 0), state.Player!.Position);
        Assert.Equal(new Vector2(0, 1), state.Player.Forward);
        Assert.Equal([11], state.Entities.Select(e => e.Id));
    }

    [Fact]
    public void PlayerPositionReadsAloneWithoutWalkingTheScene()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 300, -40, 0);
        Assert.True(reader.TryReadPlayerPosition(out var position));
        Assert.Equal(300, position.X);
        Assert.Equal(-40, position.Y);
    }

    [Fact]
    public void ForwardFollowsMovementAndStaysWhenStill()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.BreakPlayerMgr();
        reader.TryRead(out var state, out _);
        Assert.Null(state.Player);
        Assert.Equal("live - no local player - entities 2, map icons 0, explored off, scans 1", state.Status);
        Assert.Equal(2, state.Entities.Length);
    }

    [Fact]
    public void ATeleportDoesNotRotateForward()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
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
        var reader = ReaderFor(world);
        reader.TryRead(out var without, out _);
        Assert.Null(without.MinimapRotation);

        world.AddMinimap(0.25f);
        var again = ReaderFor(world);
        again.TryRead(out var with, out _);
        Assert.Equal(-90f, with.MinimapRotation);
    }

    [Fact]
    public void PublishesTheQuestTargetsAndNoneWithoutThem()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        ReaderFor(world).TryRead(out var without, out _);
        Assert.Empty(without.QuestTargets);

        world.SetQuestTargets(world.AddMinimap(0f), 7, (1, 3450f, -1710f), (2, -8040f, -4950f));
        ReaderFor(world).TryRead(out var with, out _);
        Assert.Equal([new QuestTarget(1, new Vector2(3450, -1710)), new QuestTarget(2, new Vector2(-8040, -4950))], with.QuestTargets);
    }

    [Fact]
    public void PublishesTheGamesExploredTrailAndCountsItInTheStatus()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var minimap = world.AddMinimap(0.25f);
        world.SetLevel(110, 0);
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [new Vector3(1, 2, 3)]));
        var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Equal([new Vector2(1, 2)], state.Explored!.PointArray());
        Assert.Contains("explored 1", state.Status);
    }

    [Fact]
    public void PublishesTheGamesMapIconsAtTheLiveEntityPosition()
    {
        var (world, _) = WorldWithPlayerAt(0, 0);
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 3, new FakeIcon(1040, 1, 2, EntityId: 11), new FakeIcon(10, 7, 8));
        var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Equal([new MapIcon(1040, "", new Vector2(5, 5), 1, 0, 11), new MapIcon(10, "", new Vector2(7, 8), 1, 0, 0)], state.MapIcons);
    }

    [Theory]
    [InlineData(0f, 1f, 0f)]
    [InlineData(90f, 0f, 1f)]
    [InlineData(180f, -1f, 0f)]
    public void PlayerForwardComesFromTheGamesHeading(float heading, float x, float y)
    {
        var (world, _) = WorldWithPlayerAt(0, 0);
        var minimap = world.AddMinimap(0f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapHeading, heading);
        var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.Equal(x, state.Player!.Forward.X, 4);
        Assert.Equal(y, state.Player.Forward.Y, 4);
    }

    [Fact]
    public void StateCarriesTheGameMinimapOrNullWithoutAWidget()
    {
        var (world, _) = WorldWithPlayerAt(0, 0);
        var none = ReaderFor(world);
        none.TryRead(out var without, out _);
        Assert.Null(without.GameMinimap);

        var minimap = world.AddMinimap(0f);
        world.SetGameMinimap(minimap, 1, 6.2086f, 34304);
        var reader = ReaderFor(world);
        reader.TryRead(out var state, out _);
        Assert.True(state.GameMinimap!.Open);
        Assert.Equal(6.2086f * 34304 * 0.64f / 1.92f, state.GameMinimap.WorldWidth, 0.01f);
    }

    [Fact]
    public void WithoutAHeadingForwardStillFollowsMovement()
    {
        var (world, player) = WorldWithPlayerAt(0, 0);
        var minimap = world.AddMinimap(0f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapHeading, float.NaN);
        var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.MoveEntity(player, 30, 0, 0);
        reader.TryRead(out var moved, out _);
        Assert.Equal(new Vector2(1, 0), moved.Player!.Forward);
    }

    [Fact]
    public void TwoEntitiesWithTheSameIdDoNotBreakTheRead()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(5, 48, true, 1, 1, 0), world.AddEntity(5, 48, true, 2, 2, 0));
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(1040, 9, 9, EntityId: 5));
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Equal(1040, Assert.Single(state.MapIcons).Type);
    }

    [Fact]
    public void AFollowIconForAnEntityInNoKindMapGetsItsScenePosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(72, 67, true, 30, 40, 0, mapOffset: FakeWorld.Unregistered));
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 108);
        world.SetFollowIcons(minimap, (72, 2642u, 108u));
        var reader = ReaderFor(world);

        reader.TryRead(out var state, out _);

        Assert.Equal([new MapIcon(2642, "", new Vector2(30, 40), 1, 0, 72)], state.MapIcons);
    }

    [Fact]
    public void TheCameraComesFromTheLocalPawn()
    {
        var world = new FakeWorld();
        var (pawn, _) = world.SetLocalPlayer(5);
        var controller = world.Alloc(0x400);
        var manager = world.Alloc(0x2000);
        world.Memory.Write(pawn + Offsets.PawnController, controller);
        world.Memory.Write(controller + Offsets.ControllerCameraManager, manager);
        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 75f);

        ReaderFor(world).TryRead(out var state, out _);

        Assert.Equal(75, state.Camera?.Fov);
    }

    [Fact]
    public void PublishesTheMapInfo()
    {
        var world = FakeMapInfo.Demiman();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.SetMapInfo(4602, 3, 82, 112);
        world.PublishEntities(world.AddEntity(5, 77, true, 50, 60, 0));
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Equal(82, state.Area!.MonsterLevel);
        Assert.NotNull(state.Terrain);
    }

    [Fact]
    public void ABrokenInfoBlockLeavesNoMapButEntitiesStillPublish()
    {
        var world = FakeMapInfo.Demiman();
        world.PublishEntities(world.AddEntity(5, 77, true, 50, 60, 0));
        world.Memory.Write(world.Scene + Offsets.SceneGeometry, 0L);
        var reader = ReaderFor(world);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Null(state.Area);
        Assert.Single(state.Entities);
    }

    [Fact]
    public void ThePawnIsResolvedOncePerTick()
    {
        var world = new FakeWorld();
        var (pawn, _) = world.SetLocalPlayer(5);
        var controller = world.Alloc(0x400);
        var manager = world.Alloc(0x2000);
        world.Memory.Write(pawn + Offsets.PawnController, controller);
        world.Memory.Write(controller + Offsets.ControllerCameraManager, manager);
        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 75f);
        var reader = ReaderFor(world);
        world.Memory.Reads.Clear();

        reader.TryRead(out var state, out _);

        Assert.Equal(75, state.Camera?.Fov);
        Assert.Single(world.Memory.Reads, r => r.Address == FakeWorld.Module + Offsets.PlayerMgrGlobalRva);
    }

    [Fact]
    public void TheSceneGeometryIsResolvedOncePerTick()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities();
        var minimap = world.AddMinimap(0.25f);
        world.SetLevel(110, 0);
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [new Vector3(1, 2, 3)]));
        var reader = ReaderFor(world);
        reader.TryRead(out _, out _);
        world.Memory.Reads.Clear();

        reader.TryRead(out _, out _);

        Assert.Single(world.Memory.Reads, r => r.Address == world.Scene + Offsets.SceneGeometry);
    }

    [Fact]
    public void TheStatusCountsTheFallbackScansOnlyWhenThereWereAny()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = ReaderFor(world);
        reader.TryRead(out var without, out _);
        Assert.DoesNotContain("scans", without.Status);

        world.AddMinimap(0.25f);
        var again = ReaderFor(world);
        again.TryRead(out var with, out _);
        Assert.EndsWith("scans 1", with.Status);
    }

    private static (FakeWorld World, nint Component) WorldWithNamedPlayer(string name)
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer));
        var (pawn, _) = world.SetLocalPlayer(10);
        var component = world.AddComponents(pawn, Offsets.PlayerComponentClassName)[0];
        SetPlayerName(world, component, name);
        return (world, component);
    }

    private static void SetPlayerName(FakeWorld world, nint component, string name)
    {
        var chars = world.Alloc(0x100);
        world.Memory.WriteArray(chars, System.Text.Encoding.Unicode.GetBytes(name + "\0"));
        world.Memory.Write(component + Offsets.PlayerComponentName, chars);
        world.Memory.Write(component + Offsets.PlayerComponentName + Offsets.FStringNum, name.Length + 1);
        world.Memory.Write(component + Offsets.PlayerComponentName + Offsets.FStringMax, name.Length + 1);
    }

    [Fact]
    public void ThePlayerCarriesTheCharacterName()
    {
        var (world, _) = WorldWithNamedPlayer("Terayen#8674");

        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.Equal("Terayen#8674", state.Player!.Name);
    }

    [Fact]
    public void ThePlayerNameIsNullWhenTheComponentIsMissing()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer));
        world.SetLocalPlayer(10);

        Assert.True(ReaderFor(world).TryRead(out var state, out _));
        Assert.NotNull(state.Player);
        Assert.Null(state.Player.Name);
    }

    [Fact]
    public void ThePlayerNameIsReadOncePerRefreshWindow()
    {
        var (world, component) = WorldWithNamedPlayer("Terayen#8674");
        var now = 100_000L;
        var reader = new LiveReader(world.Memory, FakeWorld.Module, clockMilliseconds: () => now);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Equal("Terayen#8674", state.Player!.Name);

        SetPlayerName(world, component, "Other#1");
        now += Offsets.PlayerNameRefreshMilliseconds - 1;
        Assert.True(reader.TryRead(out state, out _));
        Assert.Equal("Terayen#8674", state.Player!.Name);

        now += 1;
        Assert.True(reader.TryRead(out state, out _));
        Assert.Equal("Other#1", state.Player!.Name);
    }}
