using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class ComboTests : IDisposable
{
    const string Window = "combo";

    enum Speed { Slow, Medium, Fast }

    static float FrameHeight => UiFrame.Style.FrameHeight;

    static float FontSize => UiFrame.Style.FontSize;

    static float SpacingY => UiFrame.Style.Spacing.Y;

    static float PaddingY => UiFrame.Style.WindowPadding.Y;

    sealed class Rig
    {
        public Vector2 Origin;
        public bool Changed;
        public uint ItemId;
        public Vector2 ItemSize;
        public Action Draw = () => { };
        public readonly float FrameHeight = ComboTests.FrameHeight;
        public readonly float FontSize = ComboTests.FontSize;
        public readonly float SpacingY = ComboTests.SpacingY;
        public readonly float PaddingY = ComboTests.PaddingY;

        public void Body()
        {
            Origin = ImGui.GetCursorScreenPos();
            Draw();
            ItemId = ImGui.GetItemID();
            ItemSize = ImGui.GetItemRectSize();
        }

        public void Idle() => UiFrame.Run(Window, Body);

        public void Open(float width)
        {
            Idle();
            UiFrame.Click(Window, Origin + new Vector2(width / 2, FrameHeight / 2), Body);
        }

        public void PickRow(int row)
        {
            var y = Origin.Y + FrameHeight + PaddingY + row * (FontSize + SpacingY) + FontSize / 2;
            UiFrame.Click(Window, new Vector2(Origin.X + 10f, y), Body);
        }
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void EnumPickWritesValueAndReportsChange()
    {
        var speed = Speed.Medium;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Enum(ref speed, "speed");
        rig.Open(120f);
        Assert.True(UiFrame.PopupOpen(Window, rig.Body));
        Assert.False(rig.Changed);
        rig.PickRow(2);
        Assert.True(rig.Changed);
        Assert.Equal(Speed.Fast, speed);
        Assert.False(UiFrame.PopupOpen(Window, rig.Body));
    }

    [Fact]
    public void EnumUndefinedValueShowsFirstNameSoPickingItIsNoChange()
    {
        var speed = (Speed)99;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Enum(ref speed, "speed");
        rig.Open(120f);
        rig.PickRow(0);
        Assert.False(rig.Changed);
        Assert.Equal((Speed)99, speed);
    }

    [Fact]
    public void EnumIdIsTheWidgetId()
    {
        var speed = Speed.Slow;
        var rig = new Rig();
        rig.Draw = () => Combo.Enum(ref speed, "pace");
        rig.Idle();
        var expected = 0u;
        UiFrame.Run(Window, () => expected = ImGui.GetID("##pace"));
        Assert.Equal(expected, rig.ItemId);
    }

    [Fact]
    public void OptionPickWritesIndexAndReportsChange()
    {
        var index = 1;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Option(ref index, ["A", "B", "C"], "index");
        rig.Open(120f);
        Assert.False(rig.Changed);
        rig.PickRow(2);
        Assert.True(rig.Changed);
        Assert.Equal(2, index);
    }

    [Fact]
    public void OptionPickingTheCurrentIndexIsNoChange()
    {
        var index = 1;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Option(ref index, ["A", "B", "C"], "index");
        rig.Open(120f);
        rig.PickRow(1);
        Assert.False(rig.Changed);
        Assert.Equal(1, index);
    }

    sealed class SearchRig
    {
        public readonly Rig Rig = new();
        public readonly Dictionary<string, Vector2> RowAt = [];
        public float PopupWidth;
        public float PopupHeight;
        public string? FilterAtPick;
        public string Filter = "";
        public string? Picked;
        public int Picks;
        public float Width = 250f;
        public (string key, string label, string search)[] Candidates =
        [
            ("apple", "Apple", "apple red"),
            ("banana", "Banana", "banana yellow"),
            ("cherry", "Cherry", "cherry red"),
        ];

        public SearchRig()
        {
            Rig.Draw = () =>
            {
                if (Combo.SearchCombo("fruit", "(none)", Walk(), ref Filter, out var picked, Width))
                {
                    Picked = picked;
                    FilterAtPick = Filter;
                    Picks++;
                }
            };
        }

        IEnumerable<(string key, string label, string search)> Walk()
        {
            PopupWidth = ImGui.GetWindowWidth();
            PopupHeight = ImGui.GetWindowHeight();
            RowAt.Clear();
            foreach (var c in Candidates)
            {
                RowAt[c.key] = ImGui.GetCursorScreenPos();
                yield return c;
            }
        }

        public void Open()
        {
            Rig.Open(Width);
            Rig.Idle();
        }

        public void Pick(string key) => UiFrame.Click(Window, RowAt[key] + new Vector2(10f, Rig.FontSize / 2), Rig.Body);

        public void TypeFilter(string text)
        {
            var first = RowAt.Values.OrderBy(p => p.Y).First();
            UiFrame.Click(Window, first - new Vector2(-10f, Rig.SpacingY + Rig.FrameHeight / 2), Rig.Body);
            UiFrame.Io().AddInputCharactersUTF8(text);
            Rig.Idle();
            Rig.Idle();
        }
    }

    [Fact]
    public void SearchComboPickReturnsKeyClearsFilterAndClosesPopup()
    {
        var rig = new SearchRig();
        rig.Open();
        Assert.True(UiFrame.PopupOpen(Window, rig.Rig.Body));
        Assert.Equal(0, rig.Picks);
        rig.Filter = "ban";
        rig.Rig.Idle();
        rig.Pick("banana");
        Assert.Equal(1, rig.Picks);
        Assert.Equal("banana", rig.Picked);
        Assert.Equal("", rig.FilterAtPick);
        Assert.Equal("", rig.Filter);
        Assert.False(UiFrame.PopupOpen(Window, rig.Rig.Body));
    }

    [Fact]
    public void SearchComboPickedKeyIsNotTheLabel()
    {
        var rig = new SearchRig { Candidates = [("k-1", "Label One", "one")] };
        rig.Open();
        rig.Pick("k-1");
        Assert.Equal("k-1", rig.Picked);
    }

    [Fact]
    public void SearchComboFilterMatchesTheSearchKeyNotTheLabel()
    {
        var rig = new SearchRig();
        rig.Open();
        rig.Filter = "yellow";
        rig.Rig.Idle();
        Assert.Equal(rig.RowAt["apple"], rig.RowAt["banana"]);
        rig.Pick("banana");
        Assert.Equal("banana", rig.Picked);
    }

    [Fact]
    public void SearchComboLabelAloneDoesNotMatchWhenSearchKeyDiffers()
    {
        var rig = new SearchRig { Candidates = [("apple", "Apple", "zzz"), ("banana", "Banana", "banana")] };
        rig.Open();
        rig.Filter = "Apple";
        rig.Rig.Idle();
        rig.Pick("apple");
        Assert.Equal(0, rig.Picks);
        Assert.Null(rig.Picked);
    }

    [Fact]
    public void SearchComboFilterHidesNonMatchingRows()
    {
        var rig = new SearchRig();
        rig.Open();
        rig.Filter = "red";
        rig.Rig.Idle();
        Assert.NotEqual(rig.RowAt["apple"], rig.RowAt["banana"]);
        Assert.Equal(rig.RowAt["banana"], rig.RowAt["cherry"]);
    }

    [Fact]
    public void SearchComboPairOverloadFiltersOnTheLabel()
    {
        var filter = "";
        string? picked = null;
        var rig = new Rig();
        var rows = new Dictionary<string, Vector2>();
        IEnumerable<(string key, string label)> Walk()
        {
            rows.Clear();
            foreach (var c in new[] { ("a", "Apple"), ("b", "Banana") })
            {
                rows[c.Item1] = ImGui.GetCursorScreenPos();
                yield return c;
            }
        }
        rig.Draw = () =>
        {
            if (Combo.SearchCombo("pairs", "(none)", Walk(), ref filter, out var p)) picked = p;
        };
        rig.Open(250f);
        rig.Idle();
        filter = "nan";
        rig.Idle();
        Assert.Equal(rows["a"], rows["b"]);
        UiFrame.Click(Window, rows["b"] + new Vector2(10f, rig.FontSize / 2), rig.Body);
        Assert.Equal("b", picked);
        Assert.Equal("", filter);
    }

    [Fact]
    public void SearchComboSameLabelDifferentKeysStayIndependentRows()
    {
        var rig = new SearchRig { Candidates = [("k1", "Same", "same"), ("k2", "Same", "same")] };
        rig.Open();
        rig.Pick("k2");
        Assert.Equal("k2", rig.Picked);
    }

    [Fact]
    public void SearchComboClosedPopupDropsStaleFilter()
    {
        var rig = new SearchRig { Filter = "stale" };
        rig.Rig.Idle();
        Assert.Equal("", rig.Filter);
    }

    [Fact]
    public void SearchComboOpenPopupKeepsTypedFilter()
    {
        var rig = new SearchRig();
        rig.Open();
        rig.Filter = "kept";
        rig.Rig.Idle();
        Assert.Equal("kept", rig.Filter);
    }

    [Fact]
    public void SearchComboTypingInTheFilterBoxNarrowsRows()
    {
        var rig = new SearchRig();
        rig.Open();
        rig.TypeFilter("cher");
        Assert.Equal("cher", rig.Filter);
        Assert.Equal(rig.RowAt["apple"], rig.RowAt["cherry"]);
        rig.Pick("cherry");
        Assert.Equal("cherry", rig.Picked);
    }

    [Fact]
    public void SearchComboFilterLengthIsCapped()
    {
        var rig = new SearchRig();
        rig.Open();
        rig.TypeFilter(new string('q', 300));
        Assert.InRange(rig.Filter.Length, 100, 128);
    }

    [Fact]
    public void SearchComboPinsComboAndPopupWidth()
    {
        var rig = new SearchRig { Width = 190f };
        rig.Rig.Idle();
        Assert.Equal(190f, rig.Rig.ItemSize.X);
        rig.Open();
        Assert.Equal(190f, rig.PopupWidth);
    }

    [Fact]
    public void SearchComboPopupNeverGrowsPastTheComboWidthForLongLabels()
    {
        var rig = new SearchRig { Width = 140f, Candidates = [("long", new string('W', 120), "w")] };
        rig.Open();
        rig.Rig.Idle();
        Assert.Equal(140f, rig.PopupWidth);
    }

    [Fact]
    public void SearchComboPopupHeightIsCappedAt400()
    {
        var rig = new SearchRig { Candidates = [.. Enumerable.Range(0, 100).Select(i => ($"k{i}", $"Item {i}", $"item {i}"))] };
        rig.Open();
        rig.Rig.Idle();
        Assert.Equal(400f, rig.PopupHeight);
    }

    [Fact]
    public void SearchComboNegativeWidthFillsTheAvailableWidth()
    {
        var rig = new Rig();
        var avail = 0f;
        rig.Draw = () =>
        {
            avail = ImGui.GetContentRegionAvail().X;
            var filter = "";
            Combo.SearchCombo("fill", "(none)", [("a", "A")], ref filter, out _, -1f);
        };
        rig.Idle();
        Assert.True(avail > 0f);
        Assert.Equal(avail, rig.ItemSize.X);
    }

    [Fact]
    public void SearchComboIdScopesTheComboItem()
    {
        var rig = new SearchRig();
        rig.Rig.Idle();
        var expected = 0u;
        UiFrame.Run(Window, () => expected = ImGui.GetID("##fruit"));
        Assert.Equal(expected, rig.Rig.ItemId);
    }
}
