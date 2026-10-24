using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderBoothTests
{
    internal sealed class Booth
    {
        public required FakeWorld World;
        public required FakeUi Ui;
        public required nint Page;
        public required nint Switcher;
        public required nint[] Tiles;
        public required nint[][] Items;
        public required nint EntryClass;
        public long Now;
    }

    static readonly Vector2 ClipMin = new(1000, 300);
    static readonly Vector2 ClipMax = new(1900, 900);

    internal static Booth Open(int openTab = 0, int items = 3, int switcherIndex = 2, params int[] alsoVisible)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var size = new Vector2(3440, 1440);
        var page = ui.Widget(ui.UserClass(Offsets.BoothPageClassName), Offsets.BoothPageClassName + "_1", Vector2.Zero, size);
        var pageRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var switcher = ui.Widget(ui.PanelClass("WidgetSwitcher"), Offsets.BoothSwitcher, Vector2.Zero, size);
        world.Memory.Write(switcher + Offsets.SwitcherActiveIndex, switcherIndex);
        var tileClass = world.AddClass("UITileView");
        var tiles = Enumerable.Range(0, Offsets.InventoryTabCount).Select(i =>
            ui.Widget(tileClass, Offsets.BoothTilePrefix + (Offsets.InventoryTabFirst + i), ClipMin, ClipMax - ClipMin,
                visibility: i == openTab || alsoVisible.Contains(i) ? (byte)4 : (byte)1)).ToArray();
        ui.SetTree(page, pageRoot);
        ui.SetChildren(pageRoot, [switcher, .. tiles]);
        ui.AddRoot(page);

        var booth = new Booth
        {
            World = world, Ui = ui, Page = page, Switcher = switcher, Tiles = tiles, Items = new nint[tiles.Length][],
            EntryClass = world.AddClass("AuctionHouseTileItemV2_C"),
        };
        for (var i = 0; i < tiles.Length; i++)
        {
            booth.Items[i] = [.. Enumerable.Range(0, i == openTab ? items : 0).Select(_ => world.Alloc(0x40))];
            WriteArray(booth, tiles[i] + Offsets.ListViewItems, booth.Items[i]);
            WriteArray(booth, tiles[i] + Offsets.ListViewEntryPool, []);
        }
        return booth;
    }

    static void WriteArray(Booth booth, nint at, nint[] values)
    {
        var data = booth.World.Alloc(Math.Max(8, values.Length * 8));
        booth.World.Memory.WriteArray(data, values);
        booth.World.Memory.Write(at, data);
        booth.World.Memory.Write(at + Offsets.TArrayNum, values.Length);
    }

    internal static nint Entry(Booth booth, nint item, float x, float y)
    {
        var entry = booth.Ui.Widget(booth.EntryClass, "AuctionHouseTileItemV2_C", new Vector2(x, y), new Vector2(94, 94));
        booth.World.Memory.Write(entry + Offsets.BoothEntryItemData, item);
        return entry;
    }

    internal static void Pool(Booth booth, int tab, params nint[] entries) => WriteArray(booth, booth.Tiles[tab] + Offsets.ListViewEntryPool, entries);

    static BagView? Tick(GameUiReader reader, Booth booth)
    {
        reader.Read();
        var view = reader.ReadBooth();
        booth.Now += Offsets.BagRefreshMilliseconds;
        return view;
    }

    static GameUiReader Reader(Booth booth) => booth.World.UiReader(() => booth.Now);

    [Fact]
    public void OpenTileCellsCarrySlotAndRect()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310), Entry(booth, booth.Items[0][1], 1110, 310), Entry(booth, booth.Items[0][2], 1210, 310));
        var view = Tick(Reader(booth), booth)!;
        Assert.Equal((InventoryBag.Equipment, ClipMin, ClipMax, 3), (view.Tab, view.ClipMin, view.ClipMax, view.Cells.Length));
        Assert.Equal(new BagCell(1, new Vector2(1110, 310), new Vector2(1204, 404)), view.Cells.Single(c => c.Slot == 1));
    }

    [Fact]
    public void RecycledEntryUsesListItemIndexNotOrder()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][2], 1210, 310), Entry(booth, booth.Items[0][0], 1010, 310), Entry(booth, booth.Items[0][1], 1110, 310));
        var view = Tick(Reader(booth), booth)!;
        Assert.Equal([0, 1, 2], view.Cells.Select(c => c.Slot).Order());
        Assert.Equal(1210, view.Cells.Single(c => c.Slot == 2).Min.X);
    }

    [Fact]
    public void OtherSwitcherIndexIsNull()
    {
        var booth = Open(switcherIndex: 0);
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310));
        Assert.Null(Tick(Reader(booth), booth));
    }

    [Fact]
    public void NoTileOpenIsNull()
    {
        var booth = Open(openTab: -1);
        Assert.Null(Tick(Reader(booth), booth));
    }

    [Fact]
    public void HighestVisibleTileWins()
    {
        var booth = Open(openTab: 2, alsoVisible: 0);
        Pool(booth, 2, Entry(booth, booth.Items[2][0], 1010, 310));
        Assert.Equal(InventoryBag.Commodities, Tick(Reader(booth), booth)!.Tab);

        var equipment = Open(openTab: 0);
        Pool(equipment, 0, Entry(equipment, equipment.Items[0][0], 1010, 310));
        Assert.Equal(InventoryBag.Equipment, Tick(Reader(equipment), equipment)!.Tab);
    }

    [Fact]
    public void HiddenAndCollapsedTilesAreNeverPicked()
    {
        var booth = Open(openTab: 0, alsoVisible: 3);
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310));
        var reader = Reader(booth);
        Assert.Equal(InventoryBag.Other, Tick(reader, booth)!.Tab);
        foreach (var visibility in new byte[] { 2, 1 })
        {
            booth.World.Memory.Write(booth.Tiles[3] + Offsets.UWidgetVisibility, visibility);
            Assert.Equal(InventoryBag.Equipment, Tick(reader, booth)!.Tab);
        }
    }

    [Fact]
    public void MoreThanSixtyFourItemsReadFully()
    {
        var booth = Open(items: 72);
        Pool(booth, 0, [.. booth.Items[0].Select((item, i) => Entry(booth, item, 1010 + i % 9 * 10, 310 + i / 9 * 10))]);
        Assert.Equal(Enumerable.Range(0, 72), Tick(Reader(booth), booth)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void CellOutsideClipIsDropped()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310), Entry(booth, booth.Items[0][1], 1110, 950), Entry(booth, booth.Items[0][2], 1960, 310));
        Assert.Equal([0], Tick(Reader(booth), booth)!.Cells.Select(c => c.Slot));
    }

    [Fact]
    public void EntryWithUnknownItemPointerIsSkipped()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310), Entry(booth, booth.World.Alloc(0x40), 1110, 310), Entry(booth, 0, 1210, 310));
        Assert.Equal([0], Tick(Reader(booth), booth)!.Cells.Select(c => c.Slot));
    }

    [Fact]
    public void DuplicateIndexIsDropped()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310), Entry(booth, booth.Items[0][1], 1110, 310), Entry(booth, booth.Items[0][1], 1210, 310));
        Assert.Equal([0], Tick(Reader(booth), booth)!.Cells.Select(c => c.Slot));
    }

    [Fact]
    public void ClosedPageIsNull()
    {
        var booth = Open();
        Pool(booth, 0, Entry(booth, booth.Items[0][0], 1010, 310));
        var reader = Reader(booth);
        Assert.NotNull(Tick(reader, booth));
        booth.World.Memory.Write(booth.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, booth));
        Assert.Null(Tick(reader, booth));
    }

    [Fact]
    public void NoBoothRootIsNull()
    {
        var world = new FakeWorld();
        var reader = world.UiReader();
        reader.Read();
        Assert.Null(reader.ReadBooth());
    }
}
