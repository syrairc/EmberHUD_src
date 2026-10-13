using System.Globalization;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class SortableTableTests
{
    static readonly List<(string n, float w)> Weights = [("beta", 3f), ("alpha", 9f), ("gamma", 1f)];
    static readonly List<(string n, string? g)> Groups = [("a", "x"), ("b", null), ("c", "x")];
    static readonly List<string> Names = ["GoldChest", "ClamChest", "IzaroObject"];

    static List<int> Sorted<T>(List<T> rows, Func<T, IComparable?>? key, bool asc, List<int>? start = null)
    {
        var idx = start ?? [0, 1, 2];
        SortableTable.SortIndices(idx, rows, key, asc);
        return idx;
    }

    [Fact]
    public void SortsByTheKeyAscending() => Assert.Equal(new[] { 2, 0, 1 }, Sorted(Weights, r => r.w, true));

    [Fact]
    public void SortsByTheKeyDescending() => Assert.Equal(new[] { 1, 0, 2 }, Sorted(Weights, r => r.w, false));

    [Fact]
    public void ANullKeyLeavesTheOrderAlone() => Assert.Equal(new[] { 1, 0, 2 }, Sorted(Weights, null, true, [1, 0, 2]));

    [Fact]
    public void NullKeysSortFirstAndTiesKeepSourceOrder() => Assert.Equal(new[] { 1, 0, 2 }, Sorted(Groups, r => r.g, true));

    [Fact]
    public void TiesStayInSourceOrderWhenTheSortIsFlipped() => Assert.Equal(new[] { 0, 2, 1 }, Sorted(Groups, r => r.g, false, [1, 0, 2]));

    [Fact]
    public void SortingNeverTouchesTheSource()
    {
        Sorted(Weights, r => r.w, false);
        Sorted(Groups, r => r.g, false);
        Assert.Equal("beta", Weights[0].n);
        Assert.Equal("a", Groups[0].n);
    }

    [Fact]
    public void AnEmptyFilterShowsAll() => Assert.Equal(3, SortableTable.VisibleIndices(Names, s => s, "").Count);

    [Fact]
    public void TheFilterIsCaseInsensitiveAndKeepsSourceOrder() => Assert.Equal(new[] { 0, 1 }, SortableTable.VisibleIndices(Names, s => s, "chest"));

    [Fact]
    public void NoMatchIsEmpty() => Assert.Empty(SortableTable.VisibleIndices(Names, s => s, "zzz"));

    [Fact]
    public void NullLabelsDoNotThrow() => Assert.Empty(SortableTable.VisibleIndices(Names, _ => null, "x"));

    [Fact]
    public void NoLabelFuncMeansNoFiltering() => Assert.Equal(3, SortableTable.VisibleIndices(Names, null, "zzz").Count);

    sealed class Probe
    {
        static int created;
        readonly string id = "tbl" + Interlocked.Increment(ref created);
        public List<int> Drawn = [];
        public Vector2 WeightHeader;
        public Vector2 RowHeader;
        public float WeightCellWidth;
        public bool Balanced = true;
        public List<(string n, float w)> Rows = Weights;
        public string Filter = "";
        public bool WithFilterBox = true;
        public float MaxHeight;

        public string Window => "probe" + id;

        public void Frame() => UiFrame.Run(Window, Body);

        public void Body()
        {
            Drawn.Clear();
            WeightHeader = default;
            RowHeader = default;
            TableColumn<(string n, float w)>[] columns =
            [
                new() { Header = "Name", SortKey = r => r.n, Draw = (r, i) => { Drawn.Add(i); ImGui.TextUnformatted(r.n); } },
                new() { Header = "Weight", Width = 80f, SortKey = r => r.w, Draw = (r, _) => { if (WeightHeader == default) { var style = ImGui.GetStyle(); var at = ImGui.GetCursorScreenPos(); WeightHeader = new Vector2(at.X + 4f, at.Y - style.CellPadding.Y - (ImGui.GetTextLineHeight() / 2 + style.CellPadding.Y)); } WeightCellWidth = ImGui.GetContentRegionAvail().X; ImGui.TextUnformatted(r.w.ToString(CultureInfo.InvariantCulture)); } },
                new() { Header = "Row", Draw = (_, i) => { if (RowHeader == default) { var style = ImGui.GetStyle(); var at = ImGui.GetCursorScreenPos(); RowHeader = new Vector2(at.X + 4f, at.Y - style.CellPadding.Y - (ImGui.GetTextLineHeight() / 2 + style.CellPadding.Y)); } ImGui.TextUnformatted("row " + i); } },
            ];
            var depth = ImGuiP.GetCurrentWindow().IDStack.Size;
            SortableTable.Draw(id, Rows, columns, ref Filter, WithFilterBox ? r => r.n : null, MaxHeight);
            Balanced &= ImGuiP.GetCurrentTable().IsNull && ImGuiP.GetCurrentWindow().IDStack.Size == depth;
        }
    }

    static void ResetIo()
    {
        UiFrame.Unhover();
    }

    [Fact]
    public void TablesDrawOneBalancedFrame()
    {
        var probe = new Probe();
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.True(probe.Balanced);
            Assert.Equal(new[] { 0, 1, 2 }, probe.Drawn);
            Assert.Equal("beta", Weights[0].n);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void AMissingTableOrColumnsDrawsNothingAndStaysBalanced()
    {
        var filter = "";
        var depth = 0;
        var balanced = false;
        var moved = true;
        UiFrame.Run("probe", () =>
        {
            depth = ImGuiP.GetCurrentWindow().IDStack.Size;
            var before = ImGui.GetCursorScreenPos();
            SortableTable.Draw("none", null, [new TableColumn<string> { Header = "n" }], ref filter);
            SortableTable.Draw("empty", Names, [], ref filter);
            SortableTable.Draw("nullcols", Names, null, ref filter);
            balanced = ImGuiP.GetCurrentWindow().IDStack.Size == depth && ImGuiP.GetCurrentTable().IsNull;
            moved = ImGui.GetCursorScreenPos() != before;
        });
        Assert.True(balanced);
        Assert.False(moved);
    }

    [Fact]
    public void TheSearchBoxFiltersRowsAndKeepsSourceIndices()
    {
        var probe = new Probe { Filter = "al" };
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.Equal(new[] { 1 }, probe.Drawn);
            Assert.True(probe.Balanced);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void NoFilterTextFuncDrawsEveryRowWhateverTheFilterSays()
    {
        var probe = new Probe { Filter = "al", WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.Equal(new[] { 0, 1, 2 }, probe.Drawn);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void ClickingAHeaderSortsAscendingThenDescending()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            var first = probe.Drawn.ToList();
            Assert.Equal(new[] { 2, 0, 1 }, first);
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            var second = probe.Drawn.ToList();
            Assert.Equal(first.AsEnumerable().Reverse(), second);
            Assert.True(probe.Balanced);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void ATristateHeaderGoesAscendingDescendingThenBackToTheSourceOrder()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            Assert.Equal(new[] { 1, 0, 2 }, probe.Drawn);
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            Assert.Equal(new[] { 0, 1, 2 }, probe.Drawn);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void AColumnWithoutASortKeyCannotBeSortedBy()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            Assert.Equal(new[] { 2, 0, 1 }, probe.Drawn);
            UiFrame.Click(probe.Window, probe.RowHeader, probe.Body);
            Assert.Equal(new[] { 2, 0, 1 }, probe.Drawn);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void TheSearchBoxTakesOneFrameRowAboveTheTableOnlyWithAFilterTextFunc()
    {
        var withBox = new Probe { WithFilterBox = true };
        var without = new Probe { WithFilterBox = false };
        var rowHeight = 0f;
        try
        {
            withBox.Frame();
            withBox.Frame();
            without.Frame();
            without.Frame();
            UiFrame.Run("probe", () => rowHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
            Assert.Equal(rowHeight, withBox.WeightHeader.Y - without.WeightHeader.Y, 0.01f);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void AScrollingTableOnlyDrawsTheRowsOnScreen()
    {
        var rows = Enumerable.Range(0, 2000).Select(i => ("row" + i, (float)i)).ToList();
        var probe = new Probe { Rows = rows, WithFilterBox = false, MaxHeight = 120f };
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.InRange(probe.Drawn.Count, 1, 100);
            Assert.Equal(0, probe.Drawn[0]);
            Assert.True(probe.Balanced);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void ANonScrollingTableDrawsEveryRow()
    {
        var rows = Enumerable.Range(0, 200).Select(i => ("row" + i, (float)i)).ToList();
        var probe = new Probe { Rows = rows, WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.Equal(200, probe.Drawn.Count);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void TheVisibleListIsOnlyRebuiltWhenItsInputsChange()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            var builds = SortableTable.FilterBuilds;
            probe.Frame();
            probe.Frame();
            Assert.Equal(builds, SortableTable.FilterBuilds);

            probe.Rows = [.. Weights];
            probe.Frame();
            Assert.Equal(builds + 1, SortableTable.FilterBuilds);
            probe.Frame();
            Assert.Equal(builds + 1, SortableTable.FilterBuilds);

            probe.Rows.Add(("delta", 4f));
            probe.Frame();
            Assert.Equal(builds + 2, SortableTable.FilterBuilds);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void ChangingTheFilterRebuildsTheVisibleListOnce()
    {
        var probe = new Probe();
        try
        {
            probe.Frame();
            probe.Frame();
            var builds = SortableTable.FilterBuilds;
            probe.Filter = "al";
            probe.Frame();
            probe.Frame();
            Assert.Equal(builds + 1, SortableTable.FilterBuilds);
            Assert.Equal(new[] { 1 }, probe.Drawn);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void TheSortIsOnlyRebuiltWhenTheColumnOrDirectionChanges()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            var sorts = SortableTable.SortBuilds;
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            var ascending = SortableTable.SortBuilds;
            Assert.Equal(sorts + 1, ascending);
            probe.Frame();
            probe.Frame();
            Assert.Equal(ascending, SortableTable.SortBuilds);
            UiFrame.Click(probe.Window, probe.WeightHeader, probe.Body);
            Assert.Equal(ascending + 1, SortableTable.SortBuilds);
            Assert.Equal(new[] { 1, 0, 2 }, probe.Drawn);
        }
        finally { ResetIo(); }
    }

    [Fact]
    public void StringKeysSortOrdinallyIgnoringCase()
    {
        List<string> rows = ["beta", "Alpha", "gamma", "Beta2"];
        var idx = new List<int> { 0, 1, 2, 3 };

        SortableTable.SortIndices(idx, rows, r => r, true);

        Assert.Equal(new[] { 1, 0, 3, 2 }, idx);
    }

    [Fact]
    public void AFixedWidthColumnKeepsItsDeclaredWidth()
    {
        var probe = new Probe { WithFilterBox = false };
        try
        {
            probe.Frame();
            probe.Frame();
            Assert.Equal(80f, probe.WeightCellWidth, 0.5f);
        }
        finally { ResetIo(); }
    }
}
