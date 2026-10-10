using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class ComboTests
{
    const string Window = "combo";

    enum Speed { Slow, Medium, Fast }

    static float Metric(Func<float> read)
    {
        var value = 0f;
        UiFrame.Run(Window, () => value = read());
        return value;
    }

    static float FrameHeight => Metric(() => ImGui.GetFrameHeight());

    static float FontSize => Metric(() => ImGui.GetFontSize());

    static float SpacingY => Metric(() => ImGui.GetStyle().ItemSpacing.Y);

    static float PaddingY => Metric(() => ImGui.GetStyle().WindowPadding.Y);

    static void Cleanup(Rig rig)
    {
        UiFrame.ClickAway(Window, rig.Body);
        UiFrame.Unhover();
    }

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

        public bool PopupOpen()
        {
            var open = false;
            UiFrame.Run(Window, () =>
            {
                Body();
                open = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
            });
            return open;
        }

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

    [Fact]
    public void EnumPickWritesValueAndReportsChange()
    {
        var speed = Speed.Medium;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Enum(ref speed);
        try
        {
            rig.Open(120f);
            Assert.True(rig.PopupOpen());
            Assert.False(rig.Changed);
            rig.PickRow(2);
            Assert.True(rig.Changed);
            Assert.Equal(Speed.Fast, speed);
            Assert.False(rig.PopupOpen());
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void EnumUndefinedValueShowsFirstNameSoPickingItIsNoChange()
    {
        var speed = (Speed)99;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Enum(ref speed);
        try
        {
            rig.Open(120f);
            rig.PickRow(0);
            Assert.False(rig.Changed);
            Assert.Equal((Speed)99, speed);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void EnumIdDefaultsToTypeNameAndHonoursOverride()
    {
        var speed = Speed.Slow;
        var rig = new Rig();
        try
        {
            rig.Draw = () => Combo.Enum(ref speed);
            rig.Idle();
            var defaultId = rig.ItemId;
            rig.Draw = () => Combo.Enum(ref speed, "other");
            rig.Idle();
            var otherId = rig.ItemId;
            var expected = (Default: 0u, Other: 0u);
            UiFrame.Run(Window, () => expected = (ImGui.GetID("##Speed"), ImGui.GetID("##other")));
            Assert.Equal(expected.Default, defaultId);
            Assert.Equal(expected.Other, otherId);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void EnumAndOptionPinTheirWidth()
    {
        var speed = Speed.Slow;
        var index = 0;
        var rig = new Rig();
        try
        {
            rig.Draw = () => Combo.Enum(ref speed, width: 173f);
            rig.Idle();
            Assert.Equal(173f, rig.ItemSize.X);
            rig.Draw = () => Combo.Option(ref index, ["A", "B"], width: 211f);
            rig.Idle();
            Assert.Equal(211f, rig.ItemSize.X);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void OptionPickWritesIndexAndReportsChange()
    {
        var index = 1;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Option(ref index, ["A", "B", "C"]);
        try
        {
            rig.Open(120f);
            Assert.False(rig.Changed);
            rig.PickRow(2);
            Assert.True(rig.Changed);
            Assert.Equal(2, index);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void OptionPickingTheCurrentIndexIsNoChange()
    {
        var index = 1;
        var rig = new Rig();
        rig.Draw = () => rig.Changed |= Combo.Option(ref index, ["A", "B", "C"]);
        try
        {
            rig.Open(120f);
            rig.PickRow(1);
            Assert.False(rig.Changed);
            Assert.Equal(1, index);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void OptionIdDefaultsToFirstLabelOrOptWhenEmpty()
    {
        var index = 0;
        var rig = new Rig();
        var ids = new List<uint>();
        try
        {
            rig.Draw = () => Combo.Option(ref index, ["Alpha", "B"]);
            rig.Idle();
            ids.Add(rig.ItemId);
            rig.Draw = () => Combo.Option(ref index, []);
            rig.Idle();
            ids.Add(rig.ItemId);
            rig.Draw = () => Combo.Option(ref index, ["Alpha", "B"], "custom");
            rig.Idle();
            ids.Add(rig.ItemId);
            var expected = new List<uint>();
            UiFrame.Run(Window, () => expected = [ImGui.GetID("##Alpha"), ImGui.GetID("##opt"), ImGui.GetID("##custom")]);
            Assert.Equal(expected, ids);
        }
        finally { Cleanup(rig); }
    }

    static void Cleanup(SearchRig rig) => Cleanup(rig.Rig);

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

        public bool PopupOpen() => Rig.PopupOpen();

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
        try
        {
            rig.Open();
            Assert.True(rig.PopupOpen());
            Assert.Equal(0, rig.Picks);
            rig.Filter = "ban";
            rig.Rig.Idle();
            rig.Pick("banana");
            Assert.Equal(1, rig.Picks);
            Assert.Equal("banana", rig.Picked);
            Assert.Equal("", rig.FilterAtPick);
            Assert.Equal("", rig.Filter);
            Assert.False(rig.PopupOpen());
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboPickedKeyIsNotTheLabel()
    {
        var rig = new SearchRig { Candidates = [("k-1", "Label One", "one")] };
        try
        {
            rig.Open();
            rig.Pick("k-1");
            Assert.Equal("k-1", rig.Picked);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboFilterMatchesTheSearchKeyNotTheLabel()
    {
        var rig = new SearchRig();
        try
        {
            rig.Open();
            rig.Filter = "yellow";
            rig.Rig.Idle();
            Assert.Equal(rig.RowAt["apple"], rig.RowAt["banana"]);
            rig.Pick("banana");
            Assert.Equal("banana", rig.Picked);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboLabelAloneDoesNotMatchWhenSearchKeyDiffers()
    {
        var rig = new SearchRig { Candidates = [("apple", "Apple", "zzz"), ("banana", "Banana", "banana")] };
        try
        {
            rig.Open();
            rig.Filter = "Apple";
            rig.Rig.Idle();
            rig.Pick("apple");
            Assert.Equal(0, rig.Picks);
            Assert.Null(rig.Picked);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboFilterHidesNonMatchingRows()
    {
        var rig = new SearchRig();
        try
        {
            rig.Open();
            rig.Filter = "red";
            rig.Rig.Idle();
            Assert.NotEqual(rig.RowAt["apple"], rig.RowAt["banana"]);
            Assert.Equal(rig.RowAt["banana"], rig.RowAt["cherry"]);
        }
        finally { Cleanup(rig); }
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
        try
        {
            rig.Open(250f);
            rig.Idle();
            filter = "nan";
            rig.Idle();
            Assert.Equal(rows["a"], rows["b"]);
            UiFrame.Click(Window, rows["b"] + new Vector2(10f, rig.FontSize / 2), rig.Body);
            Assert.Equal("b", picked);
            Assert.Equal("", filter);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboSameLabelDifferentKeysStayIndependentRows()
    {
        var rig = new SearchRig { Candidates = [("k1", "Same", "same"), ("k2", "Same", "same")] };
        try
        {
            rig.Open();
            rig.Pick("k2");
            Assert.Equal("k2", rig.Picked);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboClosedPopupDropsStaleFilter()
    {
        var rig = new SearchRig { Filter = "stale" };
        try
        {
            rig.Rig.Idle();
            Assert.Equal("", rig.Filter);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboOpenPopupKeepsTypedFilter()
    {
        var rig = new SearchRig();
        try
        {
            rig.Open();
            rig.Filter = "kept";
            rig.Rig.Idle();
            Assert.Equal("kept", rig.Filter);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboTypingInTheFilterBoxNarrowsRows()
    {
        var rig = new SearchRig();
        try
        {
            rig.Open();
            rig.TypeFilter("cher");
            Assert.Equal("cher", rig.Filter);
            Assert.Equal(rig.RowAt["apple"], rig.RowAt["cherry"]);
            rig.Pick("cherry");
            Assert.Equal("cherry", rig.Picked);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboFilterLengthIsCapped()
    {
        var rig = new SearchRig();
        try
        {
            rig.Open();
            rig.TypeFilter(new string('q', 300));
            Assert.InRange(rig.Filter.Length, 100, 128);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboPinsComboAndPopupWidth()
    {
        var rig = new SearchRig { Width = 190f };
        try
        {
            rig.Rig.Idle();
            Assert.Equal(190f, rig.Rig.ItemSize.X);
            rig.Open();
            Assert.Equal(190f, rig.PopupWidth);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboPopupNeverGrowsPastTheComboWidthForLongLabels()
    {
        var rig = new SearchRig { Width = 140f, Candidates = [("long", new string('W', 120), "w")] };
        try
        {
            rig.Open();
            rig.Rig.Idle();
            Assert.Equal(140f, rig.PopupWidth);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboPopupHeightIsCappedAt400()
    {
        var rig = new SearchRig { Candidates = [.. Enumerable.Range(0, 100).Select(i => ($"k{i}", $"Item {i}", $"item {i}"))] };
        try
        {
            rig.Open();
            rig.Rig.Idle();
            Assert.Equal(400f, rig.PopupHeight);
        }
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboNegativeWidthFillsTheAvailableWidth()
    {
        var rig = new Rig();
        var avail = 0f;
        try
        {
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
        finally { Cleanup(rig); }
    }

    [Fact]
    public void SearchComboIdScopesTheComboItem()
    {
        var rig = new SearchRig();
        try
        {
            rig.Rig.Idle();
            var expected = 0u;
            UiFrame.Run(Window, () => expected = ImGui.GetID("##fruit"));
            Assert.Equal(expected, rig.Rig.ItemId);
        }
        finally { Cleanup(rig); }
    }
}
