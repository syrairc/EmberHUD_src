using System.Collections.Generic;
using System.Linq;
using EmberHUD;

namespace Market;

internal readonly record struct Row(int Page, TradeListing Listing, bool Stale)
{
    public double PerUnit => Listing.Quantity > 0 ? (double)Listing.Price / Listing.Quantity : double.PositiveInfinity;

    public bool Sold => Listing.Status is not null;
}

internal sealed class Book
{
    readonly SortedDictionary<int, (TradeListing[] Listings, long ReadAt)> _pages = [];
    string _item = "";
    int _cellsPerPage;
    int? _lastPage;

    public int Count => _pages.Count;

    public string PagesText => _lastPage is { } last ? $"{_pages.Count} / {last}" : $"{_pages.Count} / {(_pages.Count == 0 ? 0 : _pages.Keys.Last())}+";

    public void Update(TradePage? shown, long now)
    {
        if (shown is null)
        {
            Clear();
            return;
        }
        var item = shown.Listings.Select(l => l.Name).FirstOrDefault(name => name.Length > 0) ?? "";
        if (shown.CellsPerPage != _cellsPerPage || item.Length > 0 && _item.Length > 0 && item != _item) Clear();
        _cellsPerPage = shown.CellsPerPage;
        if (item.Length > 0) _item = item;
        if (shown.LastPage)
        {
            _lastPage = shown.Page;
            foreach (var page in _pages.Keys.Where(page => page > shown.Page).ToList()) _pages.Remove(page);
        }
        else if (shown.Page >= _lastPage) _lastPage = null;
        _pages[shown.Page] = (shown.Listings, now);
    }

    public void Clear()
    {
        _pages.Clear();
        _item = "";
        _cellsPerPage = 0;
        _lastPage = null;
    }

    public List<Row> Rows(long now, long staleAfter) =>
        [.. _pages.SelectMany(page => page.Value.Listings.Select(listing => new Row(page.Key, listing, now - page.Value.ReadAt > staleAfter)))];

    public Row? Find(int page, int slot) =>
        _pages.TryGetValue(page, out var cached) && cached.Listings.FirstOrDefault(l => l.Slot == slot) is { Name: not null } listing ? new Row(page, listing, false) : null;
}
