using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;

namespace Market;

public sealed class MainTests
{
    static Row Row(int page, int slot, long quantity, long price, string? status = null) =>
        new(page, new TradeListing(slot, status is null ? "Precious Ember" : "", quantity, price, status, new Vector2(slot * 10, 0), new Vector2(slot * 10 + 8, 8)), false);

    static readonly System.Collections.Generic.List<Row> Rows =
    [
        Row(1, 0, 1000, 129),
        Row(1, 1, 3712, 297),
        Row(2, 0, 95, 9999),
        Row(2, 1, 1442, 115, "Sold"),
        Row(3, 4, 100, 20),
    ];

    static string Keys(System.Collections.Generic.IEnumerable<Row> rows) => string.Join(',', rows.Select(r => $"{r.Page}:{r.Listing.Slot}"));

    [Test]
    public void DefaultOrderIsCheapestPerUnitAndSoldRowsAreDropped()
    {
        Check.Equal("1:1,1:0,3:4,2:0", Keys(Main.Shown(Rows, new Settings(), 1)));
    }

    [Test]
    public void EveryColumnSortsAndTheKeysOrderTheRows()
    {
        var columns = new Main().Columns();
        Check.That(columns.Skip(1).All(column => column.SortKey is not null), "every column but the checkbox has a sort key");
        var early = Row(1, 9, 1, 1);
        var late = Row(2, 0, 1, 1);
        Check.That(columns[2].SortKey!(early)!.CompareTo(columns[2].SortKey!(late)) < 0, "page sorts before slot");
    }

    [Test]
    public void BothFiltersApplyAndZeroIsOff()
    {
        var settings = new Settings();
        settings.MaxPrice.Value = 200;
        Check.Equal("1:0,3:4", Keys(Main.Shown(Rows, settings, 1).OrderBy(r => r.Page).ThenBy(r => r.Listing.Slot)));
        settings.MaxPrice.Value = 0;
        settings.MaxPerUnit.Value = 0.1f;
        Check.Equal("1:1", Keys(Main.Shown(Rows, settings, 1).OrderBy(r => r.Page).ThenBy(r => r.Listing.Slot)));
        settings.MaxPrice.Value = 120;
        Check.Equal("", Keys(Main.Shown(Rows, settings, 1)));
    }

    [Test]
    public void CurrentPageOnlyKeepsJustTheShownPage()
    {
        var settings = new Settings();
        Check.Equal("1:1,1:0,3:4,2:0", Keys(Main.Shown(Rows, settings, 1)));
        settings.CurrentPageOnly.Value = true;
        Check.Equal("2:0", Keys(Main.Shown(Rows, settings, 2)));
        Check.Equal("", Keys(Main.Shown(Rows, settings, 4)));
    }

    [Test]
    public void ZeroQuantityHasNoPerUnit()
    {
        var row = Row(1, 0, 0, 50);
        Check.That(double.IsPositiveInfinity(row.PerUnit), "per unit of qty 0");
        var settings = new Settings();
        settings.MaxPerUnit.Value = 5;
        Check.That(!Main.Passes(row, settings), "qty 0 never passes a per-unit filter");
    }

    [Test]
    public void TheWindowDrawsWithAndWithoutAMarket()
    {
        var main = new Main();
        TestGame.Set(GameState.Empty);
        TestFrame.Run(main.Render);
        TestGame.Set(GameState.Empty with { Trade = new TradePage(1, false, 72, [Rows[0].Listing, Rows[1].Listing], "game:missing") });
        main.Tick();
        TestFrame.Run(main.Render);
        Check.That(main.Settings.Show.Value, "drawing closed the window");
    }

    static TradePage Shown(int page, params Row[] rows) => new(page, false, 72, [.. rows.Select(r => r.Listing)], "game:missing");

    static string Slots(System.Collections.Generic.IEnumerable<TradeListing> listings) => string.Join(',', listings.Select(l => l.Slot));

    [Test]
    public void OnlySoldCellsAreDimmedWithNoFilters()
    {
        var page = Shown(1, Row(1, 0, 1000, 129), Row(1, 1, 3712, 297, "Sold"), Row(1, 2, 100, 20));
        Check.Equal("1", Slots(Main.Dimmed(page, new Settings())));
    }

    [Test]
    public void CellsOverTheMaxPriceOrMaxPerUnitAreDimmedToo()
    {
        var page = Shown(1, Row(1, 0, 1000, 129), Row(1, 1, 3712, 297), Row(1, 2, 100, 20), Row(1, 3, 50, 5, "Sold"));
        var settings = new Settings();
        settings.MaxPrice.Value = 200;
        Check.Equal("1,3", Slots(Main.Dimmed(page, settings)));
        settings.MaxPrice.Value = 0;
        settings.MaxPerUnit.Value = 0.1f;
        Check.Equal("0,2,3", Slots(Main.Dimmed(page, settings)));
    }

    [Test]
    public void APickOnThisPageIsHereAndOnAnotherPageIsOtherPage()
    {
        var book = new Book();
        var cheap = Row(2, 1, 3712, 297);
        book.Update(Shown(2, cheap), 0);
        book.Update(Shown(1, Row(1, 0, 1000, 129)), 0);
        Check.Equal(PickState.OtherPage, Main.StateOf(Pick.Of(cheap), Shown(1, Row(1, 0, 1000, 129)), book));
        Check.Equal(PickState.Here, Main.StateOf(Pick.Of(cheap), Shown(2, cheap), book));
    }

    [Test]
    public void ASoldOrChangedPickIsGone()
    {
        var book = new Book();
        var cheap = Row(1, 1, 3712, 297);
        book.Update(Shown(1, cheap), 0);
        Check.Equal(PickState.Gone, Main.StateOf(Pick.Of(cheap), Shown(1, Row(1, 1, 3712, 297, "Sold")), book));
        Check.Equal(PickState.Gone, Main.StateOf(Pick.Of(cheap), Shown(1, Row(1, 1, 500, 40)), book));
        Check.Equal(PickState.Gone, Main.StateOf(Pick.Of(cheap), Shown(1), book));
        book.Update(Shown(1, Row(1, 1, 500, 40)), 0);
        Check.Equal(PickState.Gone, Main.StateOf(Pick.Of(cheap), Shown(2), book));
    }

    [Test]
    public void TheGridDrawsWithASelectionOnAndOffThePage()
    {
        var main = new Main();
        var cheap = Row(1, 1, 3712, 297);
        TestGame.Set(GameState.Empty with { Trade = Shown(1, cheap, Row(1, 0, 1000, 129)) });
        main.Tick();
        main.Toggle(Pick.Of(cheap));
        TestFrame.Run(main.Render);
        TestGame.Set(GameState.Empty with { Trade = Shown(2, Row(2, 0, 5, 5)) });
        main.Tick();
        TestFrame.Run(main.Render);
        Check.That(main.Picks.Contains(Pick.Of(cheap)), "selection survives a page flip");
    }

    [Test]
    public void TogglingTwiceRemovesThePick()
    {
        var main = new Main();
        var pick = Pick.Of(Row(1, 1, 3712, 297));
        main.Toggle(pick);
        Check.Equal(1, main.Picks.Count);
        main.Toggle(pick);
        Check.Equal(0, main.Picks.Count);
    }

    [Test]
    public void PicksOnDifferentPagesSurviveAPageFlip()
    {
        var main = new Main();
        var first = Row(1, 1, 3712, 297);
        var second = Row(2, 0, 95, 9999);
        TestGame.Set(GameState.Empty with { Trade = Shown(1, first) });
        main.Tick();
        main.Toggle(Pick.Of(first));
        TestGame.Set(GameState.Empty with { Trade = Shown(2, second) });
        main.Tick();
        main.Toggle(Pick.Of(second));
        TestGame.Set(GameState.Empty with { Trade = Shown(1, first) });
        main.Tick();
        Check.Equal(2, main.Picks.Count);
    }

    [Test]
    public void APickWhoseListingSoldOrChangedIsRemovedOnTheNextTick()
    {
        var main = new Main();
        var sells = Row(1, 1, 3712, 297);
        var changes = Row(1, 2, 100, 20);
        var stays = Row(1, 3, 50, 5);
        TestGame.Set(GameState.Empty with { Trade = Shown(1, sells, changes, stays) });
        main.Tick();
        foreach (var row in new[] { sells, changes, stays }) main.Toggle(Pick.Of(row));
        TestGame.Set(GameState.Empty with { Trade = Shown(1, Row(1, 1, 3712, 297, "Sold"), Row(1, 2, 100, 25), stays) });
        main.Tick();
        Check.That(main.Picks.Count == 1 && main.Picks.Contains(Pick.Of(stays)), "only the unchanged pick is left");
    }

    [Test]
    public void ClosingTheMarketClearsThePicks()
    {
        var main = new Main();
        var row = Row(1, 1, 3712, 297);
        TestGame.Set(GameState.Empty with { Trade = Shown(1, row) });
        main.Tick();
        main.Toggle(Pick.Of(row));
        TestGame.Set(GameState.Empty);
        main.Tick();
        Check.Equal(0, main.Picks.Count);
    }
}
