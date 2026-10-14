using System;
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

    public int Version { get; private set; }

    public long StaleSignature(long now, long staleAfter)
    {
        long signature = 0;
        foreach (var (page, cached) in _pages)
            if (now - cached.ReadAt > staleAfter) signature += (page + 1) * unchecked((long)0x9E3779B97F4A7C15);
        return signature;
    }

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
            foreach (var page in _pages.Keys.Where(page => page > shown.Page).ToList())
            {
                _pages.Remove(page);
                Version++;
            }
        }
        else if (shown.Page >= _lastPage) _lastPage = null;
        if (!_pages.TryGetValue(shown.Page, out var cached) || !SameListings(cached.Listings, shown.Listings)) Version++;
        _pages[shown.Page] = (shown.Listings, now);
    }

    static bool SameListings(TradeListing[] a, TradeListing[] b) => ReferenceEquals(a, b) || a.AsSpan().SequenceEqual(b);

    public void Clear()
    {
        if (_pages.Count > 0) Version++;
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
