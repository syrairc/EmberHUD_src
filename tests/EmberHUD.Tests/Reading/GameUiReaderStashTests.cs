using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderStashTests
{
    internal sealed class Stash
    {
        public required FakeWorld World;
        public required nint Page;
        public required nint Grid;
        public required nint Strip;
        public required nint[] Titles;
        public long Now;
    }

    static readonly Vector2 ClipMin = new(1990, 290);
    static readonly Vector2 ClipMax = new(2710, 710);

    internal static Stash Open(byte[]? selects = null, (int Row, int Column)[]? slots = null)
    {
        selects ??= [4, 1, 1];
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var size = new Vector2(3440, 1440);
        var page = ui.Widget(ui.UserClass("WareHouse_C"), "WareHouse_C_1", Vector2.Zero, size);
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var scroll = ui.Widget(ui.PanelClass("UIScrollBox"), Offsets.StashScroll, ClipMin, ClipMax - ClipMin);
        var grid = ui.Widget(ui.PanelClass("UniformGridPanel"), Offsets.StashGrid, ClipMin, ClipMax - ClipMin);
        var strip = ui.Widget(ui.PanelClass("HorizontalBox"), Offsets.StashTabStrip, Vector2.Zero, size);
        var titleClass = world.AddClass("WareHouseTitleItem_C");
        var titles = selects.Select(select =>
        {
            var title = ui.Widget(titleClass, "WareHouseTitleItem_C", Vector2.Zero, new Vector2(100, 40));
            world.Memory.Write(title + Offsets.TitleSelectIcon, ui.Widget(world.AddClass("Image"), "SelectIcon", Vector2.Zero, new Vector2(10, 10), select));
            return title;
        }).ToArray();
        ui.SetTree(page, root);
        ui.SetChildren(root, scroll, grid, strip);
        ui.SetChildren(strip, titles);
        ui.AddRoot(page);

        var cellClass = world.AddClass("EquipItem_C");
        slots ??= [.. Enumerable.Range(0, Offsets.StashCells).Select(i => (i / Offsets.StashColumns, i % Offsets.StashColumns))];
        var data = world.Alloc(8 * slots.Length);
        for (var i = 0; i < slots.Length; i++)
        {
            var (row, column) = slots[i];
            var cell = ui.Widget(cellClass, "EquipItem_C", new Vector2(2000 + 100 * column, 300 + 80 * row), new Vector2(90, 70));
            var slot = world.Alloc(0x80);
            world.Memory.Write(slot + Offsets.PanelSlotContent, cell);
            world.Memory.Write(slot + Offsets.GridSlotRow, row);
            world.Memory.Write(slot + Offsets.GridSlotColumn, column);
            world.Memory.Write(data + 8 * i, slot);
        }
        world.Memory.Write(grid + Offsets.PanelSlots, data);
        world.Memory.Write(grid + Offsets.PanelSlots + Offsets.TArrayNum, slots.Length);
        return new Stash { World = world, Page = page, Grid = grid, Strip = strip, Titles = titles };
    }

    static StashView? Tick(GameUiReader reader, Stash stash)
    {
        reader.Read();
        var view = reader.ReadStash();
        stash.Now += Offsets.StashRefreshMilliseconds;
        return view;
    }

    static GameUiReader Reader(Stash stash) => stash.World.UiReader(() => stash.Now);

    [Fact]
    public void ReadsThirtyFiveCellsRowMajor()
    {
        var stash = Open();
        var view = Tick(Reader(stash), stash)!;
        Assert.Equal((0, ClipMin, ClipMax, 35), (view.Tab, view.ClipMin, view.ClipMax, view.Cells.Length));
        Assert.Equal(new BagCell(9, new Vector2(2200, 380), new Vector2(2290, 450)), view.Cells.Single(c => c.Slot == 9));
        Assert.Equal(Enumerable.Range(0, 35), view.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void SlotUsesRowAndColumnNotListOrder()
    {
        var stash = Open(slots: [.. Enumerable.Range(0, 35).Reverse().Select(i => (i / 7, i % 7))]);
        var view = Tick(Reader(stash), stash)!;
        Assert.Equal(new Vector2(2200, 380), view.Cells.Single(c => c.Slot == 9).Min);
        Assert.Equal(new Vector2(2600, 620), view.Cells.Single(c => c.Slot == 34).Min);
    }

    [Fact]
    public void OpenTabIsTheVisibleSelectIcon()
    {
        var stash = Open([1, 4, 1]);
        var reader = Reader(stash);
        Assert.Equal(1, Tick(reader, stash)!.Tab);
        Assert.Equal(1, reader.OpenStashTab);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 4, 1)]
    public void NoOrTwoOpenTabsIsNull(byte a, byte b, byte add)
    {
        var stash = Open([a, b, add]);
        var reader = Reader(stash);
        Assert.Null(Tick(reader, stash));
        Assert.Null(reader.OpenStashTab);
    }

    [Fact]
    public void AddButtonIsNotATab()
    {
        var stash = Open([1, 1, 4]);
        Assert.Null(Tick(Reader(stash), stash));
    }

    [Fact]
    public void PageClosedIsNull()
    {
        var stash = Open();
        var reader = Reader(stash);
        Assert.NotNull(Tick(reader, stash));
        stash.World.Memory.Write(stash.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, stash));
        Assert.Null(Tick(reader, stash));
        Assert.Null(reader.OpenStashTab);
        Assert.Null(Reader(new Stash { World = new FakeWorld(), Page = 0, Grid = 0, Strip = 0, Titles = [] }).ReadStash());
    }

    [Fact]
    public void BadRowOrColumnDropsCell()
    {
        var stash = Open(slots: [(0, 0), (1, -1), (1, 7), (5, 0), (1, 2)]);
        Assert.Equal([0, 9], Tick(Reader(stash), stash)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void ZeroMiddleTitleSlotIsNull()
    {
        var stash = Open([1, 1, 4, 1]);
        stash.World.Memory.TryRead(stash.Strip + Offsets.PanelSlots, out nint data);
        stash.World.Memory.Write(data + 8, (nint)0);
        Assert.Null(Tick(Reader(stash), stash));
    }

    [Fact]
    public void MissingWidgetsAreRetriedOnlyAfterTheRetryWindow()
    {
        var stash = Open();
        stash.World.Memory.Write(stash.Grid + Offsets.UObjectClass, stash.World.AddClass("Image"));
        var reader = Reader(stash);
        reader.Read();
        Assert.Null(reader.ReadStash());
        var reads = stash.World.Memory.Reads.Count;
        stash.Now += Offsets.RectChildRetryMilliseconds - 1;
        Assert.Null(reader.ReadStash());
        Assert.Equal(reads, stash.World.Memory.Reads.Count);
        stash.Now += 1;
        Assert.Null(reader.ReadStash());
        Assert.True(stash.World.Memory.Reads.Count > reads);
    }

    [Fact]
    public void DuplicateRowAndColumnDropBoth()
    {
        var stash = Open(slots: [(0, 0), (1, 2), (1, 2), (1, 3)]);
        Assert.Equal([0, 10], Tick(Reader(stash), stash)!.Cells.Select(c => c.Slot).Order());
    }

    [Fact]
    public void TitleOfTheWrongClassIsNull()
    {
        var stash = Open();
        stash.World.Memory.Write(stash.Titles[1] + Offsets.UObjectClass, stash.World.AddClass("Image"));
        Assert.Null(Tick(Reader(stash), stash));
    }

    [Fact]
    public void GridOfTheWrongClassIsNull()
    {
        var stash = Open();
        stash.World.Memory.Write(stash.Grid + Offsets.UObjectClass, stash.World.AddClass("Image"));
        Assert.Null(Tick(Reader(stash), stash));
    }

    [Fact]
    public void TabChangeInsideTheRefreshWindowIsPublishedAtOnce()
    {
        var stash = Open([4, 1, 1]);
        var reader = Reader(stash);
        Assert.Equal(0, Tick(reader, stash)!.Tab);
        stash.Now -= 1;
        stash.World.Memory.TryRead(stash.Titles[0] + Offsets.TitleSelectIcon, out nint first);
        stash.World.Memory.TryRead(stash.Titles[1] + Offsets.TitleSelectIcon, out nint second);
        stash.World.Memory.Write(first + Offsets.UWidgetVisibility, (byte)1);
        stash.World.Memory.Write(second + Offsets.UWidgetVisibility, (byte)4);
        reader.Read();
        Assert.Equal(1, reader.ReadStash()!.Tab);
        Assert.Equal(1, reader.OpenStashTab);
    }

    [Fact]
    public void ReadsAreThrottledToTheRefreshInterval()
    {
        var stash = Open();
        var reader = Reader(stash);
        var shown = Tick(reader, stash);
        stash.Now -= 1;
        reader.Read();
        Assert.Same(shown, reader.ReadStash());
    }
}
