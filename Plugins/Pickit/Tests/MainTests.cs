using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class MainTests
{
    [Test]
    public void AsksForGroundItemsOnlyWhenNeeded()
    {
        TestSend.Record();
        TestGame.Set(new GameState());
        var plugin = new Main();
        plugin.Initialize();
        Check.That(TestGame.GroundItemsAskedDuring(plugin.Tick), "near on");
        plugin.Settings.NearEnabled = false;
        plugin.Settings.GemsEnabled = false;
        Check.That(!TestGame.GroundItemsAskedDuring(plugin.Tick), "both off");
        plugin.Settings.GemsEnabled = true;
        Check.That(TestGame.GroundItemsAskedDuring(plugin.Tick), "gems on");
    }

    [Test]
    public void DisableReleasesKeys()
    {
        var sent = TestSend.Record();
        TestGame.Set(new GameState { Status = "live", ChatOpen = false });
        TestInput.Hold(Key.Space);
        var plugin = new Main();
        plugin.Initialize();
        plugin.Tick();
        Check.That(sent.Count > 0 && sent[0].Down && sent[0].Key == Key.A, "pressed");
        plugin.OnDisable();
        Check.That(!sent[^1].Down && sent[^1].Key == Key.A, "released");
        TestInput.Hold();
    }

    static GameState WithItem(GameUi? ui = null) => new()
    {
        Ui = ui ?? GameUi.Empty,
        GroundItems = [new GroundItem(1, new Vector2(100, 200), new Vector2(300, 230), Vector3.Zero, true, [1], [2])]
    };

    static Main Plugin()
    {
        var plugin = new Main();
        plugin.Initialize();
        return plugin;
    }

    [Test]
    public void FullScreenPanelDrawsNothing()
    {
        TestGame.Set(WithItem(new GameUi([new GamePanel("inv", PanelKind.Inventory, Vector2.Zero, Vector2.One, true)])));
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
    }

    [Test]
    public void NoGroundItemsDrawsNothing()
    {
        TestGame.Set(new GameState());
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
    }

    [Test]
    public void DrawsGemsForAnItem()
    {
        TestGame.Set(WithItem());
        var plugin = Plugin();
        Check.That(TestFrame.Vertices(plugin.Render) > 0);
        plugin.Settings.GemsEnabled = false;
        Check.Equal(0, TestFrame.Vertices(plugin.Render));
    }

    static readonly GameUi BagOpen = new([new GamePanel("Bag", PanelKind.Inventory, default, new Vector2(900, 600), false)]);
    static readonly GameUi StashOpen = new([new GamePanel("Stash", PanelKind.Stash, default, new Vector2(900, 600), false)]);

    static BagCell[] Cells(int count) => [.. System.Linq.Enumerable.Range(0, count).Select(i => new BagCell(i, new Vector2(10 + i * 100, 10), new Vector2(104 + i * 100, 145)))];

    static InventoryItem Owned(int slot, int[] prefixes, int[] suffixes) => new(InventoryBag.Commodities, slot, "id", 1, 0, 0, 0, 5, 100, 0) { PrefixIds = prefixes, SuffixIds = suffixes };

    static GameState BagState(params InventoryItem[] items) => GameState.Empty with { Ui = BagOpen, Bag = new BagView(InventoryBag.Commodities, Vector2.Zero, new Vector2(900, 600), Cells(3)), Inventory = items };

    static GameState StashState(params InventoryItem[] items) => GameState.Empty with { Ui = StashOpen, Stash = new StashView(0, Vector2.Zero, new Vector2(900, 600), Cells(3)), StashItems = items };

    static int Vertices(Main plugin, GameState state)
    {
        TestGame.Set(state);
        return TestFrame.Vertices(plugin.Render);
    }

    [Test]
    public void BagItemDrawsIcons()
    {
        var plugin = Plugin();
        var one = Vertices(plugin, BagState(Owned(0, [1], [])));
        Check.That(one > 0, "icon");
        Check.Equal(one * 2, Vertices(plugin, BagState(Owned(0, [1], []), Owned(1, [], [1]))));
        Check.Equal(0, Vertices(plugin, BagState(Owned(0, [], []))));
    }

    [Test]
    public void StashItemDrawsIcons()
    {
        var plugin = Plugin();
        var one = Vertices(plugin, StashState(Owned(0, [1], [])));
        Check.That(one > 0, "icon");
        Check.Equal(one * 2, Vertices(plugin, StashState(Owned(0, [1], []), Owned(2, [], [1]))));
    }

    [Test]
    public void ClosedInventoryDrawsNothing()
    {
        var plugin = Plugin();
        Check.Equal(0, Vertices(plugin, BagState(Owned(0, [1], [])) with { Ui = GameUi.Empty }));
        Check.Equal(0, Vertices(plugin, BagState(Owned(0, [1], [])) with { Inventory = null }));
        Check.Equal(0, Vertices(plugin, StashState(Owned(0, [1], [])) with { StashItems = null }));
        plugin.Settings.InventoryEnabled = false;
        Check.Equal(0, Vertices(plugin, BagState(Owned(0, [1], []))));
    }

    [Test]
    public void FullScreenInventoryStillDrawsIcons()
    {
        var full = new GameUi([new GamePanel("Bag", PanelKind.Inventory, Vector2.Zero, Vector2.One, true)]);
        Check.That(Vertices(Plugin(), BagState(Owned(0, [1], [])) with { Ui = full }) > 0);
    }

    [Test]
    public void NoInventoryAskWhenToggleOff()
    {
        var plugin = Plugin();
        TestGame.Set(BagState(Owned(0, [1], [])));
        Check.That(TestGame.InventoryAskedDuring(() => TestFrame.Vertices(plugin.Render)), "asked while on");
        plugin.Settings.InventoryEnabled = false;
        Check.That(!TestGame.InventoryAskedDuring(() => TestFrame.Vertices(plugin.Render)), "not asked while off");
    }

    [Test]
    public void CellsOutsideNothingDrawnWithoutMatchingItem()
    {
        var plugin = Plugin();
        Check.Equal(0, Vertices(plugin, BagState(Owned(9, [1], []))));
        Check.Equal(0, Vertices(plugin, BagState(Owned(0, [1], []) with { Bag = InventoryBag.Equipment })));
        Check.Equal(0, Vertices(plugin, BagState() with { Bag = null }));
    }
}
