using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

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

    [Test]
    public void GroundTierNumbersAreOptIn()
    {
        var plugin = Plugin();
        TestGame.Set(WithItem());
        var plain = TestFrame.Vertices(plugin.Render);
        Check.Equal(false, plugin.Settings.GroundTierNumbers);
        plugin.Settings.GroundTierNumbers = true;
        Check.That(TestFrame.Vertices(plugin.Render) > plain, "ground on");
        plugin.Settings.GroundTierNumbers = false;
        plugin.Settings.InventoryTierNumbers = true;
        Check.Equal(plain, TestFrame.Vertices(plugin.Render));
    }

    [Test]
    public void InventoryTierNumbersAreOptInAndIndependent()
    {
        var plugin = Plugin();
        var bag = BagState(Owned(0, [1], []));
        var stash = StashState(Owned(0, [1], []));
        var plainBag = Vertices(plugin, bag);
        var plainStash = Vertices(plugin, stash);
        plugin.Settings.GroundTierNumbers = true;
        Check.Equal(plainBag, Vertices(plugin, bag));
        Check.Equal(plainStash, Vertices(plugin, stash));
        plugin.Settings.GroundTierNumbers = false;
        plugin.Settings.InventoryTierNumbers = true;
        Check.That(Vertices(plugin, bag) > plainBag, "bag on");
        Check.That(Vertices(plugin, stash) > plainStash, "stash on");
    }

    [Test]
    public void LegendaryGroundItemDrawsNothingNeighboursStill()
    {
        var plugin = Plugin();
        var normal = new GroundItem(1, new Vector2(100, 200), new Vector2(300, 230), Vector3.Zero, true, [1], [2]);
        var legendary = normal with { DropId = 2, LabelMin = new Vector2(100, 300), LabelMax = new Vector2(300, 330) } with { Legendary = true };
        TestGame.Set(GameState.Empty with { GroundItems = [legendary] });
        Check.Equal(0, TestFrame.Vertices(plugin.Render));
        TestGame.Set(GameState.Empty with { GroundItems = [normal] });
        var one = TestFrame.Vertices(plugin.Render);
        Check.That(one > 0, "normal draws");
        TestGame.Set(GameState.Empty with { GroundItems = [legendary, normal] });
        Check.Equal(one, TestFrame.Vertices(plugin.Render));
    }

    [Test]
    public void LegendaryBagAndStashItemsDrawNothingNeighboursStill()
    {
        var plugin = Plugin();
        var legendary = Owned(0, [1], []) with { Legendary = true };
        Check.Equal(0, Vertices(plugin, BagState(legendary)));
        Check.Equal(0, Vertices(plugin, StashState(legendary)));
        var one = Vertices(plugin, BagState(Owned(1, [1], [])));
        Check.That(one > 0, "normal draws");
        Check.Equal(one, Vertices(plugin, BagState(legendary, Owned(1, [1], []))));
        Check.Equal(one, Vertices(plugin, StashState(legendary, Owned(1, [1], []))));
    }

    static uint Abgr(Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);

    static List<(Vector2 Pos, uint Col)> Drawn(Main plugin, GameState state)
    {
        TestGame.Set(state);
        var drawn = new List<(Vector2, uint)>();
        TestFrame.Run(() =>
        {
            var list = EmberHUD.Draw.Background;
            var before = list.VtxBuffer.Size;
            plugin.Render();
            for (var i = before; i < list.VtxBuffer.Size; i++) drawn.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col));
        });
        return drawn;
    }

    static readonly Color Lime = Color.FromArgb(255, 0, 255, 0);

    static float Height(List<(Vector2 Pos, uint Col)> drawn, Color colour)
    {
        var ys = drawn.Where(v => v.Col == Abgr(colour)).Select(v => v.Pos.Y).ToArray();
        return ys.Length == 0 ? 0 : ys.Max() - ys.Min();
    }

    [Test]
    public void GroundNumberColourAndOpacityReachTheText()
    {
        var plugin = Plugin();
        plugin.Settings.GroundTierNumbers = true;
        Check.Equal(0, Drawn(plugin, WithItem()).Count(v => v.Col == Abgr(Lime)));
        plugin.Settings.GroundNumbers.Color = Lime;
        Check.That(Drawn(plugin, WithItem()).Any(v => v.Col == Abgr(Lime)), "colour");
        var faded = Color.FromArgb(128, 0, 255, 0);
        plugin.Settings.GroundNumbers.Color = faded;
        var drawn = Drawn(plugin, WithItem());
        Check.That(drawn.Any(v => v.Col == Abgr(faded)), "opacity");
        Check.That(!drawn.Any(v => v.Col == Abgr(Lime)), "no opaque");
    }

    [Test]
    public void NumberScaleChangesTheTextSize()
    {
        var plugin = Plugin();
        plugin.Settings.GroundTierNumbers = true;
        plugin.Settings.GroundNumbers.Color = Lime;
        var normal = Height(Drawn(plugin, WithItem()), Lime);
        plugin.Settings.GroundNumbers.Scale = 3;
        Check.That(Height(Drawn(plugin, WithItem()), Lime) > normal * 2, "scaled");
    }

    [Test]
    public void OutlineOffDrawsFewerVertices()
    {
        var plugin = Plugin();
        plugin.Settings.GroundTierNumbers = true;
        var outlined = Drawn(plugin, WithItem()).Count;
        plugin.Settings.GroundNumbers.Outline = false;
        var plain = Drawn(plugin, WithItem()).Count;
        Check.That(plain < outlined, "ground");
        plugin.Settings.InventoryTierNumbers = true;
        var bag = BagState(Owned(0, [1], []));
        var bagOutlined = Drawn(plugin, bag).Count;
        plugin.Settings.InventoryNumbers.Outline = false;
        Check.That(Drawn(plugin, bag).Count < bagOutlined, "inventory");
    }

    [Test]
    public void GroundAndInventoryNumberStylesAreIndependent()
    {
        var plugin = Plugin();
        plugin.Settings.GroundTierNumbers = true;
        plugin.Settings.InventoryTierNumbers = true;
        plugin.Settings.GroundNumbers.Color = Lime;
        Check.That(Drawn(plugin, WithItem()).Any(v => v.Col == Abgr(Lime)), "ground styled");
        Check.That(!Drawn(plugin, BagState(Owned(0, [1], []))).Any(v => v.Col == Abgr(Lime)), "bag unaffected");
        plugin.Settings.GroundNumbers.Color = Color.White;
        plugin.Settings.InventoryNumbers.Color = Lime;
        Check.That(!Drawn(plugin, WithItem()).Any(v => v.Col == Abgr(Lime)), "ground unaffected");
        Check.That(Drawn(plugin, BagState(Owned(0, [1], []))).Any(v => v.Col == Abgr(Lime)), "bag styled");
        Check.That(Drawn(plugin, StashState(Owned(0, [1], []))).Any(v => v.Col == Abgr(Lime)), "stash styled");
    }
}
