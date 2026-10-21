using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class VoraxReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
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
            Lua = new FakeLua(Memory, Module);
            View = Lua.NewTable(0, 3);
            ViewData = Lua.NewTable(0, 3);
            BaseAttr = Lua.NewTable(7, 0);
            Bag = Lua.NewTable(0, 2);
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

        public LuaTable Info(int table, int id, string nameKey, string descKey, int[] parameters, int? rarity = null)
        {
            var row = Lua.NewTable(0, 2);
            var p = Lua.NewTable(parameters.Length, 0);
            for (var i = 0; i < parameters.Length; i++) Lua.SetIntKey(p, i + 1, parameters[i], Offsets.LuaTagInt);
            Lua.SetString(row, Offsets.LuaVoraxInfoName, nameKey);
            Lua.SetString(row, Offsets.LuaVoraxInfoDesc, descKey);
            Lua.SetTable(row, Offsets.LuaVoraxInfoParams, p);
            if (rarity is { } r) Lua.SetInt(row, Offsets.LuaVoraxInfoRarity, r);
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

        public void Relic(int index, int id)
        {
            var row = Lua.NewTable(0, 1);
            Lua.SetInt(row, Offsets.LuaVoraxIndex, index);
            Lua.SetInt(row, Offsets.LuaVoraxId, id);
            Lua.SetIntKeyTable(Relics, ++_relicKey, row);
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
