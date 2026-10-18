using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class StateToolsTests
{
    static readonly Tool[] Tools = StateTools.Create(() => new Status("http://127.0.0.1:50902/mcp", 2, 1500, 40, 1, true));

    static void Use(params Entity[] entities) => TestGame.Set(new GameState { Status = "playing", AreaId = 12, Player = new Player(Vector3.Zero, Vector2.UnitX), Entities = entities });

    static JsonArray Entities(JsonObject args) => Wire.Run(Tools, "get_entities", args)["entities"]!.AsArray();

    [Test]
    public void GetStateListsProjectiles()
    {
        TestGame.Set(GameState.Empty with { Projectiles = [new Projectile(7, 3, Rarity.Boss, new Vector2(1, 2), new Vector2(0, 0), Vector2.UnitX, 250, 550)] });
        var projectiles = Wire.Run(Tools, "get_state")["projectiles"]!;
        Check.Equal(1, (int)projectiles["count"]!);
        var first = projectiles["first"]![0]!;
        Check.Equal(7, (int)first["id"]!);
        Check.Equal(3, (int)first["ownerId"]!);
        Check.Equal("Boss", (string?)first["rarity"]);
        Check.Equal(250.0, (double)first["radius"]!);
    }

    [Test]
    public void GetStateShowsTheTradePage()
    {
        TestGame.Set(GameState.Empty with { Trade = new TradePage(2, false, 72, [new TradeListing(3, "Precious Ember", 3712, 297, null, new Vector2(1, 2), new Vector2(3, 4)), new TradeListing(7, "", 1442, 115, "Sold", Vector2.Zero, Vector2.One)], "game:fe") });
        var trade = Wire.Run(Tools, "get_state")["trade"]!;
        Check.Equal(2, (int)trade["page"]!);
        Check.Equal(72, (int)trade["cellsPerPage"]!);
        Check.Equal(2, (int)trade["count"]!);
        Check.Equal(297L, (long)trade["listings"]![0]!["price"]!);
        Check.Equal("Sold", (string?)trade["listings"]![1]!["status"]);
        TestGame.Set(GameState.Empty);
        Check.That(Wire.Run(Tools, "get_state")["trade"] is null, "no market, no trade");
    }

    [Test]
    public void GetStateIncludesBag()
    {
        var cells = Enumerable.Range(0, 27).Select(i => new BagCell(i, new Vector2(i, 2), new Vector2(i + 1, 3))).ToArray();
        TestGame.Set(GameState.Empty with { Bag = new BagView(InventoryBag.Commodities, new Vector2(10, 20), new Vector2(300, 400), cells) });
        var bag = Wire.Run(Tools, "get_state")["bag"]!;
        Check.Equal("Commodities", (string?)bag["tab"]);
        Check.Equal("10,20", string.Join(',', bag["clip"]!["min"]!.AsArray().Select(v => (double)v!)), "clip min");
        Check.Equal("300,400", string.Join(',', bag["clip"]!["max"]!.AsArray().Select(v => (double)v!)), "clip max");
        Check.Equal(27, (int)bag["cells"]!);
        Check.Equal(10, bag["first"]!.AsArray().Count);
        Check.Equal(9, (int)bag["first"]![9]!["slot"]!);
        Check.Equal("9,2", string.Join(',', bag["first"]![9]!["min"]!.AsArray().Select(v => (double)v!)), "min");
        Check.Equal("10,3", string.Join(',', bag["first"]![9]!["max"]!.AsArray().Select(v => (double)v!)), "max");
        TestGame.Set(GameState.Empty);
        Check.That(Wire.Run(Tools, "get_state")["bag"] is null, "no bag, no key");
    }

    [Test]
    public void GetStateListsTheFirstTenQuestTargets()
    {
        TestGame.Set(GameState.Empty with { QuestTargets = [.. Enumerable.Range(1, 12).Select(i => new QuestTarget(i, new Vector2(i, -i)))] });
        var targets = Wire.Run(Tools, "get_state")["questTargets"]!;
        Check.Equal(12, (int)targets["count"]!);
        Check.Equal(10, targets["first"]!.AsArray().Count);
        Check.Equal(3, (int)targets["first"]![2]!["id"]!);
        Check.Equal(-3d, (double)targets["first"]![2]!["y"]!, "y");
    }

    [Test]
    public void GetStateGroupsMapIconsByTypeWithTheirEntityIds()
    {
        TestGame.Set(GameState.Empty with { MapIcons = [new MapIcon(2407, "", Vector2.Zero, 1, 0, 103) { Name = "E_S9kapai" }, new MapIcon(11, "", Vector2.Zero, 1, 0, 0), new MapIcon(2407, "", Vector2.Zero, 1, 0, 102) { Name = "E_S9kapai" }] });
        var icons = Wire.Run(Tools, "get_state")["mapIconTypes"]!.AsArray();
        Check.Equal(2, icons.Count);
        Check.Equal(11, (int)icons[0]!["type"]!);
        Check.Equal("E_S9kapai", (string?)icons[1]!["name"]);
        Check.Equal("[102,103]", icons[1]!["entityIds"]!.ToJsonString());
    }

    [Test]
    public void StatusReportsServerAndGame()
    {
        TestMemory.Clear();
        Use();
        var status = Wire.Run(Tools, "get_status");
        Check.Equal(false, (bool)status["memoryAttached"]!);
        Check.Equal("playing", (string?)status["gameStatus"]);
        Check.Equal(12, (int)status["areaId"]!);
        Check.Equal(40L, (long)status["msSinceStateChange"]!);
        Check.Equal("http://127.0.0.1:50902/mcp", (string?)status["server"]!["url"]);
        Check.Equal(2, (int)status["server"]!["inFlight"]!);
        Check.Equal(1, (int)status["watches"]!);
        Check.Equal(true, (bool)status["recording"]!);
    }

    [Test]
    public void StateCountsEntitiesAndShowsPlayerAndCamera()
    {
        var state = new GameState
        {
            Status = "playing",
            AreaId = 12,
            Player = new Player(new Vector3(1, 2, 3), new Vector2(0, 1)) { Life = 100.5, MaxLife = 200, Shield = 30, MaxShield = 40 },
            Entities = [Mob(1, 5, 10), Mob(2, 5, 20, rarity: Rarity.Rare), Mob(3, 9, 30, EntityKind.Portal)],
            Camera = new Camera(new Vector3(1, 2, 3), new Vector3(-45, 90, 0), 80),
        };
        TestGame.Set(state);
        var result = Wire.Run(Tools, "get_state");
        Check.Equal(3, (int)result["entityCount"]!);
        Check.Equal(2, (int)result["byKind"]!["Monster"]!);
        Check.Equal(1, (int)result["byKind"]!["Portal"]!);
        Check.Equal(1, (int)result["byRarity"]!["Rare"]!);
        Check.Equal(2, (int)result["byRarity"]!["Normal"]!);
        Check.Equal(3d, (double)result["player"]!["z"]!, "z");
        Check.Equal(1d, (double)result["player"]!["forwardY"]!, "forwardY");
        Check.Equal(100.5, (double)result["player"]!["life"]!, "life");
        Check.Equal(40d, (double)result["player"]!["maxShield"]!, "maxShield");
        Check.Equal(80d, (double)result["camera"]!["fov"]!, "fov");
        Check.Equal(90d, (double)result["camera"]!["yaw"]!, "yaw");
        Check.Equal(false, (bool)result["hasTerrain"]!);
    }

    [Test]
    public void StateShowsTheGameMinimapOrNull()
    {
        TestGame.Set(GameState.Empty with { GameMinimap = new GameMinimap(true, 51892f) });
        var shown = Wire.Run(Tools, "get_state")["gameMinimap"]!;
        Check.Equal(true, (bool)shown["open"]!, "open");
        Check.Equal(51892d, (double)shown["worldWidth"]!, "worldWidth");
        TestGame.Set(GameState.Empty);
        Check.That(Wire.Run(Tools, "get_state")["gameMinimap"] is null, "null when absent");
    }

    [Test]
    public void PanelPartsAreListedOnlyWhenPresent()
    {
        TestGame.Set(GameState.Empty with
        {
            Ui = new GameUi([
                new GamePanel("S16BoardRelicPop_C", PanelKind.Other, Vector2.Zero, new Vector2(3440, 1440), false) { Parts = [new NamedRect("S16BoardRelicOptCell_1", new Vector2(100, 200), new Vector2(300, 600))] },
                new GamePanel("PCBag_C", PanelKind.Inventory, Vector2.Zero, new Vector2(10, 10), false),
            ]),
        });
        var panels = Wire.Run(Tools, "get_state")["panels"]!.AsArray();
        var parts = panels[0]!["parts"]!.AsArray();
        Check.Equal(1, parts.Count);
        var part = parts[0]!;
        Check.Equal("S16BoardRelicOptCell_1", (string?)part["name"]);
        Check.Equal("100,200", string.Join(',', part["min"]!.AsArray().Select(v => (double)v!)));
        Check.Equal("300,600", string.Join(',', part["max"]!.AsArray().Select(v => (double)v!)));
        Check.That(panels[1]!["parts"] is null, "no parts key without parts");
    }

    [Test]
    public void StateListsTheOpenGamePanels()
    {
        TestGame.Set(GameState.Empty with
        {
            Ui = new GameUi([
                new GamePanel("PCBag_C", PanelKind.Inventory, new Vector2(2400, 200), new Vector2(3300, 1200), false) { Address = 0x1F2E },
                new GamePanel("WorldMapTitle_C", PanelKind.WorldMap, Vector2.Zero, new Vector2(3440, 1440), true),
            ]),
        });
        var panels = Wire.Run(Tools, "get_state")["panels"]!.AsArray();
        Check.Equal(2, panels.Count);
        Check.Equal("PCBag_C", (string?)panels[0]!["name"]);
        Check.Equal("Inventory", (string?)panels[0]!["kind"]);
        Check.Equal("2400,200", string.Join(',', panels[0]!["min"]!.AsArray().Select(v => (double)v!)));
        Check.Equal("3300,1200", string.Join(',', panels[0]!["max"]!.AsArray().Select(v => (double)v!)));
        Check.Equal(false, (bool)panels[0]!["fullScreen"]!);
        Check.Equal("0x1F2E", (string?)panels[0]!["address"]);
        Check.Equal(true, (bool)panels[1]!["fullScreen"]!);
        Check.Equal("WorldMap", (string?)panels[1]!["kind"]);
    }

    [Test]
    public void GetStateShowsTheMap()
    {
        TestGame.Set(GameState.Empty with { Area = new EmberHUD.MapInfo("Rainforest", 78, 272, null, false, [new MapContent("Vigil", "svg:quest", ContentState.Done), new MapContent("The Devil", "game:x", ContentState.Active, 1, 2)], ["+15% Drop Quantity"]) });
        var map = Wire.Run(Tools, "get_state")["map"]!;
        Check.Equal("Rainforest", (string?)map["name"]);
        Check.Equal(78, (int)map["monsterLevel"]!);
        Check.Equal(272, (int)map["remaining"]!);
        Check.That(map["xpGain"] is null, "unknown XP is null");
        Check.Equal("Done", (string?)map["content"]![0]!["state"]);
        Check.Equal(1, (int)map["content"]![0]!["total"]!);
        Check.Equal(1, (int)map["content"]![1]!["done"]!);
        Check.Equal(2, (int)map["content"]![1]!["total"]!);
        Check.Equal("+15% Drop Quantity", (string?)map["modifiers"]![0]);
        TestGame.Set(GameState.Empty);
        Check.That(Wire.Run(Tools, "get_state")["map"] is null, "no map, no key");
    }

    [Test]
    public void AnEmptyGameStillAnswers()
    {
        TestGame.Set(GameState.Empty);
        var result = Wire.Run(Tools, "get_state");
        Check.Equal("starting", (string?)result["status"]);
        Check.That(result["player"] is null && result["camera"] is null, "no player or camera");
        Check.Equal(0, result["panels"]!.AsArray().Count);
        Check.Equal(0, Entities(new JsonObject()).Count);
    }

    [Test]
    public void EntitiesAreNearestFirstAndLimited()
    {
        Use(Mob(1, 5, 30), Mob(2, 5, 10), Mob(3, 5, 20));
        var result = Wire.Run(Tools, "get_entities", new JsonObject { ["limit"] = 2 });
        Check.Equal(3, (int)result["total"]!);
        Check.Equal(2, (int)result["returned"]!);
        Check.Equal("2,3", string.Join(',', result["entities"]!.AsArray().Select(row => (int)row!["id"]!)));
        Check.Equal(10d, (double)result["entities"]![0]!["distance"]!, "distance");
    }

    [Test]
    public void EntityRowsCarryLife()
    {
        Use(Mob(1, 5, 10) with { Life = 137.996, MaxLife = 165 }, Mob(2, 5, 20));
        var rows = Entities(new JsonObject());
        Check.Equal(138d, (double)rows[0]!["life"]!, "life");
        Check.Equal(165d, (double)rows[0]!["maxLife"]!, "maxLife");
        Check.Equal(0d, (double)rows[1]!["maxLife"]!, "unknown");
    }

    [Test]
    public void EntityRowsCarryTheGameBarOnlyWhenSet()
    {
        Use(Mob(1, 5, 10) with { HasGameBar = true, GameBar = new ScreenRect(new Vector2(10, 20), new Vector2(110.5f, 30)) }, Mob(2, 5, 20));
        var rows = Entities(new JsonObject());
        Check.Equal("10,20,100.5,10", string.Join(',', rows[0]!["bar"]!.AsArray().Select(n => (double)n!)), "bar");
        Check.That(rows[1]!["bar"] is null, "no bar");
    }

    [Test]
    public void EntityRowsCarryHasBarOnlyForMonstersWithAGameBar()
    {
        Use(Mob(1, 5, 10) with { HasGameBar = true }, Mob(2, 5, 20));
        var rows = Entities(new JsonObject());
        Check.That((bool?)rows[0]!["hasBar"] == true, "hasBar");
        Check.That(rows[0]!["bar"] is null, "no rect");
        Check.That(rows[1]!["hasBar"] is null, "omitted");
    }

    [Test]
    public void EntityRowsCarryTheNameWhenThereIsOne()
    {
        Use(Mob(1, 5, 10) with { Name = "Crimson Boar" }, Mob(2, 5, 20));
        var rows = Entities(new JsonObject());
        Check.Equal("Crimson Boar", (string?)rows[0]!["name"]);
        Check.That(rows[1]!["name"] is null, "no name, no key");
    }

    [Test]
    public void FiltersCombineAndAddressesAreHex()
    {
        Use(Mob(1, 5, 10, rarity: Rarity.Rare, address: 0x1F2E), Mob(2, 5, 90, rarity: Rarity.Rare), Mob(3, 6, 10, rarity: Rarity.Rare), Mob(4, 5, 10), Mob(5, 5, 10, EntityKind.Npc, Rarity.Rare));
        var rows = Entities(new JsonObject { ["kind"] = "monster", ["rarity"] = "Rare", ["cfgId"] = 5, ["radius"] = 50 });
        Check.Equal(1, rows.Count);
        Check.Equal(1, (int)rows[0]!["id"]!);
        Check.Equal("0x1F2E", (string?)rows[0]!["address"]);
        Check.Equal("Monster", (string?)rows[0]!["kind"]);
        Check.That(Entities(new JsonObject { ["address"] = "0x1f2e" }).Count == 1, "by address");
        Check.That(Entities(new JsonObject { ["id"] = 3 }).Count == 1, "by id");
        Check.That(Entities(new JsonObject()).Single(row => (int)row!["id"]! == 2)!["address"] is null, "no address when the reader has none");
    }

    [Test]
    public void BadFilterValuesNameTheValidOnes()
    {
        Use(Mob(1, 5, 10));
        Check.That(Wire.Fails(Tools, "get_entities", new JsonObject { ["kind"] = "Dragon" }).Contains("Monster"), "kinds listed");
        Check.That(Wire.Fails(Tools, "get_entities", new JsonObject { ["rarity"] = "Epic" }).Contains("Rare"), "rarities listed");
        Check.That(Wire.Fails(Tools, "get_entities", new JsonObject { ["limit"] = 0 }).Contains("between"), "limit range");
        Check.That(Wire.Fails(Tools, "get_entities", new JsonObject { ["address"] = "banana" }).Contains("hex"), "address");
        TestGame.Set(new GameState { Status = "playing", AreaId = 1, Entities = [Mob(1, 5, 10)] });
        Check.That(Wire.Fails(Tools, "get_entities", new JsonObject { ["radius"] = 5 }).Contains("player"), "radius needs a player");
    }

    [Test]
    public void NonFiniteFloatsBecomeNullAndStillSerialise()
    {
        TestGame.Set(new GameState { Status = "playing", AreaId = 1, Player = new Player(Vector3.Zero, Vector2.UnitX), Entities = [new Entity(1, 5, new Vector3(float.NaN, float.PositiveInfinity, 1), EntityKind.Monster, Rarity.Normal)] });
        var row = Entities(new JsonObject())[0]!;
        Check.That(row["x"] is null && row["y"] is null, "null, not NaN");
        Check.That(row.ToJsonString().Contains("\"z\":1"), "finite kept");
    }

    static readonly SceneEntity[] Listed =
    [
        new(1, 48, new Vector3(30, 0, 0), true, EntityKind.Monster, "") { Address = 0x1000 },
        new(2, 77, new Vector3(10, 0, 5), false, null, "EMapGodOfWar") { Address = 0x2000 },
        new(3, 66, null, true, null, ""),
        new(4, 48, new Vector3(20, 0, 0), true, EntityKind.Monster, ""),
    ];

    static JsonObject ListScene(JsonObject? args = null, Player? player = null)
    {
        TestGame.Set(new GameState { Status = "playing", AreaId = 12, Player = player });
        TestGame.UseScene(() => Listed);
        try
        {
            return Wire.Run(Tools, "scene_entities", args);
        }
        finally
        {
            TestGame.UseScene(null);
        }
    }

    static string Ids(JsonObject result) => string.Join(',', result["entities"]!.AsArray().Select(row => (int)row!["id"]!));

    [Test]
    public void SceneEntitiesListsNearestFirstWithEveryField()
    {
        var result = ListScene(player: new Player(Vector3.Zero, Vector2.UnitX));
        Check.Equal(4, (int)result["total"]!);
        Check.Equal(4, (int)result["returned"]!);
        Check.Equal("2,4,1,3", Ids(result));
        var statue = result["entities"]![0]!;
        Check.Equal(77, (int)statue["cfgId"]!);
        Check.Equal("EMapGodOfWar", (string?)statue["class"]);
        Check.That(statue["kind"] is null, "unkinded is null");
        Check.Equal(false, (bool)statue["alive"]!);
        Check.Equal(5d, (double)statue["z"]!);
        Check.Equal(10d, (double)statue["distance"]!);
        Check.Equal("0x2000", (string?)statue["address"]);
        Check.Equal("Monster", (string?)result["entities"]![1]!["kind"]);
        var loose = result["entities"]![3]!;
        Check.That(loose["x"] is null && loose["distance"] is null, "no position");
    }

    [Test]
    public void SceneEntitiesFiltersAndLimits()
    {
        var player = new Player(Vector3.Zero, Vector2.UnitX);
        Check.Equal("4,1", Ids(ListScene(new JsonObject { ["cfgId"] = 48 }, player)));
        Check.Equal("2", Ids(ListScene(new JsonObject { ["class"] = "godof" }, player)));
        Check.Equal("2,4", Ids(ListScene(new JsonObject { ["radius"] = 25 }, player)));
        var limited = ListScene(new JsonObject { ["limit"] = 1 }, player);
        Check.Equal(4, (int)limited["total"]!);
        Check.Equal(1, (int)limited["returned"]!);
        Check.Equal("1,2,3,4", Ids(ListScene()));
        Check.That(Wire.Fails(Tools, "scene_entities", new JsonObject { ["limit"] = 0 }).Contains("between"), "limit range");
    }

    [Test]
    public void SceneEntitiesWithoutAGameIsEmpty()
    {
        TestGame.UseScene(null);
        TestGame.Set(GameState.Empty);
        var result = Wire.Run(Tools, "scene_entities", new JsonObject { ["radius"] = 10 });
        Check.Equal(0, (int)result["total"]!);
        Check.Equal(0, result["entities"]!.AsArray().Count);
    }

    static readonly InventoryItem[] Bags =
    [
        new(InventoryBag.Equipment, 0, "406_a", 406, 31, 1, 2, 1, 1, 0),
        new(InventoryBag.Commodities, 0, "5080_b", 5080, 1, 0, 2, 177, 9999, 7),
        new(InventoryBag.Commodities, 1, "382001_c", 382001, 1, 0, 100, 6, 999, 42),
    ];

    [Test]
    public void GetInventoryListsItemsAndFiltersByBag()
    {
        TestGame.Set(GameState.Empty with { Inventory = Bags });
        var tool = Tools.Single(t => t.Name == "get_inventory");
        Check.That(tool.Description.Length > 0, "described");
        var all = Wire.Run(Tools, "get_inventory");
        Check.Equal(true, (bool)all["ready"]!);
        Check.Equal(3, all["items"]!.AsArray().Count);
        var stack = all["items"]![1]!;
        Check.Equal("Commodities", (string?)stack["bag"]);
        Check.Equal(0, (int)stack["slot"]!);
        Check.Equal("5080_b", (string?)stack["id"]);
        Check.Equal(5080, (int)stack["baseId"]!);
        Check.Equal(177, (int)stack["count"]!);
        Check.Equal(9999, (int)stack["stackMax"]!);
        Check.Equal(7, (int)stack["type"]!);
        Check.Equal(100, (int)all["items"]![2]!["rareValue"]!);
        Check.Equal(31, (int)all["items"]![0]!["level"]!);
        Check.Equal(1, (int)all["items"]![0]!["quality"]!);
        Check.Equal(2, Wire.Run(Tools, "get_inventory", new JsonObject { ["bag"] = "commodities" })["items"]!.AsArray().Count);
        Check.Equal(0, Wire.Run(Tools, "get_inventory", new JsonObject { ["bag"] = "Skills" })["items"]!.AsArray().Count);
    }

    [Test]
    public void GetInventoryBeforeTheReaderHasRunIsNotReady()
    {
        TestGame.Set(GameState.Empty);
        var result = Wire.Run(Tools, "get_inventory");
        Check.Equal(false, (bool)result["ready"]!);
        Check.Equal(0, result["items"]!.AsArray().Count);
    }

    [Test]
    public void GetInventoryWaitsForTheReader()
    {
        try
        {
            TestGame.Set(GameState.Empty);
            var late = Task.Run(() => { Thread.Sleep(200); TestGame.Set(GameState.Empty with { Inventory = Bags }); });
            var result = Wire.Run(Tools, "get_inventory");
            late.Wait();
            Check.Equal(true, (bool)result["ready"]!);
            Check.Equal(3, result["items"]!.AsArray().Count);
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void GetInventoryGivesUpWhenTheReaderNeverAnswers()
    {
        try
        {
            TestGame.Set(GameState.Empty);
            var clock = Stopwatch.StartNew();
            var result = Wire.Run(Tools, "get_inventory");
            Check.That(clock.ElapsedMilliseconds < 1500, "returned");
            Check.Equal(false, (bool)result["ready"]!);
            Check.Equal(0, result["items"]!.AsArray().Count);
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void GetInventoryReturnsAtOnceWhenAlreadyRead()
    {
        try
        {
            TestGame.Set(GameState.Empty with { Inventory = Bags });
            var clock = Stopwatch.StartNew();
            Wire.Run(Tools, "get_inventory");
            Check.That(clock.ElapsedMilliseconds < 200, "immediate");
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void GetInventoryRejectsAnUnknownBag()
    {
        TestGame.Set(GameState.Empty with { Inventory = Bags });
        Check.That(Wire.Fails(Tools, "get_inventory", new JsonObject { ["bag"] = "Stash" }).Contains("Equipment"), "bags listed");
    }
}
