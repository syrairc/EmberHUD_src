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
        public long Now;

        public Fixture(string page = "1122_S13GamePlayMainCtrl", string? state = null, bool withManager = true, bool withViewData = true, int round = 2, int rounds = 13, int rerolls = 3, int slots = 6, bool defaultMonsters = true)
        {
            state ??= Offsets.VoraxStateRound;
            Lua = new FakeLua(Memory, Module);
            View = Lua.NewTable(0, 3);
            ViewData = Lua.NewTable(0, 2);
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
