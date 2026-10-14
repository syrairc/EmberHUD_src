using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

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
        Check.That(book.Find(2, 0)?.Status is not null, "sold row kept and marked");
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

    [Test]
    public void TheVersionHoldsWhenTheSameListingsAreReadAgain()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100), Listing(1, 2000, 150)), 0);
        var first = book.Version;

        book.Update(Page(1, Listing(0, 1000, 100), Listing(1, 2000, 150)), 50);
        book.Update(Page(1, Listing(0, 1000, 100), Listing(1, 2000, 150)), 100);

        Check.Equal(first, book.Version);
    }

    [Test]
    public void TheVersionMovesWhenAPageIsAddedChangedPrunedOrCleared()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1000, 100)), 0);
        var seen = book.Version;

        book.Update(Page(1, Listing(0, 1000, 101)), 10);
        Check.That(book.Version != seen, "a price changed");
        seen = book.Version;

        book.Update(Page(1, Listing(0, 1000, 101), Listing(1, 5, 5)), 20);
        Check.That(book.Version != seen, "a listing was added to the page");
        seen = book.Version;

        book.Update(Page(2, Listing(0, 1, 1)), 30);
        Check.That(book.Version != seen, "a new page");
        seen = book.Version;

        book.Update(Page(1, Listing(0, 1000, 101), Listing(1, 5, 5)) with { LastPage = true }, 40);
        Check.That(book.Version != seen, "page 2 is past the new last page and was dropped");
        seen = book.Version;

        book.Clear();
        Check.That(book.Version != seen, "cleared");
    }

    [Test]
    public void ClearingAnEmptyBookKeepsTheVersion()
    {
        var book = new Book();
        var start = book.Version;

        book.Clear();
        book.Update(null, 0);

        Check.Equal(start, book.Version);
    }

    [Test]
    public void StaleSignatureNamesTheStalePages()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1, 1)), 0);
        book.Update(Page(2, Listing(0, 1, 1)), 400);
        var none = book.StaleSignature(400, 500);
        var first = book.StaleSignature(600, 500);
        var both = book.StaleSignature(1000, 500);

        Check.That(none != first && first != both && none != both, "stale sets differ");
        Check.Equal(first, book.StaleSignature(600, 500));
    }

    [Test]
    public void StaleSignatureMovesWhenOnePageGoesStaleAsAnotherIsReread()
    {
        var book = new Book();
        book.Update(Page(1, Listing(0, 1, 1)), 0);
        book.Update(Page(2, Listing(0, 1, 1)), 400);
        var before = book.StaleSignature(600, 500);
        var version = book.Version;

        book.Update(Page(1, Listing(0, 1, 1)), 1000);

        Check.Equal(version, book.Version);
        Check.That(before != book.StaleSignature(1000, 500), "page 1 fresh and page 2 stale is not the same set");
    }

    [Test]
    public void StaleSignatureTellsApartSetsWhosePageNumbersAddUpTheSame()
    {
        var book = new Book();
        foreach (var page in new[] { 0, 3 }) book.Update(Page(page, Listing(0, 1, 1)), 0);
        foreach (var page in new[] { 1, 2 }) book.Update(Page(page, Listing(0, 1, 1)), 400);
        var zeroAndThree = book.StaleSignature(600, 500);

        foreach (var page in new[] { 0, 3 }) book.Update(Page(page, Listing(0, 1, 1)), 1000);
        var oneAndTwo = book.StaleSignature(1000, 500);

        Check.That(zeroAndThree != oneAndTwo, "stale {0,3} and stale {1,2} are different sets");
    }

    [Test]
    public void ListingAtFindsTheSlotOrNothing()
    {
        TradeListing[] listings = [Listing(0, 1, 1), Listing(4, 2, 2)];

        Check.Equal(2L, Book.ListingAt(listings, 4)!.Value.Quantity);
        Check.That(Book.ListingAt(listings, 1) is null, "no such slot");
        Check.That(new Book().Find(1, 0) is null, "no such page");
    }
}
