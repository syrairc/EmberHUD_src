using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderCompareTests
{
    sealed class Scene
    {
        public required FakeWorld World;
        public required FakeUi Ui;
        public long Now;
        public int Next = 10;
        public nint Page;
        public nint TipPanel;
    }

    sealed record Tip(nint Root, nint[] Rows, nint[] Damage, nint[] Survival);

    static Scene Open(bool tradePage = true, bool tipPanel = true)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        nint pageWidget = 0;
        nint panelWidget = 0;
        if (tradePage)
        {
            var page = ui.Widget(ui.UserClass(Offsets.TradePageClassName), Offsets.TradePageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
            ui.SetTree(page, ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, new Vector2(3440, 1440)));
            ui.AddRoot(ui.Host(page));
            pageWidget = page;
        }
        if (tipPanel)
        {
            var panel = ui.Widget(ui.UserClass(Offsets.CompareTipPanelClassName), Offsets.CompareTipPanelClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
            ui.SetTree(panel, ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, new Vector2(3440, 1440)));
            ui.AddRoot(ui.Host(panel));
            panelWidget = panel;
        }
        return new Scene { World = world, Ui = ui, Page = pageWidget, TipPanel = panelWidget };
    }

    static Tip AddTip(Scene scene, (string Damage, string Survival, byte Visibility)[] rows, Vector2? at = null)
    {
        var ui = scene.Ui;
        var rootPosition = at ?? new Vector2(1000, 400);
        var position = new Vector2(1000, 400);
        var size = new Vector2(300, 400);
        var userClass = ui.UserClass("AttrbuteCompareItem_C");
        var itemClass = ui.UserClass("AttrbuteItem_C");
        var textClass = ui.UserClass("UITextBlock");
        var canvas = ui.PanelClass("CanvasPanel");
        var root = ui.Widget(ui.UserClass(Offsets.CompareTipClassName), "TipWeaponItem_C_" + scene.Next, rootPosition, size);
        var rootPanel = ui.Widget(canvas, "CanvasPanel_0", position, size);
        var items = ui.Widget(ui.UserClass("AttrbuteCompareItems_C"), Offsets.CompareItems, position, size);
        var itemsPanel = ui.Widget(canvas, "CanvasPanel_0", position, size);
        ui.SetTree(root, rootPanel);
        ui.SetChildren(rootPanel, items);
        ui.SetTree(items, itemsPanel);

        string[] rowNames = [Offsets.CompareRow1, Offsets.CompareRow2];
        var rowWidgets = new nint[2];
        var damage = new nint[2];
        var survival = new nint[2];
        for (var i = 0; i < 2; i++)
        {
            var row = ui.Widget(userClass, rowNames[i], position, size, visibility: i < rows.Length ? rows[i].Visibility : (byte)1);
            var rowPanel = ui.Widget(canvas, "CanvasPanel_0", position, size);
            var dps = ui.Widget(itemClass, Offsets.CompareDamage, position, size);
            var def = ui.Widget(itemClass, Offsets.CompareSurvival, position, size);
            damage[i] = ui.Widget(textClass, Offsets.CompareValue, position, size);
            survival[i] = ui.Widget(textClass, Offsets.CompareValue, position, size);
            ui.SetTree(row, rowPanel);
            ui.SetChildren(rowPanel, dps, def);
            ui.SetTree(dps, ui.Widget(canvas, "CanvasPanel_0", position, size));
            ui.SetTree(def, ui.Widget(canvas, "CanvasPanel_0", position, size));
            ui.SetChildren(WidgetTreeRoot(scene, dps), damage[i]);
            ui.SetChildren(WidgetTreeRoot(scene, def), survival[i]);
            ui.Text(damage[i], Offsets.TextBlockText, i < rows.Length ? rows[i].Damage : "0%");
            ui.Text(survival[i], Offsets.TextBlockText, i < rows.Length ? rows[i].Survival : "0%");
            rowWidgets[i] = row;
        }
        ui.SetChildren(itemsPanel, rowWidgets);

        scene.Next++;
        return new Tip(root, rowWidgets, damage, survival);
    }

    static nint WidgetTreeRoot(Scene scene, nint userWidget)
    {
        scene.World.Memory.TryReadPtr(userWidget + Offsets.UserWidgetTree, out var tree);
        scene.World.Memory.TryReadPtr(tree + Offsets.WidgetTreeRoot, out var root);
        return root;
    }

    static (string, string, byte)[] Two => [("+12.3%", "-4.4%", 0), ("+3.0%", "+9.5%", 4)];

    static GameUiReader Reader(Scene scene) => scene.World.UiReader(() => scene.Now);

    static EquipCompare? Tick(GameUiReader reader, Scene scene)
    {
        reader.Read();
        var compare = reader.ReadCompare();
        scene.Now += Offsets.CompareRefreshMilliseconds;
        return compare;
    }

    static CompareRow[] Rows(EquipCompare? compare) => compare!.Rows;

    [Fact]
    public void ReadsBothVisibleRowsInOrder()
    {
        var scene = Open();
        AddTip(scene, Two);
        Assert.Equal([new CompareRow(12.3f, -4.4f), new CompareRow(3f, 9.5f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void ACollapsedRowIsDropped()
    {
        var scene = Open();
        AddTip(scene, [("+12.3%", "-4.4%", 0), ("+3.0%", "+9.5%", 1)]);
        Assert.Equal([new CompareRow(12.3f, -4.4f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void AHiddenRowIsDropped()
    {
        var scene = Open();
        AddTip(scene, [("+12.3%", "-4.4%", 2), ("+3.0%", "+9.5%", 3)]);
        Assert.Equal([new CompareRow(3f, 9.5f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void NoTooltipGivesNull()
    {
        var scene = Open();
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void NoTradePageGivesNullEvenWithATooltip()
    {
        var scene = Open(tradePage: false);
        AddTip(scene, Two);
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void AnUnparseableTextIsNullAndABlankRowIsDropped()
    {
        var scene = Open();
        AddTip(scene, [("Sold", "-4.4%", 0), ("x", "", 4)]);
        Assert.Equal([new CompareRow(null, -4.4f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void NoParseableRowsGivesNull()
    {
        var scene = Open();
        AddTip(scene, [("a", "b", 0), ("c", "d", 4)]);
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void GarbageWidgetsGiveNullWithoutThrowing()
    {
        var scene = Open();
        var tip = AddTip(scene, Two);
        scene.World.Memory.Write(tip.Root + Offsets.UserWidgetTree, scene.World.Alloc(0x40));
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void ASingleTooltipWithAnOffScreenRootGivesNull()
    {
        var scene = Open();
        AddTip(scene, Two, at: new Vector2(-9000, -9000));
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void ASingleTooltipWithAnEmptyRootGivesNull()
    {
        var scene = Open();
        var tip = AddTip(scene, Two);
        scene.Ui.WeakPtr(tip.Root + Offsets.UWidgetSlate, scene.Ui.Slate(new Vector2(1000, 400), Vector2.Zero));
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void ASecondInstanceAppearingLaterNeverLeavesTheFirstsNumbers()
    {
        var scene = Open();
        var reader = Reader(scene);
        AddTip(scene, [("+1.0%", "+2.0%", 0)]);
        Assert.Equal([new CompareRow(1f, 2f)], Rows(Tick(reader, scene)));
        AddTip(scene, [("+5.0%", "+6.0%", 0)], at: new Vector2(1500, 400));
        scene.Now += Offsets.CompareScanMilliseconds;
        Assert.Null(Tick(reader, scene));
    }

    [Fact]
    public void AZeroedTextPointerAfterAGoodReadGivesNull()
    {
        var scene = Open();
        var reader = Reader(scene);
        var tip = AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        foreach (var text in tip.Damage.Concat(tip.Survival)) scene.World.Memory.Write(text + Offsets.TextBlockText, 0L);
        Assert.Null(Tick(reader, scene));
    }

    [Fact]
    public void AZeroedRowNameAfterAGoodReadGivesNull()
    {
        var scene = Open();
        var reader = Reader(scene);
        var tip = AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(tip.Rows[0] + Offsets.UObjectName, 0xFFFFFFu);
        Assert.Null(Tick(reader, scene));
    }

    [Fact]
    public void ClosingTheTradePageClearsTheCache()
    {
        var scene = Open();
        var reader = Reader(scene);
        var first = AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(scene.Page + Offsets.UWidgetVisibility, (byte)1);
        reader.Read();
        reader.Read();
        Assert.Null(reader.ReadCompare());
        scene.Now += Offsets.CompareRefreshMilliseconds;
        scene.World.Memory.Write(scene.Page + Offsets.UWidgetVisibility, (byte)4);
        scene.World.Memory.Write(first.Root + Offsets.UObjectClass, 0L);
        AddTip(scene, [("+7.0%", "+8.0%", 0)]);
        reader.Read();
        Assert.Equal([new CompareRow(7f, 8f)], Rows(Tick(reader, scene)));
    }

    [Fact]
    public void TwoTooltipsGeometryPicksTheOneOnScreen()
    {
        var scene = Open();
        AddTip(scene, [("+1.0%", "+2.0%", 0)], at: new Vector2(-9000, -9000));
        AddTip(scene, [("+5.0%", "+6.0%", 0)]);
        Assert.Equal([new CompareRow(5f, 6f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void TheTooltipRectIsTheRootRect()
    {
        var scene = Open();
        AddTip(scene, Two);
        var compare = Tick(Reader(scene), scene)!;
        Assert.Equal(new Vector2(1000, 400), compare.Min);
        Assert.Equal(new Vector2(1300, 800), compare.Max);
    }

    [Fact]
    public void WithTwoTooltipsTheRectIsTheOnScreenOnes()
    {
        var scene = Open();
        AddTip(scene, [("+1.0%", "+2.0%", 0)], at: new Vector2(-9000, -9000));
        AddTip(scene, [("+5.0%", "+6.0%", 0)], at: new Vector2(1200, 300));
        var compare = Tick(Reader(scene), scene)!;
        Assert.Equal(new Vector2(1200, 300), compare.Min);
        Assert.Equal(new Vector2(1500, 700), compare.Max);
    }

    [Fact]
    public void TwoTooltipsThatGeometryCannotSplitGiveNull()
    {
        var scene = Open();
        AddTip(scene, [("+1.0%", "+2.0%", 0)]);
        AddTip(scene, [("+5.0%", "+6.0%", 0)], at: new Vector2(1500, 400));
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void ATooltipWithoutRowsDoesNotBlockTheLiveOne()
    {
        var scene = Open();
        AddTip(scene, [("a", "b", 0)]);
        AddTip(scene, [("+5.0%", "+6.0%", 0)]);
        Assert.Equal([new CompareRow(5f, 6f)], Rows(Tick(Reader(scene), scene)));
    }

    [Fact]
    public void ARecycledTooltipIsRescanned()
    {
        var scene = Open();
        var reader = Reader(scene);
        var old = AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(old.Root + Offsets.UObjectClass, 0L);
        AddTip(scene, [("+7.0%", "+8.0%", 0)]);
        scene.Now += Offsets.CompareScanMilliseconds;
        Assert.Equal([new CompareRow(7f, 8f)], Rows(Tick(reader, scene)));
    }

    [Fact]
    public void ANewTooltipIsFoundAfterTheScanInterval()
    {
        var scene = Open();
        var reader = Reader(scene);
        Assert.Null(Tick(reader, scene));
        AddTip(scene, Two);
        Assert.Null(Tick(reader, scene));
        scene.Now += Offsets.CompareScanMilliseconds;
        Assert.NotNull(Tick(reader, scene));
    }

    [Fact]
    public void NoTipPanelGivesNullEvenWithATooltip()
    {
        var scene = Open(tipPanel: false);
        AddTip(scene, Two);
        Assert.Null(Tick(Reader(scene), scene));
    }

    [Fact]
    public void ATipPanelGoneForOneWalkStillGivesRows()
    {
        var scene = Open();
        var reader = Reader(scene);
        AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(scene.TipPanel + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(scene.TipPanel + Offsets.UWidgetVisibility, (byte)4);
        Assert.NotNull(Tick(reader, scene));
    }

    [Fact]
    public void ATipPanelGoneForTwoWalksGivesNullAndRescansOnReturn()
    {
        var scene = Open();
        var reader = Reader(scene);
        AddTip(scene, Two);
        Assert.NotNull(Tick(reader, scene));
        scene.World.Memory.Write(scene.TipPanel + Offsets.UWidgetVisibility, (byte)1);
        Tick(reader, scene);
        Assert.Null(Tick(reader, scene));
        scene.World.Memory.Write(scene.TipPanel + Offsets.UWidgetVisibility, (byte)4);
        scene.Now += Offsets.CompareScanMilliseconds;
        Assert.NotNull(Tick(reader, scene));
    }

    [Fact]
    public void VersionIsNonZeroAndStableWhileNothingIsRewritten()
    {
        var scene = Open();
        var reader = Reader(scene);
        AddTip(scene, Two);
        var first = Tick(reader, scene)!.Version;
        Assert.NotEqual(0, first);
        Assert.Equal(first, Tick(reader, scene)!.Version);
    }

    [Fact]
    public void VersionChangesWhenATextIsRewritten()
    {
        var scene = Open();
        var reader = Reader(scene);
        var tip = AddTip(scene, Two);
        var before = Tick(reader, scene)!.Version;
        scene.Ui.Text(tip.Damage[0], Offsets.TextBlockText, "+12.3%");
        Assert.NotEqual(before, Tick(reader, scene)!.Version);
    }

    [Fact]
    public void VersionChangesWhenOnlyTheSecondRowIsRewritten()
    {
        var scene = Open();
        var reader = Reader(scene);
        var tip = AddTip(scene, Two);
        var before = Tick(reader, scene)!.Version;
        scene.Ui.Text(tip.Survival[1], Offsets.TextBlockText, "+9.5%");
        Assert.NotEqual(before, Tick(reader, scene)!.Version);
    }

    [Fact]
    public void ReadsAreThrottledToTheRefreshInterval()
    {
        var scene = Open();
        var reader = Reader(scene);
        var tip = AddTip(scene, Two);
        var shown = Tick(reader, scene);
        scene.Now -= 1;
        scene.Ui.Text(tip.Damage[0], Offsets.TextBlockText, "+99.0%");
        reader.Read();
        Assert.Same(shown, reader.ReadCompare());
        scene.Now += 1;
        Assert.Equal(99f, Rows(Tick(reader, scene))[0].Damage);
    }
}
