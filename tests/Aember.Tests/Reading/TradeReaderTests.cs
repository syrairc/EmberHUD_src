using System.Numerics;
using Aember.Host;
using Aember.Host.Reading;

namespace Aember.Tests;

public sealed class TradeReaderTests
{
    sealed class Market
    {
        public required FakeWorld World;
        public required FakeUi Ui;
        public required long Page;
        public required long PageText;
        public required long Next;
        public required long Grid;
        public required List<(long Cell, long Filled, long Status, long Quantity, long Price, long Name, long StatusText)> Cells;
        public long Now;
    }

    static Market Open(int cells, int filled, int page = 1, bool last = false)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var size = new Vector2(3440, 1440);
        var pageWidget = ui.Widget(ui.UserClass(Offsets.TradePageClassName), Offsets.TradePageClassName + "_1", Vector2.Zero, size);
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var pageText = ui.Widget(ui.PanelClass("UIShrinkTextBlock"), Offsets.TradePageText, new Vector2(1700, 1300), new Vector2(80, 20));
        var box = ui.Widget(ui.UserClass("AutoWrapBox_C"), Offsets.TradeItemBox, Vector2.Zero, size);
        var boxRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var next = ui.Widget(ui.PanelClass("CanvasPanel"), Offsets.TradeNextArrow, new Vector2(3380, 700), new Vector2(40, 80), visibility: last ? (byte)1 : (byte)4);
        var grid = ui.Widget(ui.PanelClass("WrapBox"), Offsets.TradeGrid, new Vector2(203, 150), new Vector2(3034, 1090));
        ui.SetTree(pageWidget, root);
        ui.SetChildren(root, pageText, box);
        ui.SetTree(box, boxRoot);
        ui.SetChildren(boxRoot, next, grid);
        ui.Text(pageText, Offsets.ShrinkTextContent, $"Page {page}");

        var cellClass = ui.UserClass("AuctionHouseStallGoodItemV2_C");
        var text = ui.UserClass("UITextBlock");
        var shrink = ui.PanelClass("UIShrinkTextBlock");
        var canvas = ui.PanelClass("CanvasPanel");
        var market = new Market { World = world, Ui = ui, Page = pageWidget, PageText = pageText, Next = next, Grid = grid, Cells = [] };
        for (var i = 0; i < cells; i++)
        {
            var min = new Vector2(203 + 169 * (i % 18), 150 + 274 * (i / 18));
            var cell = ui.Widget(cellClass, "AuctionHouseStallGoodItemV2_C", min, new Vector2(163, 267), nameNumber: (uint)i + 1);
            var cellRoot = ui.Widget(canvas, "CanvasPanel_0", min, new Vector2(163, 267));
            var filledPanel = ui.Widget(canvas, Offsets.TradeCellFilled, min, new Vector2(163, 267), visibility: i < filled ? (byte)4 : (byte)1);
            var status = ui.Widget(canvas, Offsets.TradeCellStatus, min, new Vector2(163, 267), visibility: 1);
            var quantity = ui.Widget(text, Offsets.TradeCellQuantity, min, new Vector2(60, 20));
            var price = ui.Widget(text, Offsets.TradeCellPrice, min, new Vector2(60, 20));
            var name = ui.Widget(shrink, Offsets.TradeCellName, min, new Vector2(150, 20));
            var statusText = ui.Widget(text, Offsets.TradeCellStatusText, min, new Vector2(60, 20));
            ui.SetTree(cell, cellRoot);
            ui.SetChildren(cellRoot, filledPanel, status);
            ui.SetChildren(filledPanel, quantity, price, name);
            ui.SetChildren(status, statusText);
            ui.Text(quantity, Offsets.TextBlockText, $"{1000 + i}");
            ui.Text(price, Offsets.TextBlockText, $"{100 + i}");
            ui.Text(name, Offsets.ShrinkTextContent, "Precious Ember");
            ui.Text(statusText, Offsets.TextBlockText, "Item Name");
            market.Cells.Add((cell, filledPanel, status, quantity, price, name, statusText));
        }
        ui.SetChildren(grid, [.. market.Cells.Select(c => c.Cell)]);
        ui.AddRoot(ui.Host(pageWidget));
        return market;
    }

    static GameUiReader Reader(Market market)
    {
        var minimap = new MinimapReader(market.World.Memory, unchecked((nint)FakeWorld.ModuleBase));
        return new GameUiReader(market.World.Memory, unchecked((nint)FakeWorld.ModuleBase), minimap.TryReadName, () => market.Now);
    }

    static TradePage? Tick(GameUiReader reader, Market market)
    {
        reader.Read();
        var page = reader.ReadTrade();
        market.Now += Offsets.TradeRefreshMilliseconds;
        return page;
    }

    static TradePage? Settle(GameUiReader reader, Market market)
    {
        Tick(reader, market);
        return Tick(reader, market);
    }

    [Fact]
    public void ReadsTheFilledCellsWithPageRectsAndPriceIcon()
    {
        var market = Open(72, 61, page: 6, last: true);
        var page = Settle(Reader(market), market)!;
        Assert.Equal((6, true, 72, 61), (page.Page, page.LastPage, page.CellsPerPage, page.Listings.Length));
        Assert.Equal(new TradeListing(0, "Precious Ember", 1000, 100, null, new Vector2(203, 150), new Vector2(366, 417)), page.Listings[0]);
        Assert.Equal(new Vector2(203 + 169, 150 + 274), page.Listings[19].Min);
        Assert.Equal(Aember.Draw.GameIconPrefix + Offsets.TradePriceTexture, page.PriceIcon);
    }

    [Fact]
    public void CollapsedCellsAreNotListings()
    {
        var market = Open(10, 4);
        Assert.Equal([0, 1, 2, 3], Settle(Reader(market), market)!.Listings.Select(l => l.Slot));
    }

    [Fact]
    public void NothingIsShownUntilTwoReadsAgree()
    {
        var market = Open(4, 4);
        var reader = Reader(market);
        Assert.Null(Tick(reader, market));
        market.Ui.Text(market.Cells[2].Price, Offsets.TextBlockText, "999");
        Assert.Null(Tick(reader, market));
        Assert.Equal(999, Tick(reader, market)!.Listings[2].Price);
    }

    [Fact]
    public void ReadsAreThrottledToTheRefreshInterval()
    {
        var market = Open(4, 4);
        var reader = Reader(market);
        var shown = Settle(reader, market);
        market.Now -= 1;
        market.Ui.Text(market.Cells[0].Price, Offsets.TextBlockText, "1");
        reader.Read();
        Assert.Same(shown, reader.ReadTrade());
        market.Now += 1;
        Tick(reader, market);
        Assert.Equal(1, Tick(reader, market)!.Listings[0].Price);
    }

    [Fact]
    public void ATornReadKeepsTheLastGoodPage()
    {
        var market = Open(4, 4);
        var reader = Reader(market);
        var shown = Settle(reader, market);
        market.Ui.Text(market.Cells[1].Quantity, Offsets.TextBlockText, "");
        Assert.Same(shown, Tick(reader, market));
        Assert.Same(shown, Tick(reader, market));
    }

    [Fact]
    public void ASoldCellHasTheGamesStatusAndNoName()
    {
        var market = Open(4, 4);
        var cell = market.Cells[2];
        market.World.Memory.Write(cell.Status + Offsets.UWidgetVisibility, (byte)4);
        market.Ui.Text(cell.StatusText, Offsets.TextBlockText, "Sold");
        market.World.Memory.Write(cell.Name + Offsets.ShrinkTextContent, 0L);
        var sold = Settle(Reader(market), market)!.Listings[2];
        Assert.Equal(("", "Sold", 1002L, 102L), (sold.Name, sold.Status, sold.Quantity, sold.Price));
    }

    [Fact]
    public void ThePageNumberIsTheDigitsInAnyLanguage()
    {
        var market = Open(2, 2);
        market.Ui.Text(market.PageText, Offsets.ShrinkTextContent, "第3页");
        Assert.Equal(3, Settle(Reader(market), market)!.Page);
    }

    [Fact]
    public void AClosedMarketGivesNull()
    {
        var market = Open(2, 2);
        var reader = Reader(market);
        Assert.NotNull(Settle(reader, market));
        market.World.Memory.Write(market.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, market));
        Assert.Null(Tick(reader, market));
    }

    [Fact]
    public void OneTornWalkKeepsTheShownPage()
    {
        var market = Open(2, 2);
        var reader = Reader(market);
        Settle(reader, market);
        market.World.Memory.Write(market.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, market));
        market.World.Memory.Write(market.Page + Offsets.UWidgetVisibility, (byte)4);
        Assert.NotNull(Tick(reader, market));
        market.World.Memory.Write(market.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, market));
    }

    [Fact]
    public void ANewCellListIsFoundAgain()
    {
        var market = Open(6, 6);
        var reader = Reader(market);
        Assert.Equal(6, Settle(reader, market)!.CellsPerPage);
        market.Ui.SetChildren(market.Grid, [.. market.Cells.Take(3).Select(c => c.Cell)]);
        Assert.Equal(3, Settle(reader, market)!.CellsPerPage);
    }

    [Fact]
    public void NoTradePageGivesNull()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);
        var minimap = new MinimapReader(world.Memory, unchecked((nint)FakeWorld.ModuleBase));
        var reader = new GameUiReader(world.Memory, unchecked((nint)FakeWorld.ModuleBase), minimap.TryReadName, () => 0);
        reader.Read();
        Assert.Null(reader.ReadTrade());
    }

    [Theory]
    [InlineData("9,999", 9999L)]
    [InlineData("Page 12", 12L)]
    [InlineData("99980001", 99980001L)]
    [InlineData("", null)]
    [InlineData("Sold", null)]
    [InlineData("99999999999999999999999", null)]
    public void NumbersKeepOnlyDigits(string text, long? expected) => Assert.Equal(expected, GameUiReader.Number(text));
}
