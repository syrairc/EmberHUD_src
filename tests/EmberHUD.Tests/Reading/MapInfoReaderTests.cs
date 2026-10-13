using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class MapInfoReaderTests
{
    const int Map = 4602;
    const int Town = 110;
    const int MapScene = 3;
    static readonly int TownScene = int.Parse(Offsets.TownSceneTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
    static readonly string[] VigilSpec = Offsets.MapContent.Split(';')[0].Split(',');
    static readonly int Vigil = int.Parse(VigilSpec[2]);
    const string Coffins = "There are Coffins awaiting escort in this stage";
    long _now;

    MapInfoReader ReaderFor(FakeWorld world)
    {
        var module = unchecked((nint)FakeWorld.ModuleBase);
        var minimap = new MinimapReader(world.Memory, module);
        return new(world.Memory, module, new LocText(world.Memory, module), new GameUiReader(world.Memory, module, minimap.TryReadName), minimap.TryReadName, () => _now);
    }

    internal static FakeWorld Demiman(bool withPanel = true, string[]? panelLines = null)
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + Map, "Demiman Village"),
            (Offsets.LevelNameKey + Town, "Ember's Rest"),
            (Offsets.ModifierTextKey + Vigil, Coffins));
        world.SetMapInfo(Map, MapScene, 82, 112);
        world.SetModifiers(5111003, Vigil, 71020000);
        if (withPanel) AddHud(world, panelLines ?? ["<RichText Id=\"100004\">+60</>% Drop Quantity", "-6% Max Elemental Resistance for you", Coffins]);
        return world;
    }

    static void AddHud(FakeWorld world, string[] panelLines)
    {
        var ui = new FakeUi(world);
        var hud = ui.Widget(ui.UserClass(Offsets.HudPageClassName), Offsets.HudPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(3440, 1440));
        var xp = ui.Widget(ui.PanelClass("UIShrinkTextBlock"), Offsets.MapXpWidget, Vector2.Zero, new Vector2(200, 20));
        var box = ui.Widget(ui.PanelClass("VerticalBox"), Offsets.MapAffixWidget, Vector2.Zero, new Vector2(400, 200));
        var rich = ui.UserClass("UIRichTextBlock");
        var lines = panelLines
            .Select((text, i) => { var item = ui.Widget(rich, $"UIRichTextBlock_{i + 1}", Vector2.Zero, new Vector2(400, 20)); ui.Text(item, Offsets.TextBlockText, text); return item; })
            .ToArray();
        ui.Text(xp, Offsets.ShrinkTextContent, "Map Base XP gain: 100%");
        ui.SetTree(hud, root);
        ui.SetChildren(root, xp, box);
        ui.SetChildren(box, lines);
        ui.AddRoot(hud);
    }

    [Fact]
    public void ReadsTheMapLikeTheGamePanel()
    {
        var world = Demiman();
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 36, [])!;
        Assert.Equal("Demiman Village", map.Name);
        Assert.Equal(82, map.MonsterLevel);
        Assert.Equal(Offsets.RemainingAddsAlive == 1 ? 148 : 112, map.Remaining);
        Assert.Equal("100%", map.XpGain);
        Assert.False(map.IsTown);
    }

    [Fact]
    public void VigilIsAContentRowAndItsLineIsDropped()
    {
        var world = Demiman();
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        var row = Assert.Single(map.Content);
        Assert.Equal(VigilSpec[0], row.Name);
        Assert.Equal(VigilSpec[3], row.Icon);
        Assert.Equal(["+60% Drop Quantity", "-6% Max Elemental Resistance for you"], map.Modifiers);
    }

    [Theory]
    [InlineData(0, 0, ContentState.Pending)]
    [InlineData(1, 0, ContentState.Active)]
    [InlineData(1, 1, ContentState.Done)]
    [InlineData(0x6_0000_0000, 0x6_0000_0000, ContentState.Pending)]
    [InlineData(0x6_0000_0001, 0x6_0000_0001, ContentState.Done)]
    public void VigilStateFromTheS15Vars(long started, long done, ContentState expected)
    {
        var world = Demiman();
        world.SetGameplays((8100, [(Offsets.VigilStartedVar + "_EventsMember_3BE2", started), (Offsets.VigilDoneVar + "_EventsMember_0C50", done), ("CarEtyID_EventsMember_9D8C", 62)]));
        Assert.Equal(expected, ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Content.Single().State);
    }

    [Fact]
    public void AVarNameMustEndAtTheSuffix()
    {
        var world = Demiman();
        world.SetGameplays((8100, [(Offsets.VigilDoneVar + "Time_EventsMember_1", 30)]));
        Assert.Equal(ContentState.Pending, ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Content.Single().State);
    }

    [Fact]
    public void NoContentWithoutItsModifier()
    {
        var world = Demiman();
        world.SetModifiers(5111003);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Empty(map.Content);
        Assert.Contains(Coffins, map.Modifiers);
    }

    [Fact]
    public void TownIsTown()
    {
        var world = Demiman();
        world.SetMapInfo(Town, TownScene, 2, 1);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.True(map.IsTown);
        Assert.Equal("Ember's Rest", map.Name);
    }

    [Fact]
    public void WithoutThePanelTheDataLinesStillShow()
    {
        var world = Demiman(withPanel: false);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Equal("Demiman Village", map.Name);
        Assert.Null(map.XpGain);
        Assert.Empty(map.Modifiers);
        Assert.Single(map.Content);
    }

    [Fact]
    public void UnknownLevelGivesAnEmptyName()
    {
        var world = Demiman();
        world.SetMapInfo(9999, MapScene, 50, 10);
        Assert.Equal("", ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Name);
    }

    [Fact]
    public void BrokenInfoBlockGivesNoMap()
    {
        var world = Demiman();
        world.Memory.Write(world.Scene + Offsets.SceneGeometry, 0L);
        Assert.Null(ReaderFor(world).Read((nint)world.Scene, 1, 0, []));
    }

    [Fact]
    public void ModifierCountOverCapGivesNoContent()
    {
        var world = Demiman();
        world.Memory.Write(world.Module() + Offsets.GridModuleModifierCount, (uint)Offsets.MaxModifiers + 1);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Empty(map.Content);
        Assert.Equal(82, map.MonsterLevel);
    }

    [Fact]
    public void GameplayCountOverCapLeavesVigilPending()
    {
        var world = Demiman();
        world.SetGameplays((8100, [(Offsets.VigilDoneVar + "_X", 1)]));
        world.Memory.Write(world.Scene + Offsets.SceneGameplayCount, (uint)Offsets.MaxGameplays + 1);
        Assert.Equal(ContentState.Pending, ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Content.Single().State);
    }

    [Fact]
    public void ANewAreaWalksTheHudAgainForTheNewAffixBox()
    {
        var world = Demiman();
        var reader = ReaderFor(world);
        reader.Read((nint)world.Scene, 1, 0, []);
        AddHud(world, ["+20% Drop Quantity"]);
        Assert.Equal(["+20% Drop Quantity"], reader.Read((nint)world.Scene, 2, 0, [])!.Modifiers);
    }

    [Fact]
    public void GarbageToSpawnGivesNoRemaining()
    {
        var world = Demiman();
        world.SetMapInfo(Map, MapScene, 82, Offsets.MaxRemaining + 1);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Null(map.Remaining);
        Assert.Equal(82, map.MonsterLevel);
    }

    [Fact]
    public void XpWithoutThePrefixIsNull()
    {
        Assert.Equal("100%", MapInfoReader.XpOf("Map Base XP gain: 100%"));
        Assert.Equal("1%", MapInfoReader.XpOf("Map Base XP gain: 1%"));
        Assert.Null(MapInfoReader.XpOf("no number here"));
        Assert.Null(MapInfoReader.XpOf(null));
    }

    [Fact]
    public void RefreshesOncePerIntervalAndAtOnceOnAreaChange()
    {
        var world = Demiman();
        var reader = ReaderFor(world);
        var first = reader.Read((nint)world.Scene, 1, 0, []);
        world.SetMapInfo(Map, MapScene, 82, 100);
        _now += Offsets.MapInfoRefreshMilliseconds - 1;
        Assert.Same(first, reader.Read((nint)world.Scene, 1, 0, []));
        _now += 1;
        Assert.Equal(100, reader.Read((nint)world.Scene, 1, 0, [])!.Remaining);
        world.SetMapInfo(Map, MapScene, 80, 100);
        Assert.Equal(80, reader.Read((nint)world.Scene, 2, 0, [])!.MonsterLevel);
    }

    static readonly MapIcon[] NoIcons = [];

    static MapIcon CardIcon(int entityId) => new(Offsets.TarotIconType, "UI_MiniMap_S9ICON_GP00", default, 1, 0, entityId);

    static MapIcon PortalIcon => new(11, "UI_MiniMap_Portal", default, 1, 0, 20);

    static FakeWorld TarotMap()
    {
        var world = Demiman();
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + Map, "Demiman Village"),
            (Offsets.TarotNameKey + (5011 - Offsets.TarotNameBase), "The Devil"),
            (Offsets.TarotNameKey + (5010 - Offsets.TarotNameBase), "The Hermit"));
        world.SetGameplays(
            (8100, []),
            (5000, [("finish_count _EventsMember_1", 1)]),
            (5011, [(Offsets.TarotEntityVar + "_EventsMember_A", 105)]),
            (5010, [(Offsets.TarotEntityVar + "_EventsMember_B", 106)]),
            (5011, [(Offsets.TarotEntityVar + "_EventsMember_A", 107)]));
        return world;
    }

    static readonly MapIcon[] AllCardsStanding = [PortalIcon, CardIcon(105), CardIcon(106), CardIcon(107)];

    MapInfo AfterSeeingAllCards(FakeWorld world, MapInfoReader reader, MapIcon[] icons)
    {
        reader.Read((nint)world.Scene, 1, 0, AllCardsStanding);
        _now += Offsets.MapInfoRefreshMilliseconds;
        return reader.Read((nint)world.Scene, 1, 0, icons)!;
    }

    [Fact]
    public void TarotCardsGroupByTypeWithDoneFromTheirIcons()
    {
        var world = TarotMap();
        var map = AfterSeeingAllCards(world, ReaderFor(world), [PortalIcon, CardIcon(105), CardIcon(107)]);
        var devil = map.Content.Single(c => c.Name == "The Devil");
        var hermit = map.Content.Single(c => c.Name == "The Hermit");
        Assert.Equal((0, 2, ContentState.Pending), (devil.Done, devil.Total, devil.State));
        Assert.Equal((1, 1, ContentState.Done), (hermit.Done, hermit.Total, hermit.State));
        Assert.Equal(Offsets.TarotIcon, devil.Icon);
    }

    [Fact]
    public void SomeCardsDoneIsActive()
    {
        var world = TarotMap();
        var devil = AfterSeeingAllCards(world, ReaderFor(world), [PortalIcon, CardIcon(105), CardIcon(106)]).Content.Single(c => c.Name == "The Devil");
        Assert.Equal((1, 2, ContentState.Active), (devil.Done, devil.Total, devil.State));
    }

    [Fact]
    public void AnEmptyIconListKeepsTheLastTarotCounts()
    {
        var world = TarotMap();
        var reader = ReaderFor(world);
        AfterSeeingAllCards(world, reader, [PortalIcon, CardIcon(106), CardIcon(107)]);
        _now += Offsets.MapInfoRefreshMilliseconds;
        var devil = reader.Read((nint)world.Scene, 1, 0, NoIcons)!.Content.Single(c => c.Name == "The Devil");
        Assert.Equal((1, 2), (devil.Done, devil.Total));
    }

    [Fact]
    public void ACardNeverSeenIsNotDone()
    {
        var world = TarotMap();
        var reader = ReaderFor(world);
        var cards = reader.Read((nint)world.Scene, 1, 0, [PortalIcon])!.Content.Where(c => c.Name.StartsWith("The ")).ToArray();
        Assert.All(cards, c => Assert.Equal((0, ContentState.Pending), (c.Done, c.State)));
        _now += Offsets.MapInfoRefreshMilliseconds;
        cards = reader.Read((nint)world.Scene, 1, 0, AllCardsStanding)!.Content.Where(c => c.Name.StartsWith("The ")).ToArray();
        Assert.All(cards, c => Assert.Equal(0, c.Done));
        _now += Offsets.MapInfoRefreshMilliseconds;
        var hermit = reader.Read((nint)world.Scene, 1, 0, [PortalIcon, CardIcon(105), CardIcon(107)])!.Content.Single(c => c.Name == "The Hermit");
        Assert.Equal((1, 1, ContentState.Done), (hermit.Done, hermit.Total, hermit.State));
    }

    [Fact]
    public void ANewAreaForgetsTheCardsSeenAndDone()
    {
        var world = TarotMap();
        var reader = ReaderFor(world);
        var devil = AfterSeeingAllCards(world, reader, [PortalIcon, CardIcon(106)]).Content.Single(c => c.Name == "The Devil");
        Assert.Equal(2, devil.Done);
        devil = reader.Read((nint)world.Scene, 2, 0, NoIcons)!.Content.Single(c => c.Name == "The Devil");
        Assert.Equal((0, 2), (devil.Done, devil.Total));
    }

    [Fact]
    public void TheHostAndUnknownTypesAreNotCards()
    {
        var world = TarotMap();
        var names = ReaderFor(world).Read((nint)world.Scene, 1, 0, [PortalIcon])!.Content.Select(c => c.Name).ToArray();
        Assert.Equal(["The Devil", "The Hermit"], names.Where(n => n.StartsWith("The ")).Order().ToArray());
        Assert.Equal(2, names.Length - (names.Contains(VigilSpec[0]) ? 1 : 0));
    }

    [Fact]
    public void ACardWithoutAnEntityIdCountsButIsNeverDone()
    {
        var world = TarotMap();
        world.SetGameplays((5011, [("something_else_X", 1)]));
        var devil = ReaderFor(world).Read((nint)world.Scene, 1, 0, [PortalIcon])!.Content.Single(c => c.Name == "The Devil");
        Assert.Equal((0, 1, ContentState.Pending), (devil.Done, devil.Total, devil.State));
    }

    [Fact]
    public void AnUnknownCardNameFallsBackToItsType()
    {
        var world = TarotMap();
        world.SetGameplays((5013, [(Offsets.TarotEntityVar + "_X", 200)]));
        Assert.Contains(ReaderFor(world).Read((nint)world.Scene, 1, 0, [PortalIcon])!.Content, c => c.Name == "Tarot 5013");
    }

    static readonly string[][] StatueSpecs = [.. Offsets.Statues.Split(';').Select(s => s.Split(','))];
    static readonly string[] War = StatueSpecs.Single(s => s[0].EndsWith("War"));
    static readonly string[] Machine = StatueSpecs.Single(s => s[0].EndsWith("Machine"));
    static readonly string[] Darkness = StatueSpecs.Single(s => s[0].EndsWith("Darkness"));

    const string PlaneAnchor = "This stage has been marked by a Plane Anchor";
    const string Kins = "Kins of Aeterna are wandering around on this map";
    const string TrialOfWar = "The stage has Trial: God of War";
    static readonly string[] VoidPirates = Offsets.MapContent.Split(';')[1].Split(',');
    static readonly string[] KinsSpec = Offsets.MapContent.Split(';')[2].Split(',');
    const string DarkSurge = "This stage has been completely flooded by the Dark Surge";
    static readonly string[] DarkSurgeSpec = Offsets.MapContent.Split(';')[3].Split(',');
    static readonly string[] MistvilleSpec = Offsets.MapContent.Split(';')[4].Split(',');

    static FakeWorld ClaimedLines(params string[] lines)
    {
        var world = Demiman(panelLines: lines);
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + Map, "Demiman Village"),
            (Offsets.ModifierTextKey + Vigil, Coffins),
            (VoidPirates[5], PlaneAnchor),
            (KinsSpec[5], Kins),
            (War[5], TrialOfWar),
            (War[1], "Statue of War"));
        world.SetModifiers(5111003, Vigil, int.Parse(VoidPirates[2]), int.Parse(KinsSpec[2]));
        return world;
    }

    [Fact]
    public void VoidPiratesAndKinsAreContentRows()
    {
        var world = ClaimedLines(PlaneAnchor, Kins, "<RichText Id=\"100004\">+60</>% Drop Quantity");
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Equal([VigilSpec[0], "Void Pirates", "Kins of Aeterna"], map.Content.Select(c => c.Name));
        Assert.All(map.Content.Skip(1), c => Assert.Equal((VoidPirates[3], ContentState.Pending), (c.Icon, c.State)));
    }

    [Fact]
    public void TheirGameLinesAreDropped()
    {
        var world = ClaimedLines(PlaneAnchor, Kins, "<RichText Id=\"100004\">+60</>% Drop Quantity");
        Assert.Equal(["+60% Drop Quantity"], ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Modifiers);
    }

    [Fact]
    public void DarkSurgeIsAContentRow()
    {
        var world = Demiman(panelLines: ["<RichText Id=\"100004\">+60</>% Drop Quantity", DarkSurge]);
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + Map, "Demiman Village"),
            (Offsets.ModifierTextKey + Vigil, Coffins),
            (DarkSurgeSpec[5], DarkSurge));
        world.SetModifiers(5111003, Vigil, int.Parse(DarkSurgeSpec[2]));
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Equal([VigilSpec[0], "Dark Surge"], map.Content.Select(c => c.Name));
        Assert.Equal((DarkSurgeSpec[3], ContentState.Pending), (map.Content[1].Icon, map.Content[1].State));
        Assert.Equal(["+60% Drop Quantity"], map.Modifiers);
    }

    [Fact]
    public void MistvilleResidentIsAContentRow()
    {
        var world = Demiman();
        world.SetModifiers(5111003, Vigil, int.Parse(MistvilleSpec[2]));
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.Equal([VigilSpec[0], "Mistville Resident"], map.Content.Select(c => c.Name));
        Assert.Equal((MistvilleSpec[3], ContentState.Pending), (map.Content[1].Icon, map.Content[1].State));
    }

    [Fact]
    public void VigilStillClaimsItsMapModifierLine()
    {
        var world = ClaimedLines(Coffins, "<RichText Id=\"100004\">+60</>% Drop Quantity");
        Assert.Equal(["+60% Drop Quantity"], ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Modifiers);
    }

    [Fact]
    public void AStatueRowClaimsItsTrialLine()
    {
        var world = ClaimedLines(TrialOfWar);
        var reader = ReaderFor(world);
        Assert.Equal([TrialOfWar], reader.Read((nint)world.Scene, 1, 0, [])!.Modifiers);
        AddWar(world, 300);
        _now += Offsets.MapInfoRefreshMilliseconds;
        Assert.Empty(reader.Read((nint)world.Scene, 1, 0, [])!.Modifiers);
    }

    static FakeWorld StatueMap()
    {
        var world = Demiman();
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + Map, "Demiman Village"),
            (War[1], "Statue of War"),
            (Machine[1], "Statue of Machines"));
        return world;
    }

    static long AddWar(FakeWorld world, int entityId, bool active = false) => world.AddStatue(entityId, War[0], War[3], Convert.ToInt32(War[4], 16), active);

    MapContent[] Statues(FakeWorld world, MapInfoReader reader, int area = 1)
    {
        _now += Offsets.MapInfoRefreshMilliseconds;
        return [.. reader.Read((nint)world.Scene, area, 0, [])!.Content.Where(c => c.Name.StartsWith("Statue of "))];
    }

    [Fact]
    public void AStatueIsPendingUntilActivated()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        var statue = AddWar(world, 300);
        var row = Assert.Single(Statues(world, reader));
        Assert.Equal(("Statue of War", War[2], ContentState.Pending), (row.Name, row.Icon, row.State));
        var component = new FightMapReader(world.Memory).Read((nint)(statue + Offsets.EntityComponents)).Single().Value;
        world.Memory.Write(component + Convert.ToInt32(War[4], 16), (byte)1);
        Assert.Equal(ContentState.Active, Assert.Single(Statues(world, reader)).State);
    }

    [Fact]
    public void AStatueThatLeavesAfterBeingSeenIsDone()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        AddWar(world, 300, active: true);
        Assert.Equal(ContentState.Active, Assert.Single(Statues(world, reader)).State);
        world.RemoveStatue(300);
        Assert.Equal(ContentState.Active, Assert.Single(Statues(world, reader)).State);
        var row = Assert.Single(Statues(world, reader));
        Assert.Equal(("Statue of War", ContentState.Done), (row.Name, row.State));
    }

    [Fact]
    public void OneUnreadableGamePlayMapDoesNotFinishAStatue()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        world.AddStatue(301, Machine[0]);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, reader)).State);
        world.Memory.Write(world.FightMgr + Offsets.FightMapGamePlay + Offsets.TMapNum, Offsets.MaxMapElements + 1);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, reader)).State);
        world.Memory.Write(world.FightMgr + Offsets.FightMapGamePlay + Offsets.TMapNum, 1);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, reader)).State);
        world.RemoveStatue(301);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, reader)).State);
        Assert.Equal(ContentState.Done, Assert.Single(Statues(world, reader)).State);
    }

    [Fact]
    public void ABrokenFightMgrKeepsTheLastStatueState()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        AddWar(world, 300, active: true);
        Assert.Equal(ContentState.Active, Assert.Single(Statues(world, reader)).State);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.FightMgrGlobalRva, 0L);
        for (var refresh = 0; refresh < Offsets.StatueAbsentRefreshes + 1; refresh++)
        {
            var row = Assert.Single(Statues(world, reader));
            Assert.Equal(("Statue of War", ContentState.Active), (row.Name, row.State));
        }
    }

    [Fact]
    public void AStatueNeverSeenIsNotListed()
    {
        var world = StatueMap();
        Assert.Empty(Statues(world, ReaderFor(world)));
    }

    [Fact]
    public void AStatueWithoutAnActiveByteGoesPendingToDone()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        world.AddStatue(301, Machine[0]);
        var row = Assert.Single(Statues(world, reader));
        Assert.Equal(("Statue of Machines", ContentState.Pending), (row.Name, row.State));
        world.RemoveStatue(301);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, reader)).State);
        Assert.Equal(ContentState.Done, Assert.Single(Statues(world, reader)).State);
    }

    [Fact]
    public void AnUnnamedStatueUsesItsClassName()
    {
        var world = StatueMap();
        world.AddStatue(302, Darkness[0]);
        Assert.Equal("Statue of Darkness", Assert.Single(Statues(world, ReaderFor(world))).Name);
    }

    [Fact]
    public void StatuesAreOrderedByEntityId()
    {
        var world = StatueMap();
        world.AddStatue(305, Machine[0]);
        AddWar(world, 300);
        Assert.Equal(["Statue of War", "Statue of Machines"], Statues(world, ReaderFor(world)).Select(s => s.Name));
    }

    [Fact]
    public void ANewAreaForgetsSeenStatues()
    {
        var world = StatueMap();
        var reader = ReaderFor(world);
        AddWar(world, 300);
        Assert.Single(Statues(world, reader));
        world.RemoveStatue(300);
        Assert.Empty(Statues(world, reader, area: 2));
    }

    [Fact]
    public void OtherGameplayEntitiesAreIgnored()
    {
        var world = StatueMap();
        world.AddStatue(303, "ESeasonGamePlayAltar");
        Assert.Empty(Statues(world, ReaderFor(world)));
        Assert.DoesNotContain(ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!.Content, c => c.Name.Contains("Altar"));
    }

    [Fact]
    public void ABrokenComponentMapLeavesTheStatuePending()
    {
        var world = StatueMap();
        var statue = AddWar(world, 300, active: true);
        world.Memory.Write(statue + Offsets.EntityComponents + Offsets.TMapNum, Offsets.MaxMapElements + 1);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, ReaderFor(world))).State);
        world.Memory.Write(statue + Offsets.EntityComponents + Offsets.TMapNum, 1);
        world.Memory.Write(statue + Offsets.EntityComponents, 0x7FFF_0000L);
        Assert.Equal(ContentState.Pending, Assert.Single(Statues(world, ReaderFor(world))).State);
    }

    [Fact]
    public void NoFightMgrGivesNoStatueRows()
    {
        var world = StatueMap();
        AddWar(world, 300);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.FightMgrGlobalRva, 0L);
        var map = ReaderFor(world).Read((nint)world.Scene, 1, 0, [])!;
        Assert.DoesNotContain(map.Content, c => c.Name.StartsWith("Statue of "));
    }
}
