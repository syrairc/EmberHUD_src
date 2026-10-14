using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class ListEditorTests : IDisposable
{
    static string Moved(int from, int to)
    {
        List<string> list = ["A", "B", "C"];
        ListEditor.Move(list, from, to);
        return string.Concat(list);
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void MoveForward() => Assert.Equal("BCA", Moved(0, 2));

    [Fact]
    public void MoveBackward() => Assert.Equal("CAB", Moved(2, 0));

    [Fact]
    public void MoveOutOfRangeIsANoOp() => Assert.Equal("ABC", Moved(1, 9));

    [Theory]
    [InlineData(1, 1, 4, 1, 0, 2)]
    [InlineData(1, -1, 4, 1, 0, 0)]
    [InlineData(0, -1, 4, 1, 0, -1)]
    [InlineData(2, 1, 4, 1, 0, -1)]
    [InlineData(3, -1, 4, 1, 0, -1)]
    [InlineData(1, 0, 4, 1, 0, -1)]
    [InlineData(2, 1, 4, 0, 0, 3)]
    [InlineData(2, -1, 4, 0, 1, 1)]
    [InlineData(1, -1, 4, 0, 1, -1)]
    [InlineData(0, 1, 4, 0, 1, -1)]
    [InlineData(1, 1, 4, 1, 1, 2)]
    [InlineData(1, 1, 3, 1, 2, -1)]
    public void SwapTargetKeepsPinnedRowsPut(int from, int dir, int count, int pinnedTail, int pinnedHead, int expected) =>
        Assert.Equal(expected, ListEditor.SwapTarget(from, dir, count, pinnedTail, pinnedHead));

    [Fact]
    public void UntouchedListsReportNoChangeAndLeaveTheirDataAlone()
    {
        List<string> compact = ["alpha", "beta", "gamma"];
        List<string> ordered = ["Priority", "Currency", "Maps", "Everything else"];
        var selected = 1;
        var changed = false;
        var opts = new ListOpts { PinnedHead = 1, PinnedTail = 1, FilterText = i => ordered[i], Menu = i => ImGui.MenuItem("Delete") };

        UiFrame.Run("test", () =>
        {
            changed |= ListEditor.Compact("buttons", compact, (s, _) => { ImGui.TextUnformatted(s); return false; }, AddSource<string>.Button(() => "new"), reorderable: true, confirmDelete: true, filterText: s => s);
            changed |= ListEditor.Compact("combo", compact, (s, _) => { ImGui.TextUnformatted(s); return false; }, AddSource<string>.Combo([("delta", "Delta")], key => key));
            changed |= ListEditor.ReorderableList("ordered", ordered, ref selected, (s, _) => ImGui.TextUnformatted(s), opts);
        });

        Assert.False(changed);
        Assert.Equal(3, compact.Count);
        Assert.Equal("Priority", ordered[0]);
        Assert.Equal(1, selected);
    }

    const string Window = "listeditor";

    readonly record struct RowInfo(Vector2 Cursor, Vector2 Min, Vector2 Max, float Alpha, uint Probe);

    readonly UiFrame.ModalMetrics Modal = UiFrame.ReadModalMetrics();

    readonly float FrameHeight = UiFrame.Style.FrameHeight;

    readonly float FontSize = UiFrame.Style.FontSize;

    readonly Vector2 Spacing = UiFrame.Style.Spacing;

    static readonly string[] Original = ["alpha", "beta", "gamma"];

    readonly float BaseAlpha = UiFrame.Style.Alpha;

    sealed class CompactList
    {
        public readonly List<string> Items = [.. Original];
        public string Id = UiFrame.NextId("list");
        public AddSource<string>? Add;
        public bool Reorderable;
        public bool Confirm;
        public Func<string, string>? FilterText;
        public int Returns;
        public Vector2 Origin;
        public readonly List<RowInfo> Rows = [];
        public readonly List<uint> ExpectedProbes = [];
        public Vector2 ModalPos;

        public void Draw()
        {
            Origin = ImGui.GetCursorScreenPos();
            Rows.Clear();
            ExpectedProbes.Clear();
            for (var i = 0; i < Items.Count; i++)
            {
                ImGui.PushID(Id);
                ImGui.PushID(i);
                ExpectedProbes.Add(ImGui.GetID("probe"));
                ImGui.PopID();
                ImGui.PopID();
            }
            if (ListEditor.Compact(Id, Items, Row, Add, Reorderable, Confirm, FilterText)) Returns++;
            var modal = ImGuiP.FindWindowByName("Delete?##eidel");
            if (!modal.IsNull) ModalPos = modal.Pos;
        }

        bool Row(string text, int index)
        {
            var cursor = ImGui.GetCursorScreenPos();
            ImGui.TextUnformatted(text);
            Rows.Add(new RowInfo(cursor, ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetStyle().Alpha, ImGui.GetID("probe")));
            return false;
        }
    }

    Vector2 XButton(CompactList list, int row) => new(list.Rows[row].Max.X + Spacing.X + UiFrame.ButtonWidth("x") / 2, list.Rows[row].Cursor.Y + FrameHeight / 2);

    Vector2 Grip(CompactList list, int row) => new(list.Rows[row].Cursor.X - Spacing.X - UiFrame.ButtonWidth(":::") / 2, list.Rows[row].Cursor.Y + FrameHeight / 2);

    Vector2 ModalButton(CompactList list, bool confirm) => UiFrame.ConfirmModalButton(list.ModalPos, confirm, Modal);

    void EraseFilter(Action body, Vector2 box) => UiFrame.EraseText(Window, body, box);

    [Fact]
    public void CompactButtonAddAppendsTheFactoryItemOnce()
    {
        var list = new CompactList { Add = AddSource<string>.Button(() => "new") };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, list.Origin + new Vector2(UiFrame.ButtonWidth("+ Add") / 2, FrameHeight / 2), list.Draw);
        Assert.Equal(["alpha", "beta", "gamma", "new"], list.Items);
        Assert.Equal(1, list.Returns);
    }

    [Fact]
    public void CompactButtonAddUsesTheCallerLabelForItsWidth()
    {
        var list = new CompactList { Add = AddSource<string>.Button(() => "new", "Add a much longer label") };
        UiFrame.Run(Window, list.Draw);
        var farRight = list.Origin + new Vector2(UiFrame.ButtonWidth("Add a much longer label") - 2f, FrameHeight / 2);
        UiFrame.Click(Window, farRight, list.Draw);
        Assert.Equal(4, list.Items.Count);
    }

    sealed class Walker : IEnumerable<(string key, string label)>
    {
        public readonly Dictionary<string, Vector2> RowAt = [];

        public IEnumerator<(string key, string label)> GetEnumerator()
        {
            RowAt.Clear();
            foreach (var c in new[] { ("apple", "Apple"), ("banana", "Banana"), ("cherry", "Cherry") })
            {
                RowAt[c.Item1] = ImGui.GetCursorScreenPos();
                yield return c;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void CompactComboAddMakesTheItemFromTheKeyNotTheLabel()
    {
        var walker = new Walker();
        var list = new CompactList { Add = AddSource<string>.Combo(walker, key => "made:" + key) };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, list.Origin + new Vector2(125f, FrameHeight / 2), list.Draw);
        UiFrame.Run(Window, list.Draw);

        Assert.Equal(3, list.Items.Count);
        UiFrame.Click(Window, walker.RowAt["banana"] + new Vector2(10f, FontSize / 2), list.Draw);
        Assert.Equal(["alpha", "beta", "gamma", "made:banana"], list.Items);
        Assert.Equal(1, list.Returns);
    }

    [Fact]
    public void CompactComboAddFilterIsKeptPerListScopeAndSurvivesTheBoxLosingFocus()
    {
        var walkerA = new Walker();
        var a = new CompactList { Add = AddSource<string>.Combo(walkerA, key => key) };
        var b = new CompactList { Id = a.Id, Add = AddSource<string>.Combo(new Walker(), key => key) };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        UiFrame.Run(Window, Body);
        UiFrame.Click(Window, a.Origin + new Vector2(125f, FrameHeight / 2), Body);
        UiFrame.Run(Window, Body);
        var firstRow = walkerA.RowAt["apple"];
        UiFrame.Click(Window, firstRow + new Vector2(20f, -Spacing.Y - FrameHeight / 2), Body);
        UiFrame.Io().AddInputCharactersUTF8("ban");
        UiFrame.Run(Window, Body);
        UiFrame.Io().AddKeyEvent(ImGuiKey.Enter, true);
        UiFrame.Run(Window, Body);
        UiFrame.Io().AddKeyEvent(ImGuiKey.Enter, false);
        UiFrame.Run(Window, Body);
        UiFrame.Run(Window, Body);
        Assert.Equal(walkerA.RowAt["apple"], walkerA.RowAt["banana"]);
        Assert.NotEqual(walkerA.RowAt["banana"], walkerA.RowAt["cherry"]);
    }

    [Fact]
    public void CompactReportsAnEditMadeByTheRowDrawer()
    {
        List<string> items = ["alpha"];
        var returned = false;
        UiFrame.Run(Window, () => returned = ListEditor.Compact(UiFrame.NextId("edit"), items, (_, _) => true));
        Assert.True(returned);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompactXButtonRemovesExactlyThatRow(int row)
    {
        var list = new CompactList();
        UiFrame.Run(Window, list.Draw);
        var expected = list.Items.Where((_, i) => i != row).ToList();
        UiFrame.Click(Window, XButton(list, row), list.Draw);
        Assert.Equal(expected, list.Items);
        Assert.Equal(1, list.Returns);
    }

    [Fact]
    public void CompactRowsAreScopedUnderTheListIdAndTheRowIndex()
    {
        var list = new CompactList { Id = "scoped" };
        UiFrame.Run(Window, list.Draw);
        Assert.Equal(list.ExpectedProbes, list.Rows.Select(r => r.Probe));
        Assert.Equal(3, list.Rows.Select(r => r.Probe).Distinct().Count());
    }

    [Fact]
    public void CompactTheSameIdUnderTwoScopesGivesDifferentRowIds()
    {
        var a = new CompactList();
        var b = new CompactList { Id = a.Id };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        UiFrame.Run(Window, Body);
        Assert.Empty(a.Rows.Select(r => r.Probe).Intersect(b.Rows.Select(r => r.Probe)));
    }

    [Fact]
    public void CompactConfirmDeleteAsksFirstAndConfirmRemovesTheClickedRow()
    {
        var list = new CompactList { Confirm = true };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, XButton(list, 1), list.Draw);
        Assert.True(UiFrame.PopupOpen(Window, list.Draw));
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(0, list.Returns);
        UiFrame.Click(Window, ModalButton(list, true), list.Draw);
        Assert.Equal(["alpha", "gamma"], list.Items);
        Assert.Equal(1, list.Returns);
        Assert.False(UiFrame.PopupOpen(Window, list.Draw));
    }

    [Fact]
    public void CompactConfirmDeleteCancelKeepsTheRowAndReportsNothing()
    {
        var list = new CompactList { Confirm = true };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, XButton(list, 0), list.Draw);
        Assert.True(UiFrame.PopupOpen(Window, list.Draw));
        UiFrame.Click(Window, ModalButton(list, false), list.Draw);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(0, list.Returns);
        Assert.False(UiFrame.PopupOpen(Window, list.Draw));
    }

    [Fact]
    public void CompactConfirmDeleteBelongsToTheListWhoseXWasClicked()
    {
        var other = new CompactList { Confirm = true };
        var owner = new CompactList { Confirm = true };
        void Body()
        {
            other.Draw();
            owner.Draw();
        }
        UiFrame.Run(Window, Body);
        UiFrame.Click(Window, XButton(owner, 1), Body);
        Assert.True(UiFrame.PopupOpen(Window, Body));
        UiFrame.Click(Window, ModalButton(owner, true), Body);
        Assert.Equal(["alpha", "gamma"], owner.Items);
        Assert.Equal(Original, other.Items);
        Assert.Equal(1, owner.Returns);
        Assert.Equal(0, other.Returns);
    }

    [Fact]
    public void CompactGripsAppearOnlyWhenReorderable()
    {
        var plain = new CompactList();
        var grips = new CompactList { Reorderable = true };
        UiFrame.Run(Window, plain.Draw);
        UiFrame.Run(Window, grips.Draw);
        Assert.Equal(plain.Origin.X, plain.Rows[0].Cursor.X);
        Assert.Equal(grips.Origin.X + UiFrame.ButtonWidth(":::") + Spacing.X, grips.Rows[0].Cursor.X);
    }

    [Fact]
    public void CompactDraggingAGripOntoARowMovesTheItem()
    {
        var list = new CompactList { Reorderable = true };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, Grip(list, 0), Grip(list, 2), list.Draw);
        Assert.Equal(["beta", "gamma", "alpha"], list.Items);
        Assert.Equal(1, list.Returns);
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, Grip(list, 2), Grip(list, 0), list.Draw);
        Assert.Equal(Original, list.Items);
        Assert.Equal(2, list.Returns);
    }

    [Fact]
    public void CompactDroppingAGripOnNothingChangesNothing()
    {
        var list = new CompactList { Reorderable = true };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Press(Window, Grip(list, 1), list.Draw);
        UiFrame.MoveTo(Window, Grip(list, 1) + new Vector2(UiFrame.WindowSize.X / 2, 0), list.Draw);
        UiFrame.MoveTo(Window, Grip(list, 1) + new Vector2(UiFrame.WindowSize.X / 2, 0), list.Draw);
        UiFrame.Release(Window, list.Draw);
        Assert.Equal(Original, list.Items);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void CompactDroppingAGripAfterCrossingRowsButOffTheListChangesNothing()
    {
        var list = new CompactList { Reorderable = true };
        UiFrame.Run(Window, list.Draw);
        var away = Grip(list, 1) + new Vector2(UiFrame.WindowSize.X / 2, 0);
        UiFrame.Press(Window, Grip(list, 0), list.Draw);
        for (var row = 1; row <= 2; row++)
        {
            UiFrame.MoveTo(Window, Grip(list, row), list.Draw);
            UiFrame.MoveTo(Window, Grip(list, row), list.Draw);
        }
        UiFrame.MoveTo(Window, away, list.Draw);
        UiFrame.MoveTo(Window, away, list.Draw);
        UiFrame.Release(Window, list.Draw);
        Assert.Equal(Original, list.Items);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void CompactReleasingInTheGapBetweenRowsIsANoOp()
    {
        var list = new CompactList { Reorderable = true };
        UiFrame.Run(Window, list.Draw);
        var gap = new Vector2(Grip(list, 1).X, Grip(list, 1).Y + FrameHeight / 2 + Spacing.Y / 2);
        UiFrame.Press(Window, Grip(list, 0), list.Draw);
        UiFrame.MoveTo(Window, Grip(list, 1), list.Draw);
        UiFrame.MoveTo(Window, Grip(list, 1), list.Draw);
        UiFrame.MoveTo(Window, gap, list.Draw);
        UiFrame.MoveTo(Window, gap, list.Draw);
        UiFrame.Release(Window, list.Draw);
        Assert.Equal(Original, list.Items);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void CompactADragInOneScopeNeverMovesTheSameIdListInAnother()
    {
        var a = new CompactList { Reorderable = true };
        var b = new CompactList { Id = a.Id, Reorderable = true };
        void Body()
        {
            ImGui.BeginGroup();
            ImGui.PushID("A");
            a.Draw();
            ImGui.PopID();
            ImGui.EndGroup();
            ImGui.SameLine(300f);
            ImGui.BeginGroup();
            ImGui.PushID("B");
            b.Draw();
            ImGui.PopID();
            ImGui.EndGroup();
        }
        UiFrame.Run(Window, Body);
        UiFrame.Press(Window, Grip(a, 0), Body);
        UiFrame.MoveTo(Window, Grip(b, 2), Body);
        UiFrame.MoveTo(Window, Grip(b, 2), Body);
        UiFrame.Release(Window, Body);
        Assert.Equal(Original, a.Items);
        Assert.Equal(Original, b.Items);
        Assert.Equal(0, a.Returns + b.Returns);
    }

    [Fact]
    public void CompactADragReleasedInAListIsAppliedOnlyByThatListEvenWhenAnotherSameIdListDrawsFirst()
    {
        var owner = new CompactList { Reorderable = true };
        var other = new CompactList { Id = owner.Id, Reorderable = true };
        void Body()
        {
            ImGui.PushID("B");
            other.Draw();
            ImGui.PopID();
            ImGui.PushID("A");
            owner.Draw();
            ImGui.PopID();
        }
        UiFrame.Run(Window, Body);
        UiFrame.Drag(Window, Grip(owner, 0), Grip(owner, 2), Body);
        Assert.Equal(["beta", "gamma", "alpha"], owner.Items);
        Assert.Equal(Original, other.Items);
        Assert.Equal(0, other.Returns);
    }

    [Fact]
    public void CompactFilterDimsNonMatchingRowsAndTheBoxSitsAboveTheRows()
    {
        var list = new CompactList { FilterText = s => s };
        var alpha = BaseAlpha;
        try
        {
            UiFrame.Run(Window, list.Draw);
            Assert.All(list.Rows, r => Assert.Equal(alpha, r.Alpha));
            Assert.True(list.Rows[0].Cursor.Y >= list.Origin.Y + FrameHeight);
            UiFrame.Click(Window, list.Origin + new Vector2(20f, FrameHeight / 2), list.Draw);
            UiFrame.Io().AddInputCharactersUTF8("bet");
            UiFrame.Run(Window, list.Draw);
            UiFrame.Run(Window, list.Draw);
            Assert.True(list.Rows[0].Alpha < alpha);
            Assert.Equal(alpha, list.Rows[1].Alpha);
            Assert.True(list.Rows[2].Alpha < alpha);
        }
        finally { EraseFilter(list.Draw, list.Origin + new Vector2(20f, FrameHeight / 2)); UiFrame.Settle(Window, list.Draw); }
    }

    [Fact]
    public void CompactFilterTextSurvivesTheBoxLosingFocus()
    {
        var list = new CompactList { FilterText = s => s };
        var alpha = BaseAlpha;
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, list.Origin + new Vector2(20f, FrameHeight / 2), list.Draw);
        UiFrame.Io().AddInputCharactersUTF8("bet");
        UiFrame.Run(Window, list.Draw);
        UiFrame.ClickAway(Window, list.Draw);
        UiFrame.Run(Window, list.Draw);
        Assert.Equal([true, false, true], list.Rows.Select(r => r.Alpha < alpha));
        EraseFilter(list.Draw, list.Origin + new Vector2(20f, FrameHeight / 2));
        Assert.All(list.Rows, r => Assert.Equal(alpha, r.Alpha));
    }

    [Fact]
    public void CompactWithoutFilterTextDrawsNoFilterBox()
    {
        var list = new CompactList();
        UiFrame.Run(Window, list.Draw);
        Assert.Equal(list.Origin.Y, list.Rows[0].Cursor.Y);
    }

    [Fact]
    public void CompactFilterTextIsKeptPerListScope()
    {
        var a = new CompactList { FilterText = s => s };
        var b = new CompactList { Id = a.Id, FilterText = s => s };
        var alpha = BaseAlpha;
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Click(Window, a.Origin + new Vector2(20f, FrameHeight / 2), Body);
            UiFrame.Io().AddInputCharactersUTF8("bet");
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);
            Assert.Contains(a.Rows, r => r.Alpha < alpha);
            Assert.All(b.Rows, r => Assert.Equal(alpha, r.Alpha));
        }
        finally { EraseFilter(Body, a.Origin + new Vector2(20f, FrameHeight / 2)); UiFrame.Settle(Window, Body); }
    }

    sealed class OrderedList
    {
        public readonly List<string> Items = ["Priority", "Currency", "Maps", "Scarabs", "Everything else"];
        public string Id = UiFrame.NextId("ordered");
        public int Selected;
        public ListOpts Opts = new();
        public int Returns;
        public Vector2 Origin;
        public int Vertices;
        public readonly List<RowInfo> Rows = [];
        public readonly List<int> RowCounts = [];
        public readonly List<int> MenuRows = [];
        public Vector2 MenuPos;
        public readonly List<uint> ExpectedProbes = [];
        public readonly List<List<Vector2>> IndexUvs = [];
        int _mark;

        public void Draw()
        {
            Origin = ImGui.GetCursorScreenPos();
            Rows.Clear();
            ExpectedProbes.Clear();
            IndexUvs.Clear();
            _mark = ImGui.GetWindowDrawList().VtxBuffer.Size;
            for (var i = 0; i < Items.Count; i++)
            {
                ImGui.PushID(Id);
                ImGui.PushID(i);
                ExpectedProbes.Add(ImGui.GetID("probe"));
                ImGui.PopID();
                ImGui.PopID();
            }
            if (ListEditor.ReorderableList(Id, Items, ref Selected, Row, Opts)) Returns++;
            RowCounts.Add(Rows.Count);
            Vertices = ImGui.GetWindowDrawList().VtxBuffer.Size;
        }

        void Row(string text, int index)
        {
            var list = ImGui.GetWindowDrawList();
            IndexUvs.Add(UvsBetween(list, _mark, list.VtxBuffer.Size));
            var cursor = ImGui.GetCursorScreenPos();
            ImGui.TextUnformatted(text);
            Rows.Add(new RowInfo(cursor, ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetStyle().Alpha, ImGui.GetID("probe")));
            _mark = list.VtxBuffer.Size;
        }

        public Action<int> RecordingMenu() => i =>
        {
            MenuRows.Add(i);
            MenuPos = ImGui.GetCursorScreenPos();
            if (ImGui.MenuItem("Delete")) Items.RemoveAt(i);
        };
    }

    static List<Vector2> UvsBetween(ImDrawListPtr list, int from, int to)
    {
        List<Vector2> uvs = [];
        for (var i = from; i < to; i++) uvs.Add(list.VtxBuffer[i].Uv);
        return uvs;
    }

    Vector2 RowAt(OrderedList list, int row) => new(list.Rows[row].Cursor.X + 1f, list.Rows[row].Cursor.Y + FontSize / 2);

    [Fact]
    public void OrderedClickSelectsTheRowWithoutReportingAChange()
    {
        var list = new OrderedList();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, RowAt(list, 2), list.Draw);
        Assert.Equal(2, list.Selected);
        Assert.Equal(0, list.Returns);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
    }

    [Fact]
    public void OrderedClickingAPinnedRowSelectsIt()
    {
        var list = new OrderedList { Selected = 2 };
        list.Opts.PinnedHead = 1;
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, RowAt(list, 0), list.Draw);
        Assert.Equal(0, list.Selected);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void OrderedRowsAreScopedUnderTheListIdAndTheRowIndex()
    {
        var list = new OrderedList { Id = "scoped" };
        UiFrame.Run(Window, list.Draw);
        Assert.Equal(list.ExpectedProbes, list.Rows.Select(r => r.Probe));
        Assert.Equal(5, list.Rows.Select(r => r.Probe).Distinct().Count());
    }

    [Fact]
    public void OrderedRowHeightDecidesWhereARowEnds()
    {
        var tall = new OrderedList();
        var plain = new OrderedList();
        tall.Opts.RowHeight = FrameHeight * 3;
        tall.Opts.ShowIndex = false;
        UiFrame.Run(Window, tall.Draw);
        UiFrame.Click(Window, new Vector2(tall.Rows[0].Cursor.X + 1f, tall.Origin.Y + FrameHeight * 2.5f), tall.Draw);
        Assert.Equal(0, tall.Selected);
        Assert.Equal((tall.Opts.RowHeight - FontSize) / 2, tall.Rows[0].Cursor.Y - tall.Origin.Y);
        tall.Selected = -1;
        UiFrame.Click(Window, new Vector2(tall.Rows[0].Cursor.X + 1f, tall.Origin.Y + FrameHeight * 3 + Spacing.Y + 2f), tall.Draw);
        Assert.Equal(1, tall.Selected);
        UiFrame.Run(Window, plain.Draw);
        plain.Selected = -1;
        UiFrame.Click(Window, new Vector2(plain.Rows[0].Cursor.X + 1f, plain.Origin.Y + FrameHeight * 1.5f), plain.Draw);
        Assert.Equal(1, plain.Selected);
    }

    [Fact]
    public void OrderedIndexColumnPushesTheRowContentRightWhenShown()
    {
        var shown = new OrderedList();
        shown.Opts.PinnedHead = 1;
        var hidden = new OrderedList();
        hidden.Opts.ShowIndex = false;
        UiFrame.Run(Window, shown.Draw);
        UiFrame.Run(Window, hidden.Draw);
        Assert.Equal(UiFrame.TextWidth("01") + Spacing.X, shown.Rows[1].Cursor.X - hidden.Rows[1].Cursor.X);
        Assert.Equal(UiFrame.TextWidth("--") + Spacing.X, shown.Rows[0].Cursor.X - hidden.Rows[0].Cursor.X);
    }

    [Fact]
    public void OrderedIndexColumnNumbersMovableRowsAndDashesPinnedOnes()
    {
        var list = new OrderedList { Selected = -1 };
        list.Opts.PinnedHead = 1;
        list.Opts.PinnedTail = 1;
        UiFrame.Unhover();
        var dashes = UiFrame.GlyphUvs(Window, "--");
        var two = UiFrame.GlyphUvs(Window, "02");
        var three = UiFrame.GlyphUvs(Window, "03");
        var four = UiFrame.GlyphUvs(Window, "04");
        Assert.NotEqual(two, three);
        UiFrame.Run(Window, list.Draw);
        Assert.Equal([dashes, two, three, four, dashes], list.IndexUvs);
    }

    [Fact]
    public void OrderedDraggingARowReordersLiveAndSelectsWhereItLanded()
    {
        var list = new OrderedList();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Press(Window, RowAt(list, 1), list.Draw);
        UiFrame.MoveTo(Window, RowAt(list, 2), list.Draw);
        Assert.Equal(["Priority", "Maps", "Currency", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(2, list.Selected);
        Assert.True(list.Returns >= 1);
        UiFrame.MoveTo(Window, RowAt(list, 3), list.Draw);
        Assert.Equal(["Priority", "Maps", "Scarabs", "Currency", "Everything else"], list.Items);
        Assert.Equal(3, list.Selected);
        var returnsWhileHeld = list.Returns;
        UiFrame.Release(Window, list.Draw);
        Assert.Equal(returnsWhileHeld, list.Returns);
        UiFrame.Run(Window, list.Draw);
        Assert.Equal(["Priority", "Maps", "Scarabs", "Currency", "Everything else"], list.Items);
    }

    [Fact]
    public void OrderedDraggedRowGetsAnOutlineWhileHeld()
    {
        var list = new OrderedList();
        try
        {
            UiFrame.Run(Window, list.Draw);
            UiFrame.Press(Window, RowAt(list, 1), list.Draw);
            UiFrame.MoveTo(Window, RowAt(list, 1) + new Vector2(0, 1f), list.Draw);
            var held = list.Vertices;
            UiFrame.Settle(Window, list.Draw);
            UiFrame.Run(Window, list.Draw);
            UiFrame.MoveTo(Window, RowAt(list, 1) + new Vector2(0, 1f), list.Draw);
            var hovered = list.Vertices;
            Assert.True(held > hovered);
        }
        finally { UiFrame.Settle(Window, list.Draw); }
    }

    [Fact]
    public void OrderedSelectedRowIsHighlighted()
    {
        var picked = new OrderedList { Selected = 0 };
        var none = new OrderedList { Selected = -1 };
        UiFrame.Run(Window, picked.Draw);
        UiFrame.Run(Window, picked.Draw);
        UiFrame.Run(Window, none.Draw);
        Assert.True(picked.Vertices > none.Vertices);
    }

    [Fact]
    public void OrderedADragEndsOnReleaseSoLaterPressesMoveNothing()
    {
        var list = new OrderedList();
        list.Opts.PinnedTail = 1;
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, RowAt(list, 1), RowAt(list, 2), list.Draw);
        Assert.Equal(["Priority", "Maps", "Currency", "Scarabs", "Everything else"], list.Items);
        UiFrame.Run(Window, list.Draw);
        UiFrame.Press(Window, RowAt(list, 4), list.Draw);
        UiFrame.MoveTo(Window, RowAt(list, 0), list.Draw);
        UiFrame.MoveTo(Window, RowAt(list, 0), list.Draw);
        Assert.Equal(["Priority", "Maps", "Currency", "Scarabs", "Everything else"], list.Items);
        UiFrame.Release(Window, list.Draw);
    }

    [Fact]
    public void OrderedDraggingPastEitherEndLandsOnTheEdgeOfTheMovableBand()
    {
        var list = new OrderedList();
        list.Opts.PinnedHead = 1;
        list.Opts.PinnedTail = 1;
        UiFrame.Run(Window, list.Draw);
        var below = new Vector2(list.Rows[4].Cursor.X + 1f, list.Rows[4].Cursor.Y + FrameHeight * 3);
        UiFrame.Drag(Window, RowAt(list, 1), below, list.Draw);
        Assert.Equal(["Priority", "Maps", "Scarabs", "Currency", "Everything else"], list.Items);
        UiFrame.Run(Window, list.Draw);
        var above = new Vector2(list.Rows[0].Cursor.X + 1f, list.Origin.Y - FrameHeight);
        UiFrame.Drag(Window, RowAt(list, 3), above, list.Draw);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
    }

    [Fact]
    public void OrderedTheMouseOnTheLastRowsBottomEdgeIsNotOverIt()
    {
        var list = new OrderedList();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Press(Window, RowAt(list, 1), list.Draw);
        var edge = new Vector2(list.Rows[4].Cursor.X + 1f, list.Rows[4].Cursor.Y + FrameHeight + Spacing.Y - MathF.Floor(Spacing.Y / 2));
        UiFrame.MoveTo(Window, edge, list.Draw);
        UiFrame.MoveTo(Window, edge, list.Draw);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        UiFrame.Release(Window, list.Draw);
    }

    [Fact]
    public void OrderedPressingAMovableRowSelectsItEvenWhenReleasedOffTheRow()
    {
        var list = new OrderedList { Selected = 0 };
        UiFrame.Run(Window, list.Draw);
        UiFrame.Press(Window, RowAt(list, 2), list.Draw);
        var away = RowAt(list, 2) + new Vector2(UiFrame.WindowSize.X, 0);
        UiFrame.MoveTo(Window, away, list.Draw);
        UiFrame.MoveTo(Window, away, list.Draw);
        UiFrame.Release(Window, list.Draw);
        Assert.Equal(2, list.Selected);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
    }

    [Fact]
    public void OrderedPinnedRowsCannotBeDraggedAndOnlySelect()
    {
        var list = new OrderedList();
        list.Opts.PinnedHead = 1;
        list.Opts.PinnedTail = 1;
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, RowAt(list, 0), RowAt(list, 3), list.Draw);
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, RowAt(list, 4), RowAt(list, 1), list.Draw);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void OrderedNotReorderableDoesNotDrag()
    {
        var list = new OrderedList();
        list.Opts.Reorderable = false;
        UiFrame.Run(Window, list.Draw);
        UiFrame.Drag(Window, RowAt(list, 1), RowAt(list, 3), list.Draw);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void OrderedARowDragInOneScopeNeverMovesTheSameIdListInAnother()
    {
        var a = new OrderedList();
        var b = new OrderedList { Id = a.Id };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        UiFrame.Run(Window, Body);
        UiFrame.Drag(Window, RowAt(a, 3), RowAt(a, 1), Body);
        Assert.Equal(["Priority", "Scarabs", "Currency", "Maps", "Everything else"], a.Items);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], b.Items);
        Assert.Equal(0, b.Returns);
    }

    static void PressKey(OrderedList list, ImGuiKey key)
    {
        UiFrame.Io().AddKeyEvent(key, true);
        UiFrame.Run(Window, list.Draw);
        UiFrame.Io().AddKeyEvent(key, false);
        UiFrame.Run(Window, list.Draw);
    }

    [Fact]
    public void OrderedCtrlArrowMovesTheSelectedRowOnlyWhileCtrlIsHeld()
    {
        var list = new OrderedList { Selected = 2 };
        UiFrame.Run(Window, list.Draw);
        PressKey(list, ImGuiKey.DownArrow);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        list.Opts.CtrlHeld = true;
        PressKey(list, ImGuiKey.DownArrow);
        Assert.Equal(["Priority", "Currency", "Scarabs", "Maps", "Everything else"], list.Items);
        Assert.Equal(3, list.Selected);
        Assert.Equal(1, list.Returns);
        PressKey(list, ImGuiKey.UpArrow);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(2, list.Selected);
    }

    [Fact]
    public void OrderedCtrlArrowStopsAtThePinnedTail()
    {
        var list = new OrderedList { Selected = 3 };
        list.Opts.CtrlHeld = true;
        list.Opts.PinnedTail = 1;
        UiFrame.Run(Window, list.Draw);
        PressKey(list, ImGuiKey.DownArrow);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(3, list.Selected);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void OrderedCtrlArrowStopsAtThePinnedHead()
    {
        var list = new OrderedList { Selected = 1 };
        list.Opts.CtrlHeld = true;
        list.Opts.PinnedHead = 1;
        UiFrame.Run(Window, list.Draw);
        PressKey(list, ImGuiKey.UpArrow);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Equal(1, list.Selected);
        Assert.Equal(0, list.Returns);
    }

    [Fact]
    public void OrderedRightClickSelectsTheRowAndRunsItsMenu()
    {
        var list = new OrderedList();
        list.Opts.Menu = list.RecordingMenu();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, RowAt(list, 3), list.Draw, button: 1);
        UiFrame.Run(Window, list.Draw);
        Assert.Equal(3, list.Selected);
        Assert.NotEmpty(list.MenuRows);
        Assert.All(list.MenuRows, row => Assert.Equal(3, row));
    }

    [Fact]
    public void OrderedMenuDeletingTheLastRowClampsTheSelectionAndReportsAChange()
    {
        var list = new OrderedList();
        list.Opts.Menu = list.RecordingMenu();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, RowAt(list, 4), list.Draw, button: 1);
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, list.MenuPos + new Vector2(10f, FontSize / 2), list.Draw);
        Assert.Equal(["Priority", "Currency", "Maps", "Scarabs"], list.Items);
        Assert.Equal(3, list.Selected);
        Assert.Equal(1, list.Returns);
    }

    [Fact]
    public void OrderedMenuDeletingAMiddleRowStopsDrawingTheRestOfThatFrame()
    {
        var list = new OrderedList();
        list.Opts.Menu = list.RecordingMenu();
        UiFrame.Run(Window, list.Draw);
        UiFrame.Click(Window, RowAt(list, 1), list.Draw, button: 1);
        UiFrame.Run(Window, list.Draw);
        list.RowCounts.Clear();
        UiFrame.Click(Window, list.MenuPos + new Vector2(10f, FontSize / 2), list.Draw);
        Assert.Equal(["Priority", "Maps", "Scarabs", "Everything else"], list.Items);
        Assert.Contains(1, list.RowCounts);
    }

    [Fact]
    public void OrderedFilterDimsNonMatchingRowsWithoutChangingOrderOrSelection()
    {
        var list = new OrderedList();
        list.Opts.FilterText = i => list.Items[i];
        var alpha = BaseAlpha;
        try
        {
            UiFrame.Run(Window, list.Draw);
            Assert.All(list.Rows, r => Assert.Equal(alpha, r.Alpha));
            UiFrame.Click(Window, list.Origin + new Vector2(20f, FrameHeight / 2), list.Draw);
            UiFrame.Io().AddInputCharactersUTF8("map");
            UiFrame.Run(Window, list.Draw);
            UiFrame.Run(Window, list.Draw);
            Assert.Equal([true, true, false, true, true], list.Rows.Select(r => r.Alpha < alpha));
            Assert.Equal(["Priority", "Currency", "Maps", "Scarabs", "Everything else"], list.Items);
            Assert.Equal(0, list.Returns);
        }
        finally { EraseFilter(list.Draw, list.Origin + new Vector2(20f, FrameHeight / 2)); UiFrame.Settle(Window, list.Draw); }
    }
}
