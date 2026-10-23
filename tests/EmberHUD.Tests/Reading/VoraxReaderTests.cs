using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class VoraxReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeWorld World = new();
        public readonly FakeMemory Memory;
        public readonly FakeUi Ui;
        public readonly FakeLua Lua;
        public readonly LuaTable View;
        public readonly LuaTable ViewData;
        public readonly LuaTable BaseAttr;
        public readonly LuaTable Bag;
        public readonly LuaTable Hand;
        public readonly LuaTable Relics;
        public readonly LuaTable[] Infos = new LuaTable[5];
        public long Now;
        private int _handKey;
        private int _relicKey;

        public Fixture(string page = "1122_S13GamePlayMainCtrl", string? state = null, bool withManager = true, bool withViewData = true, int round = 2, int rounds = 13, int rerolls = 3, int slots = 6, bool defaultMonsters = true, int[]? chests = null, bool withGameManager = true)
        {
            state ??= Offsets.VoraxStateRound;
            Memory = World.Memory;
            Ui = new FakeUi(World);
            Lua = new FakeLua(Memory, Module);
            View = Lua.NewTable(0, 4);
            ViewData = Lua.NewTable(0, 3);
            BaseAttr = Lua.NewTable(7, 0);
            Bag = Lua.NewTable(0, 3);
            var obj = Lua.NewTable(0, 1);
            var pageTable = Lua.NewTable(0, 1);
            var list = Lua.NewTable(0, 2);
            var manager = Lua.NewTable(0, 1);

            Lua.SetString(View, Offsets.LuaVoraxState, state);
            Lua.SetInt(View, Offsets.LuaVoraxNextScore, 2000);
            Lua.SetInt(View, Offsets.LuaVoraxRewardCount, 2);
            Lua.SetInt(ViewData, Offsets.LuaVoraxTotalScore, 761);
            Attr(Offsets.VoraxAttrRerolls, rerolls);
            Attr(Offsets.VoraxAttrRounds, rounds);
            Attr(Offsets.VoraxAttrSlots, slots);
            Attr(Offsets.VoraxAttrRound, round);
            Lua.SetTable(ViewData, Offsets.LuaVoraxBaseAttr, BaseAttr);
            Lua.SetTable(ViewData, Offsets.LuaVoraxMonsters, Bag);
            Hand = Lua.NewTable(0, 4);
            Relics = Lua.NewTable(0, 4);
            Lua.SetTable(ViewData, Offsets.LuaVoraxHand, Hand);
            Lua.SetTable(ViewData, Offsets.LuaVoraxRelics, Relics);
            if (withGameManager) InstallGameManager(chests ?? [0, 3, 0, 0, 0, 0]);
            if (withViewData) Lua.SetTable(View, Offsets.LuaVoraxViewData, ViewData);
            Lua.SetTable(obj, Offsets.LuaVoraxView, View);
            Lua.SetTable(pageTable, Offsets.LuaVoraxObject, obj);
            Lua.SetTable(list, page, pageTable);
            Lua.SetTable(manager, Offsets.LuaVoraxViewList, list);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaVoraxViewManager, manager);

            if (!defaultMonsters) return;
            Monster(1, 0, 202, 1, 300, 4, 14);
            Monster(2, 1, 201, 24, 5, 2, 3);
            Monster(3, 2, 201, 1, 341, 2, 4);
        }

        public VoraxReader Reader(Func<string, string?>? loc = null) => new(Memory, Module, loc, () => Now);

        private void InstallGameManager(int[] chests)
        {
            var mgr = Lua.NewTable(0, 2);
            var configType = Lua.NewTable(0, 3);
            var tables = Lua.NewTable(0, 3);
            var chestTable = Lua.NewTable(chests.Length, 0);
            (int type, int index)[] config = [(101, 3), (102, 2), (103, 0), (104, 1), (105, 1), (106, 1)];
            foreach (var (type, index) in config) Lua.SetIntKey(configType, type, index, Offsets.LuaTagInt);
            for (var i = 0; i < Infos.Length; i++)
            {
                Infos[i] = Lua.NewTable(0, 4);
                var table = Lua.NewTable(0, 1);
                Lua.SetTable(table, Offsets.LuaVoraxInfos, Infos[i]);
                Lua.SetIntKeyTable(tables, i, table);
            }

            for (var i = 0; i < chests.Length; i++) Lua.SetIntKey(chestTable, i + 1, chests[i], Offsets.LuaTagInt);
            Lua.SetTable(mgr, Offsets.LuaVoraxConfigType, configType);
            Lua.SetTable(mgr, Offsets.LuaVoraxTables, tables);
            Lua.SetTable(mgr, Offsets.LuaVoraxChests, chestTable);
            Lua.SetTable(Lua.Globals, Offsets.LuaVoraxManager, mgr);
        }

        public LuaTable Info(int table, int id, string nameKey, string descKey, int[] parameters, int? rarity = null, int? scoreInc = null)
        {
            var row = Lua.NewTable(0, 3);
            var p = Lua.NewTable(parameters.Length, 0);
            for (var i = 0; i < parameters.Length; i++) Lua.SetIntKey(p, i + 1, parameters[i], Offsets.LuaTagInt);
            Lua.SetString(row, Offsets.LuaVoraxInfoName, nameKey);
            Lua.SetString(row, Offsets.LuaVoraxInfoDesc, descKey);
            Lua.SetTable(row, Offsets.LuaVoraxInfoParams, p);
            if (rarity is { } r) Lua.SetInt(row, Offsets.LuaVoraxInfoRarity, r);
            if (scoreInc is { } inc) Lua.SetInt(row, Offsets.LuaVoraxInfoScoreIncrease, inc);
            Lua.SetIntKeyTable(Infos[table], id, row);
            return row;
        }

        public void HandCard(int index, int id, int type, bool initRelic = false)
        {
            var row = Lua.NewTable(0, 2);
            Lua.SetInt(row, Offsets.LuaVoraxIndex, index);
            Lua.SetInt(row, Offsets.LuaVoraxId, id);
            Lua.SetInt(row, Offsets.LuaVoraxType, type);
            if (initRelic) Lua.SetBool(row, Offsets.LuaVoraxInitRelic, true);
            Lua.SetIntKeyTable(Hand, ++_handKey, row);
        }

        public void Protocols(params long[] ids)
        {
            var list = Lua.NewTable(ids.Length, 0);
            for (var i = 0; i < ids.Length; i++) Lua.SetIntKey(list, i + 1, ids[i], Offsets.LuaTagInt);
            Lua.SetTable(ViewData, Offsets.LuaVoraxProtocols, list);
        }

        public void Relic(int index, int id)
        {
            var row = Lua.NewTable(0, 1);
            Lua.SetInt(row, Offsets.LuaVoraxIndex, index);
            Lua.SetInt(row, Offsets.LuaVoraxId, id);
            Lua.SetIntKeyTable(Relics, ++_relicKey, row);
        }

        public void CardItem(int slot, Vector2 min, bool slate = true) => Item(Offsets.LuaVoraxCardItems, slot, Widget(World.AddClass("CardItem"), min, new Vector2(460, 568), slate));

        public void MonsterItem(int slot, Vector2 min, bool? selected, bool slate = true)
        {
            var cls = World.AddClass("MonsterItem");
            var widget = Widget(cls, min, new Vector2(100, 30), slate);
            if (selected is { } on)
            {
                var other = World.Alloc(0x100);
                Memory.Write(other + Offsets.FFieldName, World.AddName("Other"));
                Memory.Write(other + Offsets.FPropertyOffset, 0x2E0);
                var field = World.Alloc(0x100);
                Memory.Write(field + Offsets.FFieldName, World.AddName("Selected"));
                Memory.Write(field + Offsets.FPropertyOffset, 0x2F0);
                Memory.Write(other + Offsets.FFieldNext, field);
                Memory.Write(cls + Offsets.UStructChildProperties, other);
                var image = World.AddObject(World.AddClass("UIImage"), "Selected");
                Memory.Write(image + Offsets.UWidgetVisibility, (byte)(on ? Offsets.VoraxSelectedVisible : 1));
                Memory.Write(widget + 0x2F0, image);
            }

            Item(Offsets.LuaVoraxMonsterItems, slot, widget);
        }

        private nint Widget(nint cls, Vector2 min, Vector2 extent, bool slate)
        {
            var widget = World.AddObject(cls, "Item");
            if (slate) Ui.WeakPtr(widget + Offsets.UWidgetSlate, Ui.Slate(min, extent / 0.64f, 0.64f));
            return widget;
        }

        private readonly Dictionary<string, LuaTable> _lists = [];

        private void Item(string list, int slot, nint widget)
        {
            if (!_lists.TryGetValue(list, out var items))
            {
                _lists[list] = items = Lua.NewTable(8, 0);
                Lua.SetTable(View, list, items);
            }

            var row = Lua.NewTable(0, 1);
            var userdata = World.Alloc(0x100);
            Memory.Write(userdata + Offsets.VoraxRootViewWidgetOffset, widget);
            Lua.SetValue(row, Offsets.LuaVoraxRootView, userdata, Offsets.LuaTagUserdata);
            Lua.SetIntKeyTable(items, slot + 1, row);
        }

        public void Attr(int index, int value) => Lua.SetIntKey(BaseAttr, index, value, Offsets.LuaTagInt);

        public void Monster(int key, int? index, int species, int num, int score, int rarity, int? id)
        {
            var row = Lua.NewTable(0, 3);
            if (index is { } i) Lua.SetInt(row, Offsets.LuaVoraxIndex, i);
            Lua.SetInt(row, Offsets.LuaVoraxSpecies, species);
            Lua.SetInt(row, Offsets.LuaVoraxCount, num);
            Lua.SetInt(row, Offsets.LuaVoraxActivity, score);
            Lua.SetInt(row, Offsets.LuaVoraxRarity, rarity);
            if (id is { } d) Lua.SetInt(row, Offsets.LuaVoraxId, d);
            Lua.SetIntKeyTable(Bag, key, row);
        }
    }

    private static Fixture Middle(int? index, int species, int num, int score, int rarity, int? id)
    {
        var f = new Fixture(defaultMonsters: false);
        f.Monster(1, 0, 202, 1, 300, 4, 14);
        f.Monster(2, index, species, num, score, rarity, id);
        f.Monster(3, 2, 201, 1, 341, 2, 4);
        return f;
    }

    [Fact]
    public void ReadsTheRoundTable()
    {
        var s = new Fixture().Reader().Read()!;
        Assert.Equal(2, s.Round);
        Assert.Equal(13, s.Rounds);
        Assert.Equal(3, s.Rerolls);
        Assert.Equal(6, s.MonsterSlots);
        Assert.Equal(761, s.Activity);
        Assert.Equal(2000, s.NextRewardActivity);
        Assert.Equal(2, s.RewardsTotal);
    }

    [Fact]
    public void ReadsMonstersInSlotOrder()
    {
        var s = new Fixture().Reader().Read()!;
        Assert.Equal(3, s.Monsters.Length);
        Assert.Equal([0, 1, 2], s.Monsters.Select(m => m.Slot));
        Assert.Equal([1, 24, 1], s.Monsters.Select(m => m.Count));
        Assert.Equal([300, 5, 341], s.Monsters.Select(m => m.Activity));
        Assert.Equal([300, 120, 341], s.Monsters.Select(m => m.Total));
        Assert.Equal(s.Activity, s.Monsters.Sum(m => m.Total));
        Assert.Equal([14, 3, 4], s.Monsters.Select(m => m.Id));
        Assert.Equal([202, 201, 201], s.Monsters.Select(m => m.Species));
        Assert.Equal([4, 2, 2], s.Monsters.Select(m => m.Rarity));
    }

    [Fact]
    public void MonsterNamesAndRarityNamesComeFromLoc()
    {
        var names = new Dictionary<string, string> { [Offsets.VoraxMonsterNameKey + "14"] = "Bone Warden", [Offsets.VoraxRarityNameKey + "4"] = "Red" };
        var s = new Fixture().Reader(k => names.GetValueOrDefault(k)).Read()!;
        Assert.Equal("Bone Warden", s.Monsters[0].Name);
        Assert.Equal("Red", s.Monsters[0].RarityName);
        Assert.Equal("", s.Monsters[1].Name);
        Assert.Equal("", s.Monsters[1].RarityName);
    }

    [Fact]
    public void NoLocMeansEmptyText()
    {
        var m = new Fixture().Reader().Read()!.Monsters[0];
        Assert.Equal("", m.Name);
        Assert.Equal("", m.RarityName);
    }

    [Fact]
    public void PhaseIsRoundForTheNormalRoundState() => Assert.Equal(VoraxPhase.Round, new Fixture().Reader().Read()!.Phase);

    [Fact]
    public void RewardRelicStateIsTheInstrumentPhase() => Assert.Equal(VoraxPhase.Instrument, new Fixture(state: Offsets.VoraxStateRewardRelic).Reader().Read()!.Phase);

    [Fact]
    public void UnknownStateIsOther() => Assert.Equal(VoraxPhase.Other, new Fixture(state: "S13CardPlay_Unseen").Reader().Read()!.Phase);

    [Fact]
    public void PageKeyPrefixDoesNotMatter()
    {
        var s = new Fixture("77_S13GamePlayMainCtrl").Reader().Read()!;
        Assert.Equal(2, s.Round);
        Assert.Equal(3, s.Monsters.Length);
    }

    [Fact]
    public void NullWithoutViewManager() => Assert.Null(new Fixture(withManager: false).Reader().Read());

    [Fact]
    public void NullWithoutAPageThatEndsInTheSuffix() => Assert.Null(new Fixture("1096_S13GamePlayCtrl").Reader().Read());

    [Fact]
    public void NullWithoutViewData() => Assert.Null(new Fixture(withViewData: false).Reader().Read());

    [Fact]
    public void AMonsterRowMissingAFieldIsSkipped()
    {
        var f = Middle(1, 201, 24, 5, 2, null);
        Assert.Equal([0, 2], f.Reader().Read()!.Monsters.Select(m => m.Slot));
    }

    [Fact]
    public void AMonsterRowWithoutAnIndexFallsBackToItsPosition()
    {
        var f = Middle(null, 201, 24, 5, 2, 3);
        Assert.Equal([0, 1, 2], f.Reader().Read()!.Monsters.Select(m => m.Slot));
    }

    [Theory]
    [InlineData(4, -3)]
    [InlineData(2, 0)]
    [InlineData(4, 0)]
    public void GarbageNumbersRejectTheSnapshot(int index, int value)
    {
        var f = new Fixture();
        f.Attr(index, value);
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void ARoundPastTheLastRoundRejectsTheSnapshot() => Assert.Null(new Fixture(round: 14).Reader().Read());

    [Fact]
    public void ARowWithANegativeCountIsSkipped()
    {
        var f = Middle(1, 201, -1, 5, 2, 3);
        Assert.Equal([0, 2], f.Reader().Read()!.Monsters.Select(m => m.Slot));
    }

    [Fact]
    public void AnOverflowingRowIsSkipped()
    {
        var f = Middle(1, 201, 2_000_000_000, 5, 2, 3);
        Assert.Equal([0, 2], f.Reader().Read()!.Monsters.Select(m => m.Slot));
    }

    [Fact]
    public void ATornSnapshotKeepsThePreviousState()
    {
        var f = new Fixture();
        var hooked = new SwapOnRead(f.Memory);
        var reader = new VoraxReader(hooked, Module, null, () => f.Now);
        var first = reader.Read()!;
        hooked.Trigger = f.ViewData.Address;
        hooked.Fire = () => f.Lua.ReplaceTable(f.View, Offsets.LuaVoraxViewData, f.Lua.NewTable(0, 0));
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
        f.Lua.UpdateInt(f.ViewData, Offsets.LuaVoraxTotalScore, 900);
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.VoraxRefreshMilliseconds - 1) / 1000;
        Assert.Same(first, reader.Read());
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.VoraxRefreshMilliseconds + 1) / 1000;
        Assert.Equal(900, reader.Read()!.Activity);
    }

    private const string GrayTemplate = "<RichText>+{%s:2} </><Img Id=\"0058\"/><RichText>Activity to {%s:1} random group(s) of monster and </><RichText Id=\"100077\" Udl=\"1\" HyperLinkType=\"1\" EventId=\"13000004\">mutate</><RichText> them into random monsters. There is a {%s:3}%% chance to </><RichText Id=\"100077\" Udl=\"1\" HyperLinkType=\"1\" EventId=\"13000004\">mutate</><RichText> them again into </><Img Id=\"0061\"/><RichText>Aemberons</><RichText> and +{%s:4} to their </><Img Id=\"0058\"/><RichText>Activity</>";
    private const string GrayText = "+41 Activity to 1 random group(s) of monster and mutate them into random monsters. There is a 50% chance to mutate them again into Aemberons and +30 to their Activity";

    private static Dictionary<string, string> Texts() => new()
    {
        ["relic|name|101"] = "Spinal Tap",
        ["relic|desc|101"] = "+{%s:1} </><Img Id=\"0058\"/><RichText>Activity</> to all monsters",
        ["effect|name|202"] = "Gray Matter Spinal Solution",
        ["effect|desc|202"] = GrayTemplate,
        ["effect|name|102"] = "Plain Solution",
        ["effect|desc|102"] = "Plain",
        ["effect|name|205"] = "Other Solution",
        ["effect|desc|205"] = "Other",
        [Offsets.VoraxTypeNamePrefix + "Func_S13CardGame_CardTypeEffect"] = "Effect",
        [Offsets.VoraxTypeNamePrefix + "Func_S13CardGame_CardTypeRelic"] = "Instrument",
    };

    private static Fixture Cards()
    {
        var f = new Fixture();
        f.Info(2, 202, "effect|name|202", "effect|desc|202", [1, 41, 50, 30], 2);
        f.Info(2, 102, "effect|name|102", "effect|desc|102", [], 1);
        f.Info(2, 205, "effect|name|205", "effect|desc|205", [], 3);
        f.Info(0, 101, "relic|name|101", "relic|desc|101", [35]);
        f.HandCard(0, 202, 102);
        f.HandCard(1, 102, 102);
        f.HandCard(2, 205, 102);
        f.Relic(0, 101);
        return f;
    }

    private static Func<string, string?> LocOf(Dictionary<string, string> texts) => k => texts.GetValueOrDefault(k);

    [Fact]
    public void OfferedPotionsCarryNameDescriptionAndRarity()
    {
        var s = Cards().Reader(LocOf(Texts())).Read()!;
        Assert.Equal(3, s.Offered.Length);
        var card = s.Offered[0];
        Assert.Equal(VoraxCardKind.Potion, card.Kind);
        Assert.Equal(0, card.Slot);
        Assert.Equal(202, card.Id);
        Assert.Equal(2, card.Rarity);
        Assert.Equal("Gray Matter Spinal Solution", card.Name);
        Assert.Equal(GrayText, card.Description);
        Assert.Equal("Effect", card.TypeName);
        Assert.Equal([202, 102, 205], s.Offered.Select(c => c.Id));
    }

    [Fact]
    public void OwnedInstrumentsComeFromTheRelicBag()
    {
        var s = Cards().Reader(LocOf(Texts())).Read()!;
        var card = Assert.Single(s.Instruments);
        Assert.Equal(101, card.Id);
        Assert.Equal(VoraxCardKind.Instrument, card.Kind);
        Assert.StartsWith("+35 Activity to all monsters", card.Description);
        Assert.Equal("Spinal Tap", card.Name);
        Assert.Equal("Instrument", card.TypeName);
    }

    [Fact]
    public void InitRelicRowsMakeTheInstrumentPhase()
    {
        var f = Cards();
        f.Info(0, 150, "relic|name|101", "relic|desc|101", [1]);
        f.HandCard(3, 150, 102, true);
        var s = f.Reader(LocOf(Texts())).Read()!;
        Assert.Equal(VoraxPhase.Instrument, s.Phase);
        Assert.Equal(VoraxCardKind.Instrument, s.Offered[3].Kind);
        Assert.Equal(VoraxCardKind.Potion, s.Offered[0].Kind);
    }

    [Fact]
    public void WithoutInitRelicRowsThePhaseStaysRound() => Assert.Equal(VoraxPhase.Round, Cards().Reader(LocOf(Texts())).Read()!.Phase);

    [Fact]
    public void UnknownTypeStaysUnknownAndEmpty()
    {
        var f = Cards();
        f.HandCard(3, 202, 999);
        var s = f.Reader(LocOf(Texts())).Read()!;
        var card = s.Offered[3];
        Assert.Equal(VoraxCardKind.Unknown, card.Kind);
        Assert.Equal("", card.Name);
        Assert.Equal("", card.Description);
        Assert.Equal("Gray Matter Spinal Solution", s.Offered[0].Name);
    }

    [Fact]
    public void MissingInfoRowGivesAnEmptyCard()
    {
        var f = Cards();
        f.HandCard(3, 777, 102);
        var card = f.Reader(LocOf(Texts())).Read()!.Offered[3];
        Assert.Equal(777, card.Id);
        Assert.Equal("", card.Name);
        Assert.Equal("", card.Description);
        Assert.Equal(0, card.Rarity);
    }

    [Fact]
    public void NullLocLookupGivesEmptyText()
    {
        var texts = Texts();
        texts.Remove("effect|name|202");
        var card = Cards().Reader(LocOf(texts)).Read()!.Offered[0];
        Assert.Equal("", card.Name);
        Assert.Equal(GrayText, card.Description);
    }

    private static Fixture WithProtocols(params long[] ids)
    {
        var f = Cards();
        f.Info(1, 3, "mod|name|3", "mod|desc|3", [20], 1, 20);
        f.Info(1, 4, "mod|name|4", "mod|desc|4", [], 2, 15);
        f.Protocols(ids);
        return f;
    }

    private static Dictionary<string, string> ProtocolTexts()
    {
        var texts = Texts();
        texts["mod|name|3"] = "Spinal Cord Stimulation";
        texts["mod|desc|3"] = "+{%s:1}%% reward";
        texts["mod|name|4"] = "Second";
        texts["mod|desc|4"] = "Other";
        texts[Offsets.VoraxTypeNamePrefix + "Func_S13CardGame_CardTypeBattleModifier"] = "Surgical Protocol";
        return texts;
    }

    [Fact]
    public void ProtocolsResolveLikeModifierCards()
    {
        var s = WithProtocols(3, 4).Reader(LocOf(ProtocolTexts())).Read()!;
        Assert.Equal([3, 4], s.Protocols.Select(c => c.Id));
        Assert.Equal([0, 1], s.Protocols.Select(c => c.Slot));
        var card = s.Protocols[0];
        Assert.Equal(VoraxCardKind.Modifier, card.Kind);
        Assert.Equal("Spinal Cord Stimulation", card.Name);
        Assert.Equal("+20% reward", card.Description);
        Assert.Equal(1, card.Rarity);
    }

    [Fact]
    public void RewardBonusIsTheSumOfScoreInc()
    {
        Assert.Equal(35, WithProtocols(3, 4).Reader(LocOf(ProtocolTexts())).Read()!.RewardBonusPercent);
        Assert.Equal(20, WithProtocols(3).Reader(LocOf(ProtocolTexts())).Read()!.RewardBonusPercent);
    }

    [Fact]
    public void NoProtocolListMeansNoProtocolsAndNoBonus()
    {
        var s = Cards().Reader(LocOf(ProtocolTexts())).Read()!;
        Assert.Empty(s.Protocols);
        Assert.Equal(0, s.RewardBonusPercent);
    }

    [Fact]
    public void AnEmptyProtocolListIsEmpty()
    {
        var s = WithProtocols().Reader(LocOf(ProtocolTexts())).Read()!;
        Assert.Empty(s.Protocols);
        Assert.Equal(0, s.RewardBonusPercent);
    }

    [Fact]
    public void GarbageProtocolEntriesAreSkipped()
    {
        var s = WithProtocols(3, long.MaxValue, 4).Reader(LocOf(ProtocolTexts())).Read()!;
        Assert.Equal([3, 4], s.Protocols.Select(c => c.Id));
        Assert.Equal(35, s.RewardBonusPercent);
    }

    [Fact]
    public void ABonusSumThatOverflowsIsClamped()
    {
        var f = Cards();
        f.Info(1, 3, "mod|name|3", "mod|desc|3", [20], 1, int.MaxValue);
        f.Info(1, 4, "mod|name|4", "mod|desc|4", [], 2, int.MaxValue);
        f.Protocols(3, 4);
        Assert.Equal(int.MaxValue, f.Reader(LocOf(ProtocolTexts())).Read()!.RewardBonusPercent);
    }

    [Fact]
    public void ChestsAreReadFromTheManager()
    {
        var s = Cards().Reader(LocOf(Texts())).Read()!;
        Assert.Equal([0, 3, 0, 0, 0, 0], s.Chests);
        Assert.Equal(1, s.RewardsTaken);
    }

    [Fact]
    public void NoGameManagerMeansNoChestsButTheRestStillReads()
    {
        var s = new Fixture(withGameManager: false).Reader().Read()!;
        Assert.Empty(s.Chests);
        Assert.Equal(0, s.RewardsTaken);
        Assert.Equal(3, s.Monsters.Length);
        Assert.Empty(s.Offered);
    }

    [Fact]
    public void DescriptionsAreCached()
    {
        var f = Cards();
        var texts = Texts();
        var info = f.Info(2, 300, "effect|name|202", "effect|desc|202", [1, 41, 50, 30], 2);
        f.HandCard(3, 300, 102);
        var reader = f.Reader(LocOf(texts));
        Assert.Equal("Gray Matter Spinal Solution", reader.Read()!.Offered[3].Name);
        f.Lua.UpdateString(info, Offsets.LuaVoraxInfoName, "effect|name|102");
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.VoraxRefreshMilliseconds + 1) / 1000;
        Assert.Equal("Gray Matter Spinal Solution", reader.Read()!.Offered[3].Name);
    }

    [Fact]
    public void EmptyTextIsNotCached()
    {
        var f = Cards();
        var texts = new Dictionary<string, string>();
        var reader = f.Reader(LocOf(texts));
        var first = reader.Read()!.Offered[0];
        Assert.Equal("", first.Name);
        Assert.Equal("", first.Description);
        foreach (var (k, v) in Texts()) texts[k] = v;
        f.Now = System.Diagnostics.Stopwatch.Frequency * (Offsets.VoraxRefreshMilliseconds + 1) / 1000;
        var second = reader.Read()!.Offered[0];
        Assert.Equal("Gray Matter Spinal Solution", second.Name);
        Assert.Equal(GrayText, second.Description);
    }

    [Fact]
    public void MoreCardsThanTheCapAreTruncated()
    {
        var f = Cards();
        for (var i = 3; i < Offsets.MaxVoraxCards + 3; i++) f.HandCard(i, 202, 102);
        Assert.Equal(Offsets.MaxVoraxCards, f.Reader(LocOf(Texts())).Read()!.Offered.Length);
    }

    [Fact]
    public void MonsterCardsGetTheirNameFromTheMonsterLocKey()
    {
        var f = Cards();
        f.HandCard(3, 14, 101);
        var texts = Texts();
        texts[Offsets.VoraxMonsterNameKey + "14"] = "Bone Warden";
        var card = f.Reader(LocOf(texts)).Read()!.Offered[3];
        Assert.Equal(VoraxCardKind.Monster, card.Kind);
        Assert.Equal("Bone Warden", card.Name);
        Assert.Equal("", card.Description);
    }

    [Fact]
    public void MonstersAreSortedBySlotNotTableKey()
    {
        var f = new Fixture(defaultMonsters: false);
        f.Monster(1, 2, 201, 1, 341, 2, 4);
        f.Monster(2, 0, 202, 1, 300, 4, 14);
        f.Monster(3, 1, 201, 24, 5, 2, 3);
        Assert.Equal([0, 1, 2], f.Reader().Read()!.Monsters.Select(m => m.Slot));
    }

    [Fact]
    public void CardsAreSortedBySlotNotTableKey()
    {
        var f = new Fixture();
        f.HandCard(2, 205, 102);
        f.HandCard(0, 202, 102);
        f.HandCard(1, 102, 102);
        Assert.Equal([0, 1, 2], f.Reader().Read()!.Offered.Select(c => c.Slot));
    }

    [Fact]
    public void OfferedCardsGetTheirRects()
    {
        var f = Cards();
        f.CardItem(0, new Vector2(1483.5f, 978));
        f.CardItem(1, new Vector2(1704, 963));
        f.CardItem(2, new Vector2(1925, 978));
        var cards = f.Reader(LocOf(Texts())).Read()!.Offered;
        Assert.All(cards, c => Assert.True(c.HasRect));
        Assert.Equal([1483.5, 1704, 1925], cards.Select(c => (double)c.Min.X));
        Assert.Equal([978, 963, 978], cards.Select(c => (double)c.Min.Y));
        Assert.Equal(460, cards[1].Max.X - cards[1].Min.X, 2);
        Assert.Equal(568, cards[1].Max.Y - cards[1].Min.Y, 2);
    }

    [Fact]
    public void RectReadFailureKeepsTheCard()
    {
        var f = Cards();
        f.CardItem(0, new Vector2(1483.5f, 978), slate: false);
        f.CardItem(1, new Vector2(1704, 963));
        var cards = f.Reader(LocOf(Texts())).Read()!.Offered;
        Assert.Equal(3, cards.Length);
        Assert.False(cards[0].HasRect);
        Assert.Equal("Gray Matter Spinal Solution", cards[0].Name);
        Assert.True(cards[1].HasRect);
        Assert.False(cards[2].HasRect);
    }

    [Fact]
    public void ARectIsRelativeToTheOverlayOrigin()
    {
        var f = Cards();
        f.CardItem(0, new Vector2(1500, 1000));
        var reader = f.Reader(LocOf(Texts()));
        reader.Origin = () => new Vector2(100, 40);
        var card = reader.Read()!.Offered[0];
        Assert.Equal(1400, card.Min.X, 2);
        Assert.Equal(960, card.Min.Y, 2);
    }

    [Fact]
    public void NoOriginMeansNoRects()
    {
        var f = Cards();
        f.CardItem(0, new Vector2(1500, 1000));
        var reader = f.Reader(LocOf(Texts()));
        reader.Origin = () => null;
        Assert.False(reader.Read()!.Offered[0].HasRect);
    }

    [Theory]
    [InlineData(207, 0, 0)]
    [InlineData(207, 2, 2)]
    [InlineData(-1, 0, -1)]
    public void SelectedCardFollowsCardId(int cardId, int cardIndex, int expected)
    {
        var f = Cards();
        f.Lua.SetInt(f.View, Offsets.LuaVoraxCardId, cardId);
        f.Lua.SetInt(f.View, Offsets.LuaVoraxCardIndex, cardIndex);
        Assert.Equal(expected, f.Reader(LocOf(Texts())).Read()!.SelectedCard);
    }

    [Fact]
    public void WithoutCardIdNothingIsSelected() => Assert.Equal(-1, Cards().Reader(LocOf(Texts())).Read()!.SelectedCard);

    [Fact]
    public void MonstersGetTheirRects()
    {
        var f = new Fixture();
        f.MonsterItem(0, new Vector2(836, 597), null);
        f.MonsterItem(1, new Vector2(900, 600), null, slate: false);
        var monsters = f.Reader().Read()!.Monsters;
        Assert.True(monsters[0].HasRect);
        Assert.Equal(836, monsters[0].Min.X, 2);
        Assert.Equal(100, monsters[0].Max.X - monsters[0].Min.X, 2);
        Assert.False(monsters[1].HasRect);
        Assert.False(monsters[2].HasRect);
    }

    [Fact]
    public void SelectedGroupsAreTheVisibleSelectedImages()
    {
        var f = new Fixture(defaultMonsters: false);
        for (var slot = 0; slot < 6; slot++)
        {
            f.Monster(slot + 1, slot, 201, 1, 10, 2, slot + 1);
            f.MonsterItem(slot, new Vector2(800 + slot * 100, 600), slot is 0 or 4);
        }

        Assert.Equal([0, 4], f.Reader().Read()!.SelectedMonsters);
    }

    [Fact]
    public void NoSelectedImageMeansNoGroups()
    {
        var f = new Fixture();
        for (var slot = 0; slot < 3; slot++) f.MonsterItem(slot, new Vector2(800 + slot * 100, 600), null);
        Assert.Empty(f.Reader().Read()!.SelectedMonsters);
    }

    [Fact]
    public void NoItemListsMeansNoRectsAndNoSelection()
    {
        var s = Cards().Reader(LocOf(Texts())).Read()!;
        Assert.All(s.Offered, c => Assert.False(c.HasRect));
        Assert.All(s.Monsters, m => Assert.False(m.HasRect));
        Assert.Empty(s.SelectedMonsters);
        Assert.Equal(-1, s.SelectedCard);
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
}
