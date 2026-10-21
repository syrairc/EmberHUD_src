using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class FrozenCanvasReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
        public readonly FakeLua Lua;
        public readonly LuaTable Manager;
        public readonly LuaTable Info;
        public readonly LuaTable Battle;
        public readonly LuaTable LevelPlots;
        public readonly LuaTable Widget;
        public LuaTable Model;
        public LuaTable DrawTable;
        public long Now;

        public Fixture(int? mainState = 4, string gameState = "Battle", int? nowNum = 0, int pos = 17, bool withManager = true, string hudText = "", double remaining = 1, bool remainingFloat = false, int[]? plotNos = null, bool withLevelPlots = true,
            string? drawPage = null, int viewState = 1, bool withModel = true, bool withDrawData = true)
        {
            Lua = new FakeLua(Memory, Module);
            Manager = Lua.NewTable(0, 3);
            Info = Lua.NewTable(4, 4);
            var battle = Battle = Lua.NewTable(0, 4);
            LevelPlots = Lua.NewTable(36, 0);
            var servants = Lua.NewTable(3, 0);
            var rewards = Lua.NewTable(2, 0);

            if (mainState is { } state) Lua.SetInt(Manager, Offsets.LuaFrozenMainState, state);
            Lua.SetString(Manager, Offsets.LuaFrozenGameState, gameState);
            Lua.SetInt(Manager, Offsets.LuaFrozenDifficulty, 101);
            Lua.SetInt(Manager, Offsets.LuaFrozenPos, pos);
            Lua.SetBool(Manager, Offsets.LuaFrozenInfoInit, true);
            Lua.SetInt(battle, Offsets.LuaFrozenFreeNum, 11);
            Lua.SetInt(battle, Offsets.LuaFrozenTargetNum, 8);
            if (nowNum is { } now) Lua.SetInt(battle, Offsets.LuaFrozenNowNum, now);
            Lua.SetInt(battle, Offsets.LuaFrozenChallengesAll, 6);
            Lua.SetInt(battle, Offsets.LuaFrozenChallengesLeft, 6);

            int[] servantNos = [20, 22, 10];
            for (var i = 0; i < servantNos.Length; i++) Lua.SetIntKey(servants, i + 1, servantNos[i], Offsets.LuaTagInt);
            Reward(rewards, 1, 330001, 10);
            Reward(rewards, 2, 330002, 3);
            Lua.SetTable(battle, Offsets.LuaFrozenServantPlots, servants);
            Lua.SetTable(battle, Offsets.LuaFrozenRewardList, rewards);
            if (withLevelPlots) Lua.SetTable(battle, Offsets.LuaFrozenLevelPlots, LevelPlots);

            for (var position = 1; position <= 36; position++)
            {
                var no = plotNos is null ? position : plotNos[position - 1];
                if (position == 17) Plot(position, no, 100203, 6, 2001);
                else if (position == 21) Plot(position, no, 100502, 9, 2001);
                else if (position == 1) Plot(position, no, null, null, 2002);
                else Plot(position, no, null, null, 3002);
            }

            Lua.SetIntKeyTable(Info, 4, battle);
            Lua.SetTable(Manager, Offsets.LuaFrozenInfo, Info);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaFrozenManager, Manager);

            Widget = Lua.NewTable(0, 2);
            Lua.SetString(Widget, Offsets.LuaFrozenHudText, hudText);
            Lua.SetInt(Widget, Offsets.LuaFrozenHudCurrent, hudText.Length > 0 ? 23 : 0);
            if (remainingFloat) Lua.SetFloat(Widget, Offsets.LuaFrozenHudRemaining, remaining);
            else Lua.SetInt(Widget, Offsets.LuaFrozenHudRemaining, (long)remaining);
            var holder = Lua.NewTable(0, 0);
            var list = Lua.NewTable(0, 0);
            var hud = Lua.NewTable(0, 0);
            Lua.SetTable(holder, Offsets.LuaFrozenHudWidget, Widget);
            Lua.SetTable(list, Offsets.LuaFrozenHudKey, holder);
            Lua.SetTable(hud, Offsets.LuaFrozenHudList, list);
            Lua.SetTable(Lua.Globals, Offsets.LuaFrozenHudManager, hud);

            var drawInfo = Lua.NewTable(0, 2);
            Lua.SetInt(drawInfo, Offsets.LuaFrozenNowNum, 11);
            Lua.SetInt(drawInfo, Offsets.LuaFrozenTargetNum, 11);
            Lua.SetIntKeyTable(Info, 3, drawInfo);
            if (drawPage is not null) DrawPage(drawPage, viewState, withModel, withDrawData);
        }

        public FrozenCanvasReader Reader(Func<string, string?>? loc = null) => new(Memory, Module, loc, () => Now);

        private void DrawPage(string key, int viewState, bool withModel, bool withDrawData)
        {
            var views = Lua.NewTable(0, 3);
            Lua.SetTable(views, "4213_S8MiniMapCtrl", Lua.NewTable(0, 0));
            Lua.SetTable(views, "5214_S8TalentCtrl", Lua.NewTable(0, 0));
            Lua.SetValue(views, "2949_SkillBagItemCtrl", 0, 0);
            Lua.SetValue(views, "2500_SkillCtrl", 0, 0);
            var page = Lua.NewTable(0, 1);
            var luaObject = Lua.NewTable(0, 1);
            var view = Lua.NewTable(0, 0);
            var model = Model = Lua.NewTable(0, 1);
            Lua.SetInt(view, Offsets.LuaFrozenViewState, viewState);
            Lua.SetTable(luaObject, Offsets.LuaFrozenView, view);
            if (withModel) Lua.SetTable(luaObject, Offsets.LuaFrozenModel, model);
            if (withDrawData) Lua.SetTable(model, Offsets.LuaFrozenDrawData, DrawTable = DrawData());
            Lua.SetTable(model, Offsets.LuaFrozenDrawUiData, UiData());
            Lua.SetTable(page, Offsets.LuaFrozenObject, luaObject);
            Lua.SetTable(views, key, page);
            var manager = Lua.NewTable(0, 0);
            Lua.SetTable(manager, Offsets.LuaFrozenViewList, views);
            Lua.SetTable(Lua.Globals, Offsets.LuaFrozenViewManager, manager);
        }

        public LuaTable DrawData()
        {
            var data = Lua.NewTable(0, 4);
            var plots = Lua.NewTable(36, 0);
            for (var position = 1; position <= 36; position++)
            {
                var row = Lua.NewTable(0, 2);
                Lua.SetString(row, Offsets.LuaFrozenPlotFinalType, position <= 11 ? "S8_Free" : position == 12 ? "S8_Internal_Block" : "S8_Nihility");
                var original = Lua.NewTable(2, 0);
                if (position == 11)
                {
                    Lua.SetInt(row, Offsets.LuaFrozenPlotFinalItem, 100203);
                    PaintRow(original, 1, 100101);
                    PaintRow(original, 2, 100203);
                }

                Lua.SetTable(row, Offsets.LuaFrozenPlotStack, original);
                Lua.SetIntKeyTable(plots, position, row);
            }

            var items = Lua.NewTable(4, 0);
            int[] bag = [100101, 100203, 200304, 100502];
            for (var i = 0; i < bag.Length; i++) PaintRow(items, i + 1, bag[i]);
            Lua.SetTable(data, Offsets.LuaFrozenDrawPlots, plots);
            Lua.SetTable(data, Offsets.LuaFrozenDrawItems, items);
            Lua.SetInt(data, Offsets.LuaFrozenDrawMaxLevel, 7);
            Lua.SetInt(data, Offsets.LuaFrozenDrawReviveMax, 1);
            Lua.SetInt(data, Offsets.LuaFrozenDrawRerollsMax, 3);
            Lua.SetBool(data, Offsets.LuaFrozenDrawCanUninstall, false);
            return data;
        }

        private LuaTable UiData()
        {
            var ui = Lua.NewTable(0, 2);
            Lua.SetString(ui, Offsets.LuaFrozenDrawMission, "<RichText>Fill a </><RichText Id=\"3902\">Diagonal</><RichText> Cell with a </><RichText Id=\"3902\">Gameplay</><RichText> Paint(0/3)</>");
            Lua.SetString(ui, Offsets.LuaFrozenDrawMissionDetail, "<RichText>Level up the paint</>");
            Lua.SetInt(ui, Offsets.LuaFrozenPaperMaxDiff, 2);
            var cards = Lua.NewTable(4, 0);
            Paper(cards, 1, 1, "White Snowpaper", 0, false, false);
            Paper(cards, 2, 2, "Green Snowpaper", 30, false, false);
            Paper(cards, 3, 3, "Blue Snowpaper", 60, true, false);
            Paper(cards, 4, 4, "Purple Snowpaper", 90, false, true);
            Lua.SetTable(ui, Offsets.LuaFrozenPaperList, cards);
            return ui;
        }

        private void Paper(LuaTable list, int index, int level, string name, int cost, bool locked, bool selected)
        {
            var row = Lua.NewTable(0, 3);
            Lua.SetInt(row, Offsets.LuaFrozenPaperLevel, level);            Lua.SetString(row, Offsets.LuaFrozenPaperName, name);
            Lua.SetString(row, Offsets.LuaFrozenPaperDesc, "<RichText>Desc " + name + "</>");
            Lua.SetInt(row, Offsets.LuaFrozenPaperCost, cost);
            Lua.SetBool(row, Offsets.LuaFrozenPaperLocked, locked);
            Lua.SetBool(row, Offsets.LuaFrozenPaperSelected, selected);
            Lua.SetIntKeyTable(list, index, row);
        }

        private void PaintRow(LuaTable list, int index, int id)
        {
            var row = Lua.NewTable(0, 0);
            Lua.SetInt(row, Offsets.LuaFrozenItemId, id);
            Lua.SetIntKeyTable(list, index, row);
        }

        public void Plot(int position, int no, int? itemId, int? wall, int tag)
        {
            var row = Lua.NewTable(0, 3);
            var tags = Lua.NewTable(1, 0);
            Lua.SetInt(row, Offsets.LuaFrozenPlotNo, no);
            if (itemId is { } id) Lua.SetInt(row, Offsets.LuaFrozenPlotItem, id);
            if (wall is { } w) Lua.SetInt(row, "wallDirection", w);
            Lua.SetIntKey(tags, 1, tag, Offsets.LuaTagInt);
            Lua.SetTable(row, Offsets.LuaFrozenPlotTags, tags);
            Lua.SetIntKeyTable(LevelPlots, position, row);
        }

        private void Reward(LuaTable list, int index, int item, int count)
        {
            var row = Lua.NewTable(0, 1);
            Lua.SetInt(row, Offsets.LuaFrozenRewardItem, item);
            Lua.SetInt(row, Offsets.LuaFrozenRewardCount, count);
            Lua.SetIntKeyTable(list, index, row);
        }
    }

    private sealed class SwapOnRead(FakeMemory inner) : EmberHUD.Host.Memory.IReadBackend
    {
        public nint Trigger;
        public Action? Fire;

        public bool TryRead(nint address, Span<byte> target)
        {
            var ok = inner.TryRead(address, target);
            if (address >= Trigger && address < Trigger + 48 && Fire is { } fire) { Fire = null; fire(); }
            return ok;
        }
    }

    [Fact]
    public void SlotOfFlipsTheWireNumber()
    {
        Assert.Equal(35, FrozenCanvasReader.SlotOf(1));
        Assert.Equal(0, FrozenCanvasReader.SlotOf(36));
        Assert.Equal(19, FrozenCanvasReader.SlotOf(17));
        Assert.Equal(-1, FrozenCanvasReader.SlotOf(0));
        Assert.Equal(-1, FrozenCanvasReader.SlotOf(37));
    }

    [Fact]
    public void PlotKindReadsNamesAndTags()
    {
        Assert.Equal(FrozenCanvasPlotKind.Free, FrozenCanvasReader.PlotKind("S8_Free", 0));
        Assert.Equal(FrozenCanvasPlotKind.Edge, FrozenCanvasReader.PlotKind("S8_Border_Block", 0));
        Assert.Equal(FrozenCanvasPlotKind.Void, FrozenCanvasReader.PlotKind(null, 2002));
        Assert.Equal(FrozenCanvasPlotKind.Unknown, FrozenCanvasReader.PlotKind("S8_Unseen", 0));
    }

    [Fact]
    public void PhaseOfMapsTheMainState()
    {
        Assert.Equal(FrozenCanvasPhase.Idle, FrozenCanvasReader.PhaseOf(1));
        Assert.Equal(FrozenCanvasPhase.Draw, FrozenCanvasReader.PhaseOf(3));
        Assert.Equal(FrozenCanvasPhase.Battle, FrozenCanvasReader.PhaseOf(4));
        Assert.Equal(FrozenCanvasPhase.Idle, FrozenCanvasReader.PhaseOf(2));
        Assert.Equal(FrozenCanvasPhase.Idle, FrozenCanvasReader.PhaseOf(99));
    }

    [Fact]
    public void ScreenOfMapsTheViewState()
    {
        Assert.Equal(FrozenCanvasScreen.Paper, FrozenCanvasReader.ScreenOf(1));
        Assert.Equal(FrozenCanvasScreen.Preview, FrozenCanvasReader.ScreenOf(3));
        Assert.Equal(FrozenCanvasScreen.Choice, FrozenCanvasReader.ScreenOf(4));
        Assert.Equal(FrozenCanvasScreen.Bag, FrozenCanvasReader.ScreenOf(5));
        Assert.Equal(FrozenCanvasScreen.Placing, FrozenCanvasReader.ScreenOf(6));
        Assert.Equal(FrozenCanvasScreen.Choice, FrozenCanvasReader.ScreenOf(7));
        Assert.Equal(FrozenCanvasScreen.Placing, FrozenCanvasReader.ScreenOf(8));
        Assert.Equal(FrozenCanvasScreen.Complete, FrozenCanvasReader.ScreenOf(9));
        Assert.Equal(FrozenCanvasScreen.Battle, FrozenCanvasReader.ScreenOf(10));
        Assert.Equal(FrozenCanvasScreen.None, FrozenCanvasReader.ScreenOf(0));
        Assert.Equal(FrozenCanvasScreen.None, FrozenCanvasReader.ScreenOf(99));
    }

    [Fact]
    public void BattleStateReadsTheRecord()
    {
        var state = new Fixture().Reader().Read()!;
        Assert.Equal(FrozenCanvasPhase.Battle, state.Phase);
        Assert.Equal(101, state.Difficulty);
        Assert.Equal(11, state.PlotsTotal);
        Assert.Equal(8, state.PlotsTarget);
        Assert.Equal(0, state.PlotsDone);
        Assert.Equal(6, state.ChallengesLeft);
        Assert.Equal(6, state.ChallengesTotal);
        Assert.Equal([new FrozenCanvasReward(330001, 10), new FrozenCanvasReward(330002, 3)], state.Rewards);
        Assert.Null(state.Draw);
    }

    [Fact]
    public void BattlePlotsAreFlippedIntoScreenSlots()
    {
        var plots = new Fixture().Reader().Read()!.Plots;
        Assert.Equal(36, plots.Length);
        var player = plots[19];
        Assert.Equal(19, player.Slot);
        Assert.Equal(100203, player.PaintId);
        Assert.Equal(3, player.Level);
        Assert.Equal(FrozenCanvasPlotKind.Free, player.Kind);
        Assert.Equal(1, player.X);
        Assert.Equal(3, player.Y);
        Assert.True(player.Player);
        Assert.False(player.Servant);
        Assert.Equal(100502, plots[15].PaintId);
        Assert.True(plots[16].Servant);
        Assert.True(plots[14].Servant);
        Assert.True(plots[26].Servant);
        Assert.Equal(0, plots[35].PaintId);
        Assert.Equal(FrozenCanvasPlotKind.Void, plots[35].Kind);
        Assert.Single(plots, p => p.Player);
    }

    [Fact]
    public void PaintNamesComeFromLocAndAreEmptyWithoutIt()
    {
        var f = new Fixture();
        var named = new FrozenCanvasReader(f.Memory, Module, key => key == Offsets.FrozenPaintNameKey + "100203" ? "Dwarf" : null, () => f.Now).Read()!.Plots;
        Assert.Equal("Dwarf", named[19].PaintName);
        Assert.Equal("", named[15].PaintName);
        Assert.Equal("", f.Reader().Read()!.Plots[19].PaintName);
    }

    [Fact]
    public void RewardStateStaysBattle()
    {
        var state = new Fixture(gameState: "Reward", nowNum: 11).Reader().Read()!;
        Assert.Equal(FrozenCanvasPhase.Battle, state.Phase);
        Assert.Equal(11, state.PlotsDone);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void PosOutsideThePlotsMeansNoPlayer(int pos)
    {
        var plots = new Fixture(pos: pos).Reader().Read()!.Plots;
        Assert.DoesNotContain(plots, p => p.Player);
    }

    [Fact]
    public void ABadPlotRowIsSkipped()
    {
        var f = new Fixture();
        f.Plot(2, 0, 100101, null, 2001);
        f.Plot(3, 40, 100102, null, 2001);
        f.Plot(30, 17, 100999, null, 2001);
        var plots = f.Reader().Read()!.Plots;
        Assert.Equal(36, plots.Length);
        Assert.Equal(100203, plots[19].PaintId);
        Assert.Equal(100502, plots[15].PaintId);
        Assert.DoesNotContain(plots, p => p.PaintId is 100101 or 100102 or 100999);
    }

    [Fact]
    public void MissingCountersReadAsZero()
    {
        var state = new Fixture(nowNum: null).Reader().Read();
        Assert.NotNull(state);
        Assert.Equal(0, state.PlotsDone);
    }

    [Theory]
    [InlineData(1, FrozenCanvasPhase.Idle)]
    [InlineData(3, FrozenCanvasPhase.Draw)]
    public void IdleAndDrawPhasesHaveNoBattlePlots(int mainState, FrozenCanvasPhase phase)
    {
        var state = new Fixture(mainState: mainState).Reader().Read()!;
        Assert.Equal(phase, state.Phase);
        Assert.Empty(state.Plots);
    }

    [Fact]
    public void ObjectiveComesFromTheHud()
    {
        var objective = new Fixture(hudText: "Defeat all monsters", remaining: 42.03, remainingFloat: true).Reader().Read()!.Objective;
        Assert.NotNull(objective);
        Assert.Equal("Defeat all monsters", objective.Description);
        Assert.Equal(23, objective.Done);
        Assert.Equal(42.03f, objective.SecondsLeft);
        Assert.Null(new Fixture(hudText: "", remaining: 1).Reader().Read()!.Objective);
    }

    [Fact]
    public void BattleWithoutTheInfoRecordIsNull()
    {
        var f = new Fixture();
        var reader = f.Reader();
        Assert.NotNull(reader.Read());
        f.Lua.ReplaceTable(f.Manager, Offsets.LuaFrozenInfo, f.Lua.NewTable(0, 0));
        f.Now = long.MaxValue / 2;
        Assert.Null(reader.Read());
    }

    [Fact]
    public void BattleWithoutLevelPlotsIsNull() => Assert.Null(new Fixture(withLevelPlots: false).Reader().Read());

    [Fact]
    public void NullWithoutTheManager() => Assert.Null(new Fixture(withManager: false).Reader().Read());

    [Fact]
    public void NullWhenMainUiStateIsMissing() => Assert.Null(new Fixture(mainState: null).Reader().Read());

    [Fact]
    public void ATornSnapshotKeepsThePreviousState()
    {
        var f = new Fixture();
        var hooked = new SwapOnRead(f.Memory);
        var reader = new FrozenCanvasReader(hooked, Module, null, () => f.Now);
        var first = reader.Read()!;
        hooked.Trigger = f.LevelPlots.Address;
        hooked.Fire = () => f.Lua.ReplaceTable(f.Battle, Offsets.LuaFrozenLevelPlots, f.Lua.NewTable(0, 0));
        f.Now = long.MaxValue / 2;
        Assert.Same(first, reader.Read());
        Assert.Null(hooked.Fire);
    }

    private const string Page = "3106_S8DrawCtrl";

    private static FrozenCanvasState DrawRead(Fixture f, Func<string, string?>? loc = null) => f.Reader(loc).Read()!;

    [Fact]
    public void DrawBoardIsReadInScreenOrder()
    {
        var plots = DrawRead(new Fixture(mainState: 3, drawPage: Page), key => key == Offsets.FrozenPaintNameKey + "100203" ? "Dwarf" : null).Plots;
        Assert.Equal(36, plots.Length);
        Assert.Equal(0, plots[0].Slot);
        Assert.Equal(FrozenCanvasPlotKind.Free, plots[0].Kind);
        Assert.Equal(FrozenCanvasPlotKind.Block, plots[11].Kind);
        Assert.Equal(FrozenCanvasPlotKind.Void, plots[35].Kind);
        Assert.Equal(11, plots.Count(p => p.Kind == FrozenCanvasPlotKind.Free));
        Assert.Equal(100203, plots[10].PaintId);
        Assert.Equal(3, plots[10].Level);
        Assert.Equal("Dwarf", plots[10].PaintName);
        Assert.Equal([100101, 100203], plots[10].Stacked.Select(p => p.Id));
        Assert.Equal(1, plots[10].Stacked[0].Level);
        Assert.Empty(plots[0].Stacked);
        Assert.Equal(4, plots[10].X);
        Assert.Equal(1, plots[10].Y);
    }

    [Theory]
    [InlineData(1, FrozenCanvasScreen.Paper)]
    [InlineData(3, FrozenCanvasScreen.Preview)]
    [InlineData(5, FrozenCanvasScreen.Bag)]
    [InlineData(8, FrozenCanvasScreen.Placing)]
    [InlineData(9, FrozenCanvasScreen.Complete)]
    [InlineData(10, FrozenCanvasScreen.Battle)]
    [InlineData(2, FrozenCanvasScreen.None)]
    [InlineData(42, FrozenCanvasScreen.None)]
    [InlineData(-1, FrozenCanvasScreen.None)]
    [InlineData(9999, FrozenCanvasScreen.None)]
    public void DrawScreenComesFromViewState(int viewState, FrozenCanvasScreen screen)
    {
        var state = DrawRead(new Fixture(mainState: 3, drawPage: Page, viewState: viewState));
        Assert.Equal(screen, state.Draw!.Screen);
    }

    [Fact]
    public void MissionTextIsReadyPlainText()
    {
        var draw = DrawRead(new Fixture(mainState: 3, drawPage: Page)).Draw!;
        Assert.Equal("Fill a Diagonal Cell with a Gameplay Paint(0/3)", draw.Mission);
        Assert.Equal("Level up the paint", draw.MissionDetail);
    }

    [Fact]
    public void CountersComeFromTheDrawData()
    {
        var draw = DrawRead(new Fixture(mainState: 3, drawPage: Page)).Draw!;
        Assert.Equal(7, draw.MaxLevel);
        Assert.Equal(0, draw.Revives);
        Assert.Equal(1, draw.ReviveMax);
        Assert.Equal(0, draw.Rerolls);
        Assert.Equal(3, draw.RerollsMax);
        Assert.Equal(0, draw.Uninstalls);
        Assert.False(draw.CanUninstall);
        Assert.Equal(11, draw.Placed);
        Assert.Equal(11, draw.Total);
        Assert.Equal([100101, 100203, 200304, 100502], draw.Bag.Select(p => p.Id));
        Assert.True(draw.Bag[0].IsMonster);
        Assert.False(draw.Bag[2].IsMonster);
        Assert.Equal(1002, draw.Bag[1].Series);
    }

    [Fact]
    public void PapersListTheChoices()
    {
        var draw = DrawRead(new Fixture(mainState: 3, drawPage: Page)).Draw!;
        Assert.Equal(4, draw.Papers.Length);
        Assert.Equal(new FrozenCanvasPaper(4, "Purple Snowpaper", "Desc Purple Snowpaper", 90, false, true), draw.Papers[3]);
        Assert.True(draw.Papers[2].Locked);
        Assert.Equal(2, draw.MaxPaper);
    }

    [Fact]
    public void NoPageMeansNoDraw()
    {
        Assert.Null(DrawRead(new Fixture(mainState: 3)).Draw);
        Assert.Empty(DrawRead(new Fixture(mainState: 3)).Plots);
        foreach (var f in new[] { new Fixture(mainState: 3, drawPage: Page, withModel: false), new Fixture(mainState: 3, drawPage: Page, withDrawData: false) })
        {
            var state = DrawRead(f);
            Assert.Null(state.Draw);
            Assert.Empty(state.Plots);
        }
    }

    [Fact]
    public void PageKeyPrefixAndNilEntriesDoNotMatter()
    {
        var draw = DrawRead(new Fixture(mainState: 3, drawPage: "77_S8DrawCtrl")).Draw!;
        Assert.Equal(FrozenCanvasScreen.Paper, draw.Screen);
        Assert.Equal(4, draw.Papers.Length);
    }

    [Fact]
    public void OtherCtrlKeysAreIgnored() => Assert.Null(DrawRead(new Fixture(mainState: 3, drawPage: "9_S8MiniMapCtrl")).Draw);

    [Fact]
    public void DrawPageAlsoReadsOnThePaperAndEnterScreens()
    {
        var paper = DrawRead(new Fixture(mainState: 1, drawPage: Page, viewState: 1));
        Assert.Equal(FrozenCanvasPhase.Idle, paper.Phase);
        Assert.Equal(FrozenCanvasScreen.Paper, paper.Draw!.Screen);
        Assert.Empty(paper.Plots);

        var enter = DrawRead(new Fixture(mainState: 4, drawPage: Page, viewState: 10));
        Assert.Equal(FrozenCanvasPhase.Battle, enter.Phase);
        Assert.Equal(FrozenCanvasScreen.Battle, enter.Draw!.Screen);
        Assert.Equal(100203, enter.Plots[19].PaintId);
    }

    [Fact]
    public void ATornDrawDataKeepsThePreviousState()
    {
        var f = new Fixture(mainState: 3, drawPage: Page, viewState: 8);
        var hooked = new SwapOnRead(f.Memory);
        var reader = new FrozenCanvasReader(hooked, Module, null, () => f.Now);
        var first = reader.Read()!;
        hooked.Trigger = f.DrawTable.Address;
        hooked.Fire = () => f.Lua.ReplaceTable(f.Model, Offsets.LuaFrozenDrawData, f.Lua.NewTable(0, 0));
        f.Now = long.MaxValue / 2;
        Assert.Same(first, reader.Read());
        Assert.Null(hooked.Fire);
    }

    [Fact]
    public void ReadIsCachedUntilTheRefreshInterval()
    {
        var f = new Fixture();
        var reader = f.Reader();
        var first = reader.Read()!;
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.FrozenRefreshMilliseconds - 1) / 1000;
        Assert.Same(first, reader.Read());
        f.Now = System.Diagnostics.Stopwatch.Frequency * Offsets.FrozenRefreshMilliseconds / 1000;
        Assert.NotSame(first, reader.Read());
    }
}
