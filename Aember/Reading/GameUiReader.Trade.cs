using System.Numerics;

namespace Aember.Host.Reading;

internal sealed partial class GameUiReader
{
    private readonly record struct TradeCell(nint Cell, nint Filled, nint Status, nint Quantity, nint Price, nint Name, nint StatusText);

    private sealed record TradeWidgets(nint Page, nint Grid, nint PageText, nint NextArrow, TradeCell[] Cells);

    private nint _tradePage;
    private int _tradeMisses;
    private TradeWidgets? _tradeWidgets;
    private TradePage? _tradeShown;
    private TradePage? _tradeLastRead;
    private long _tradeReadAt = long.MinValue;

    public TradePage? ReadTrade()
    {
        if (_tradePage == 0)
        {
            _tradeWidgets = null;
            _tradeShown = _tradeLastRead = null;
            _tradeReadAt = long.MinValue;
            return null;
        }

        var now = _clock();
        if (now < _tradeReadAt) return _tradeShown;
        _tradeReadAt = now + Offsets.TradeRefreshMilliseconds;

        var widgets = _tradeWidgets;
        if (widgets is null || widgets.Page != _tradePage || !SameCells(widgets))
            _tradeWidgets = widgets = TradeWidgetsOf(_tradePage);
        if (widgets is null)
        {
            _tradeReadAt = now + Offsets.RectChildRetryMilliseconds;
            return _tradeShown;
        }
        if (!TryOverlay(out _, out var origin) || ReadTradePage(widgets, origin) is not { } read) return _tradeShown;
        if (_tradeLastRead is { } last && SameRead(last, read)) _tradeShown = read;
        _tradeLastRead = read;
        return _tradeShown;
    }

    internal static long? Number(string text)
    {
        long value = 0;
        var any = false;
        foreach (var c in text)
        {
            if (c is < '0' or > '9') continue;
            if (value > (long.MaxValue - 9) / 10) return null;
            value = value * 10 + (c - '0');
            any = true;
        }
        return any ? value : null;
    }

    private TradeWidgets? TradeWidgetsOf(nint page)
    {
        var box = FindChild(page, Offsets.TradeItemBox);
        if (box == 0) return null;
        var grid = FindChild(box, Offsets.TradeGrid);
        var next = FindChild(box, Offsets.TradeNextArrow);
        var pageText = FindChild(page, Offsets.TradePageText);
        if (grid == 0 || next == 0 || pageText == 0) return null;

        List<TradeCell> cells = [];
        foreach (var cell in PanelContents(grid, Offsets.MaxTradeCells))
        {
            var found = new TradeCell(cell,
                FindChild(cell, Offsets.TradeCellFilled), FindChild(cell, Offsets.TradeCellStatus),
                FindChild(cell, Offsets.TradeCellQuantity), FindChild(cell, Offsets.TradeCellPrice),
                FindChild(cell, Offsets.TradeCellName), FindChild(cell, Offsets.TradeCellStatusText));
            if (found.Filled == 0 || found.Status == 0 || found.Quantity == 0 || found.Price == 0 || found.Name == 0 || found.StatusText == 0) return null;
            cells.Add(found);
        }
        return cells.Count == 0 ? null : new TradeWidgets(page, grid, pageText, next, [.. cells]);
    }

    private bool SameCells(TradeWidgets widgets) =>
        PanelContents(widgets.Grid, Offsets.MaxTradeCells).SequenceEqual(widgets.Cells.Select(cell => cell.Cell));

    private TradePage? ReadTradePage(TradeWidgets widgets, Vector2 origin)
    {
        if (!TryText(widgets.PageText + Offsets.ShrinkTextContent, out var pageText) || Number(pageText) is not { } page || page is <= 0 or > int.MaxValue) return null;

        List<TradeListing> listings = [];
        for (var slot = 0; slot < widgets.Cells.Length; slot++)
        {
            var cell = widgets.Cells[slot];
            if (!Visible(cell.Filled)) continue;
            if (!TryText(cell.Quantity + Offsets.TextBlockText, out var quantityText) || Number(quantityText) is not { } quantity ||
                !TryText(cell.Price + Offsets.TextBlockText, out var priceText) || Number(priceText) is not { } price ||
                Geometry(cell.Cell, origin) is not { } rect)
                return null;
            var name = TryText(cell.Name + Offsets.ShrinkTextContent, out var named) ? named : "";
            string? status = Visible(cell.Status) ? TryText(cell.StatusText + Offsets.TextBlockText, out var said) ? said : "" : null;
            listings.Add(new TradeListing(slot, name, quantity, price, status, rect.Min, rect.Max));
        }
        return new TradePage((int)page, !Visible(widgets.NextArrow), widgets.Cells.Length, [.. listings], Aember.Draw.GameIconPrefix + Offsets.TradePriceTexture);
    }

    private static bool SameRead(TradePage a, TradePage b) =>
        a.Page == b.Page && a.LastPage == b.LastPage && a.CellsPerPage == b.CellsPerPage && a.Listings.AsSpan().SequenceEqual(b.Listings);
}
