using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderBagTests
{
    sealed class Bag
    {
        public required FakeWorld World;
        public required FakeUi Ui;
        public required nint Page;
        public required nint[] Lists;
        public required nint[][] Items;
        public required nint RowClass;
        public required nint CellClass;
        public long Now;
    }

    static readonly Vector2 ClipMin = new(2505, 930);
    static readonly Vector2 ClipMax = new(3405, 1340);

    static Bag Open(int openTab = 2, int items = 7, double offset = 0)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var size = new Vector2(3440, 1440);
        var page = ui.Widget(ui.UserClass("PCBag_C"), "PCBag_C_1", Vector2.Zero, size);
        var main = ui.Widget(ui.UserClass("PCBagMain"), "PCBagMain", Vector2.Zero, size);
        var grid = ui.Widget(ui.UserClass("PCUIGridListView_C"), "PCUIGridListView", Vector2.Zero, size);
        var listClass = world.AddClass("UIListView");
        var lists = Enumerable.Range(0, Offsets.InventoryTabCount).Select(i =>
            ui.Widget(listClass, Offsets.BagListPrefix + (Offsets.InventoryTabFirst + i), ClipMin, ClipMax - ClipMin, visibility: i == openTab ? (byte)4 : (byte)1)).ToArray();
        var pageRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var mainRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var gridRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        ui.SetTree(page, pageRoot);
        ui.SetChildren(pageRoot, main);
        ui.SetTree(main, mainRoot);
        ui.SetChildren(mainRoot, grid);
        ui.SetTree(grid, gridRoot);
        ui.SetChildren(gridRoot, lists);
        ui.AddRoot(page);

        var bag = new Bag
        {
            World = world, Ui = ui, Page = page, Lists = lists, Items = new nint[lists.Length][],
            RowClass = world.AddClass("PCUIGridListViewItem_C"), CellClass = world.AddClass("PCEquipItem_C"),
        };
        for (var i = 0; i < lists.Length; i++)
        {
            var count = i == openTab ? items : 0;
            bag.Items[i] = [.. Enumerable.Range(0, count).Select(_ => world.Alloc(0x40))];
            WriteArray(bag, lists[i] + Offsets.ListViewItems, bag.Items[i]);
            WriteArray(bag, lists[i] + Offsets.ListViewEntryPool, []);
            world.Memory.Write(lists[i] + Offsets.ListViewEntrySpacing, 5f);
            SetOffset(bag, lists[i], offset);
        }
        return bag;
    }

    static void WriteArray(Bag bag, nint at, nint[] values)
    {
        var data = bag.World.Alloc(Math.Max(8, values.Length * 8));
        bag.World.Memory.WriteArray(data, values);
        bag.World.Memory.Write(at, data);
        bag.World.Memory.Write(at + Offsets.TArrayNum, values.Length);
    }

    static void SetOffset(Bag bag, nint list, double offset)
    {
        bag.World.Memory.TryRead(list + Offsets.UWidgetSlate, out nint slate);
        bag.World.Memory.Write(slate + Offsets.ListViewScrollOffset, offset);
    }

    static nint Row(Bag bag, nint item, float y)
    {
        var ui = bag.Ui;
        var row = ui.Widget(bag.RowClass, "PCUIGridListViewItem_C", new Vector2(2505, y), new Vector2(900, 135));
        bag.World.Memory.Write(row + Offsets.BagRowItemData, item);
        for (var c = 0; c < Offsets.BagRowCellCount; c++)
            bag.World.Memory.Write(row + Offsets.BagRowCells + 8 * c, ui.Widget(bag.CellClass, "PCEquipItem", new Vector2(2508 + 100 * c, y), new Vector2(94, 135)));
        return row;
    }

    static void Pool(Bag bag, int tab, params nint[] rows) => WriteArray(bag, bag.Lists[tab] + Offsets.ListViewEntryPool, rows);

    static BagView? Tick(GameUiReader reader, Bag bag)
    {
        reader.Read();
        var view = reader.ReadBag();
        bag.Now += Offsets.BagRefreshMilliseconds;
        return view;
    }

    static GameUiReader Reader(Bag bag) => bag.World.UiReader(() => bag.Now);

    [Fact]
    public void OpenTabCellsCarrySlotAndRect()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), Row(bag, bag.Items[2][1], 1070), Row(bag, bag.Items[2][2], 1210));
        var view = Tick(Reader(bag), bag)!;
        Assert.Equal((InventoryBag.Commodities, ClipMin, ClipMax, 27), (view.Tab, view.ClipMin, view.ClipMax, view.Cells.Length));
        Assert.Equal(new BagCell(10, new Vector2(2608, 1070), new Vector2(2702, 1205)), view.Cells.Single(c => c.Slot == 10));
    }

    [Fact]
    public void RecycledRowUsesListItemIndexNotOrder()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][2], 1210), Row(bag, bag.Items[2][0], 930), Row(bag, bag.Items[2][1], 1070));
        var view = Tick(Reader(bag), bag)!;
        Assert.Equal(Enumerable.Range(0, 27), view.Cells.Select(c => c.Slot).Order());
        Assert.Equal(1210, view.Cells.Single(c => c.Slot == 18).Min.Y);
    }

    [Fact]
    public void StaleRowOutsideScrollRangeIsDropped()
    {
        var bag = Open(items: 8, offset: 2.23);
        Pool(bag, 2, Row(bag, bag.Items[2][2], 903), Row(bag, bag.Items[2][3], 1043), Row(bag, bag.Items[2][4], 1183), Row(bag, bag.Items[2][5], 1323), Row(bag, bag.Items[2][1], 815));
        var slots = Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order().ToArray();
        Assert.DoesNotContain(slots, slot => slot is >= 9 and <= 17);
        Assert.Equal(Enumerable.Range(18, 36), slots);
    }

    [Fact]
    public void StaleRowPastTheBottomOfTheScrollRangeIsDropped()
    {
        var bag = Open(items: 8, offset: 2.23);
        Pool(bag, 2, Row(bag, bag.Items[2][3], 1043), Row(bag, bag.Items[2][6], 1000), Row(bag, bag.Items[2][7], 1100));
        var slots = Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order().ToArray();
        Assert.Equal(Enumerable.Range(27, 9), slots);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-135f)]
    [InlineData(-1f)]
    public void GarbageEntrySpacingIsNull(float spacing)
    {
        var bag = Open(items: 8, offset: 2.23);
        Pool(bag, 2, Row(bag, bag.Items[2][3], 1043), Row(bag, bag.Items[2][7], 1100));
        bag.World.Memory.Write(bag.Lists[2] + Offsets.ListViewEntrySpacing, spacing);
        Assert.Null(Tick(Reader(bag), bag));
    }

    [Fact]
    public void RowWithoutGeometryIsSkipped()
    {
        var bag = Open();
        var flat = Row(bag, bag.Items[2][1], 1070);
        bag.World.Memory.TryRead(flat + Offsets.UWidgetSlate, out nint slate);
        bag.World.Memory.WriteArray(slate + Offsets.SWidgetSize, new[] { 0f, 0f });
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), flat);
        Assert.Equal(Enumerable.Range(0, 9), Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order());
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(65, false)]
    [InlineData(-1, true)]
    [InlineData(65, true)]
    public void GarbageArrayCountIsNull(int count, bool pool)
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930));
        bag.World.Memory.Write(bag.Lists[2] + (pool ? Offsets.ListViewEntryPool : Offsets.ListViewItems) + Offsets.TArrayNum, count);
        Assert.Null(Tick(Reader(bag), bag));
    }

    [Fact]
    public void CellsOutsideClipAreDropped()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), Row(bag, bag.Items[2][1], 1070), Row(bag, bag.Items[2][2], 1345));
        var slots = Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).ToArray();
        Assert.Equal(18, slots.Length);
        Assert.All(slots, slot => Assert.True(slot < 18));
    }

    [Fact]
    public void NoVisibleListIsNull()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930));
        var reader = Reader(bag);
        Assert.NotNull(Tick(reader, bag));
        bag.World.Memory.Write(bag.Lists[2] + Offsets.UWidgetVisibility, (byte)1);
        Assert.Null(Tick(reader, bag));
        bag.World.Memory.Write(bag.Lists[0] + Offsets.UWidgetVisibility, (byte)4);
        bag.World.Memory.Write(bag.Lists[2] + Offsets.UWidgetVisibility, (byte)4);
        Assert.Null(Tick(reader, bag));
    }

    [Fact]
    public void GarbageScrollOffsetIsNull()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930));
        var reader = Reader(bag);
        SetOffset(bag, bag.Lists[2], double.NaN);
        Assert.Null(Tick(reader, bag));
        SetOffset(bag, bag.Lists[2], 99);
        Assert.Null(Tick(reader, bag));
        SetOffset(bag, bag.Lists[2], -1);
        Assert.Null(Tick(reader, bag));
    }

    [Fact]
    public void TwoRowsOnTheSameItemDropThatItemOnly()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), Row(bag, bag.Items[2][1], 1070), Row(bag, bag.Items[2][1], 1210));
        Assert.Equal(Enumerable.Range(0, 9), Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void RowWithNullItemIsSkipped()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), Row(bag, 0, 1070));
        Assert.Equal(Enumerable.Range(0, 9), Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void RowOfAnotherListIsSkipped()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930), Row(bag, bag.World.Alloc(0x40), 1070));
        Assert.Equal(Enumerable.Range(0, 9), Tick(Reader(bag), bag)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void ReadsAreThrottledToTheRefreshInterval()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930));
        var reader = Reader(bag);
        var shown = Tick(reader, bag);
        bag.Now -= 1;
        Pool(bag, 2);
        reader.Read();
        Assert.Same(shown, reader.ReadBag());
        bag.Now += 1;
        Assert.Empty(Tick(reader, bag)!.Cells);
    }

    [Fact]
    public void ClosedBagGivesNull()
    {
        var bag = Open();
        Pool(bag, 2, Row(bag, bag.Items[2][0], 930));
        var reader = Reader(bag);
        Assert.NotNull(Tick(reader, bag));
        bag.World.Memory.Write(bag.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, bag));
        Assert.Null(Tick(reader, bag));
    }
}
