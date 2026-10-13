using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;

namespace Market;

public sealed class BookTests
{
    static TradeListing Listing(int slot, long quantity, long price, string name = "Precious Ember", string? status = null) =>
        new(slot, name, quantity, price, status, new Vector2(slot * 10, 0), new Vector2(slot * 10 + 8, 8));

    static TradePage Page(int page, params TradeListing[] listings) => new(page, false, 72, listings, "game:fe");

    [Test]
    public void RevisitReplacesThePage()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100), Listing(1, 2000, 150)), 0);
        book.Update(Page(2, Listing(0, 500, 40)), 10);
        book.Update(Page(1, Listing(0, 1000, 100)), 20);
        Check.Equal("1:0,2:0", string.Join(',', book.Rows(20, 1000).Select(r => $"{r.Page}:{r.Listing.Slot}")));
    }

    [Test]
    public void ResultsClosingClearsEverything()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100)), 0);
        book.Update(null, 10);
        Check.Equal(0, book.Count);
    }

    [Test]
    public void AnotherItemClearsEverything()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100)), 0);
        book.Update(Page(2, Listing(0, 5, 900, "Flame Sand")), 10);
        Check.Equal("2", string.Join(',', book.Rows(10, 1000).Select(r => r.Page)));
    }

    [Test]
    public void ASoldCellDoesNotLookLikeAnotherItem()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100)), 0);
        book.Update(Page(2, Listing(0, 1442, 115, "", "Sold"), Listing(1, 1000, 129)), 10);
        Check.Equal(2, book.Count);
        Check.That(book.Find(2, 0)?.Sold == true, "sold row kept and marked");
    }

    [Test]
    public void ACellsPerPageChangeClearsEverything()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100)), 0);
        book.Update(Page(2, Listing(0, 1000, 100)) with { CellsPerPage = 40 }, 10);
        Check.Equal(1, book.Count);
    }

    [Test]
    public void PagesTextCountsUntilTheLastPageIsSeen()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1, 1)), 0);
        book.Update(Page(3, Listing(0, 1, 1)), 0);
        Check.Equal("2 / 3+", book.PagesText);
        book.Update(Page(4, Listing(0, 1, 1)) with { LastPage = true }, 0);
        Check.Equal("3 / 4", book.PagesText);
        book.Update(Page(5, Listing(0, 1, 1)), 0);
        Check.Equal("4 / 5+", book.PagesText);
    }

    [Test]
    public void OldPagesGoStale()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1, 1)), 0);
        book.Update(Page(2, Listing(0, 1, 1)), 400);
        Check.Equal("True,False", string.Join(',', book.Rows(700, 500).Select(r => r.Stale)));
    }

    [Test]
    public void NewLastPageDropsCachedPagesPastIt()
    {
        var book = new Book();
        for (var page = 1; page <= 6; page++) book.Update(Page(page, Listing(0, 1, page)), 0);
        book.Update(Page(4, Listing(0, 1, 4)) with { LastPage = true }, 0);
        Check.Equal("4 / 4", book.PagesText);
        Check.That(book.Rows(0, 1000).All(row => row.Page <= 4));
    }

    [Test]
    public void ClearForgetsTheItemAndLastPage()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1, 1)) with { LastPage = true }, 0);
        book.Clear();
        book.Update(Page(1, Listing(0, 5, 9, "Flame Sand")), 10);
        Check.Equal("1 / 1+", book.PagesText);
    }
}
