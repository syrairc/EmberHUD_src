using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class SpriteTableTests : IDisposable
{
    const string Window = "test";

    static float FrameHeight() => ImGui.GetFrameHeight();

    static Vector2 CheckboxCentre(Vector2 origin) => origin + new Vector2(FrameHeight() / 2);

    static HashSet<uint> TableColours(IReadOnlySet<string> highlight, string[] sprites)
    {
        var filter = "";
        var colours = new HashSet<uint>();
        UiFrame.Run(Window, () =>
        {
            ImDrawListPtr list = default;
            Controls.SpriteTable("sprites", sprites, highlight, ref filter, new SpriteColumn("Probe", _ =>
            {
                list = ImGui.GetWindowDrawList();
                return false;
            }));
            colours = VertexColoursSince(list, 0);
        });
        return colours;
    }

    static HashSet<uint> VertexColoursSince(ImDrawListPtr list, int start)
    {
        var colours = new HashSet<uint>();
        for (var i = start; i < list.VtxBuffer.Size; i++) colours.Add(list.VtxBuffer[i].Col);
        return colours;
    }

    static Vector2 Origin()
    {
        var origin = Vector2.Zero;
        UiFrame.Run(Window, () => origin = ImGui.GetCursorScreenPos());
        return origin;
    }

    static uint IdIn(string scope, string label)
    {
        ImGui.PushID(scope);
        var id = ImGui.GetID(label);
        ImGui.PopID();
        return id;
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void RowsAreDistinctFilteredAndSorted()
    {
        Assert.Equal(new[] { "a", "b", "c" }, Controls.SpriteRows(["b", "a", "b", "c"], ""));
        Assert.Equal(new[] { "ab", "b" }, Controls.SpriteRows(["b", "a", "ab", "b"], "b"));
        Assert.Empty(Controls.SpriteRows(["a"], "zzz"));
    }

    [Fact]
    public void RowsMatchTheFilterIgnoringCase()
    {
        Assert.Equal(["Door", "door"], Controls.SpriteRows(["door", "Door", "chest"], "DOO").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RowsSortByOrdinalValueNotByCulture() => Assert.Equal(["B", "a"], Controls.SpriteRows(["a", "B"], ""));

    [Fact]
    public void ALongTableOnlyDrawsTheRowsOnScreen()
    {
        var rig = Rig(Enumerable.Range(0, 5000).Select(i => "row" + i.ToString("D5")).ToArray());

        rig.Settle();
        rig.Cells.Clear();
        UiFrame.Run(Window, rig.Body);

        var drawn = rig.Cells.Select(cell => cell.Sprite).Distinct().ToList();
        Assert.InRange(drawn.Count, 1, 200);
        Assert.Contains("row00000", drawn);
    }

    [Fact]
    public void AMarkerRowLeavesItsValuesAloneWithoutInput()
    {
        var show = true;
        var colour = Color.FromArgb(255, 255, 0, 0);
        var size = 3f;
        var changed = true;

        UiFrame.Run(Window, () => changed = Controls.MarkerRow("Bosses", ref show, ref colour, ref size));

        Assert.False(changed);
        Assert.True(show);
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), colour);
        Assert.Equal(3f, size);
    }

    [Fact]
    public void ClickingAMarkerRowCheckboxFlipsShowOnly()
    {
        var show = true;
        var colour = Color.FromArgb(255, 255, 0, 0);
        var size = 3f;
        var changed = false;
        var origin = Origin();

        UiFrame.Click(Window, CheckboxCentre(origin), () => changed |= Controls.MarkerRow("Bosses", ref show, ref colour, ref size));

        Assert.True(changed);
        Assert.False(show);
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), colour);
        Assert.Equal(3f, size);
    }

    [Fact]
    public void ClickingTheFarEndOfAMarkerRowSliderSetsTheMaximumSize()
    {
        var show = true;
        var colour = Color.FromArgb(255, 255, 0, 0);
        var size = 3f;
        var changed = false;
        var origin = Origin();
        var slider = origin.X + 2 * (FrameHeight() + ImGui.GetStyle().ItemSpacing.X);

        UiFrame.Click(Window, new Vector2(slider + 119, origin.Y + FrameHeight() / 2), () => changed |= Controls.MarkerRow("Bosses", ref show, ref colour, ref size, 2, 9));

        Assert.True(changed);
        Assert.Equal(9f, size);
        Assert.True(show);
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), colour);
    }

    [Fact]
    public void AMarkerRowSliderRunsBetweenTheGivenBounds()
    {
        var show = true;
        var colour = Color.FromArgb(255, 255, 0, 0);
        var size = 5f;
        var origin = Origin();
        var slider = origin.X + 2 * (FrameHeight() + ImGui.GetStyle().ItemSpacing.X);

        UiFrame.Click(Window, new Vector2(slider + 1, origin.Y + FrameHeight() / 2), () => Controls.MarkerRow("Bosses", ref show, ref colour, ref size, 2, 9));

        Assert.Equal(2f, size);
    }

    [Fact]
    public void PickingInAMarkerRowColourPopupChangesTheColourAndReturnsTrue()
    {
        var show = true;
        var red = Color.FromArgb(255, 255, 0, 0);
        var colour = red;
        var size = 3f;
        var changed = false;
        var origin = Origin();
        var style = ImGui.GetStyle();
        var swatch = origin + new Vector2(FrameHeight() + style.ItemSpacing.X, 0);
        void Body() => changed |= Controls.MarkerRow("Bosses", ref show, ref colour, ref size);

        UiFrame.Click(Window, swatch + new Vector2(FrameHeight() / 2), Body);
        var square = swatch + new Vector2(style.WindowPadding.X, FrameHeight() + style.ItemSpacing.Y + style.WindowPadding.Y) + new Vector2(FrameHeight());
        Assert.False(changed);
        UiFrame.Click(Window, square, Body);

        Assert.True(changed);
        Assert.NotEqual(red, colour);
        Assert.True(show);
        Assert.Equal(3f, size);
    }

    [Fact]
    public void AMarkerRowDrawsItsColourAndEndsWithItsLabel()
    {
        var show = true;
        var colour = Color.FromArgb(255, 12, 200, 34);
        var size = 3f;
        HashSet<uint> drawn = [];
        Vector2 labelSize = default, expectedLabel = default;

        UiFrame.Run(Window, () =>
        {
            drawn = UiFrame.Drawn(() => Controls.MarkerRow("Bosses", ref show, ref colour, ref size)).Select(v => v.Col).ToHashSet();
            labelSize = ImGui.GetItemRectSize();
            expectedLabel = ImGui.CalcTextSize("Bosses");
        });

        Assert.Contains(EColor.U32(colour), drawn);
        Assert.Equal(expectedLabel, labelSize);
    }

    [Fact]
    public void AMarkerRowScopesItsWidgetsByIdOrElseByLabel()
    {
        var show = true;
        var colour = Color.FromArgb(255, 255, 0, 0);
        var size = 3f;
        var origin = Origin();
        HashSet<uint> active = [];
        uint byLabel = 0, bySecond = 0;

        void Body()
        {
            byLabel = IdIn("Bosses", "##show");
            bySecond = IdIn("second", "##show");
            Controls.MarkerRow("Bosses", ref show, ref colour, ref size);
            Controls.MarkerRow("Bosses", ref show, ref colour, ref size, id: "second");
            active.Add(ImGuiP.GetActiveID());
        }

        UiFrame.Click(Window, CheckboxCentre(origin), Body);
        Assert.Contains(byLabel, active);

        active.Clear();
        UiFrame.Click(Window, CheckboxCentre(origin) + new Vector2(0, FrameHeight() + ImGui.GetStyle().ItemSpacing.Y), Body);
        Assert.Contains(bySecond, active);
        Assert.DoesNotContain(byLabel, active);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void ASetCheckboxIsTickedWhenMembershipDiffersFromInverted(bool member, bool inverted, bool ticked)
    {
        HashSet<string> set = member ? ["door"] : [];
        var colours = new HashSet<uint>();

        UiFrame.Run(Window, () =>
        {
            colours = UiFrame.Drawn(() => Controls.SetCheckbox("##show", set, "door", inverted)).Select(v => v.Col).ToHashSet();
        });

        Assert.Equal(ticked, colours.Contains(ImGui.GetColorU32(ImGuiCol.CheckMark)));
    }

    [Fact]
    public void ClickingAnInvertedSetCheckboxAddsTheItem()
    {
        List<string> hidden = [];
        var origin = Origin();
        var changed = false;

        UiFrame.Click(Window, CheckboxCentre(origin), () => changed |= Controls.SetCheckbox("##show", hidden, "door", inverted: true));

        Assert.True(changed);
        Assert.Equal("door", Assert.Single(hidden));
    }

    [Fact]
    public void ClickingAnInvertedSetCheckboxThatIsAlreadyInTheSetRemovesTheItem()
    {
        List<string> hidden = ["door", "chest"];
        var origin = Origin();
        var changed = false;

        UiFrame.Click(Window, CheckboxCentre(origin), () => changed |= Controls.SetCheckbox("##show", hidden, "door", inverted: true));

        Assert.True(changed);
        Assert.Equal("chest", Assert.Single(hidden));
    }

    [Fact]
    public void ClickingAPlainSetCheckboxAddsAnAbsentItem()
    {
        HashSet<string> on = [];
        var origin = Origin();
        var changed = false;

        UiFrame.Click(Window, CheckboxCentre(origin), () => changed |= Controls.SetCheckbox("##on", on, "door"));

        Assert.True(changed);
        Assert.Equal("door", Assert.Single(on));
    }

    [Fact]
    public void ClickingAPlainSetCheckboxRemovesAPresentItem()
    {
        HashSet<string> on = ["door", "chest"];
        var origin = Origin();

        UiFrame.Click(Window, CheckboxCentre(origin), () => Controls.SetCheckbox("##on", on, "door"));

        Assert.Equal("chest", Assert.Single(on));
    }

    [Fact]
    public void AnUntouchedSetCheckboxReturnsFalseAndLeavesTheSet()
    {
        HashSet<string> on = ["door"];
        var changed = true;

        UiFrame.Run(Window, () => changed = Controls.SetCheckbox("##on", on, "door"));

        Assert.False(changed);
        Assert.Equal("door", Assert.Single(on));
    }

    sealed class TableRig(IEnumerable<string> sprites, IReadOnlySet<string> highlight, string filter, HashSet<string> on)
    {
        public string Filter = filter;
        public readonly List<(string Sprite, string Header, int Column, uint Id, Vector2 Centre)> Cells = [];
        public bool Changed;
        public string Scope = "sprites";

        public void Body()
        {
            Changed |= Controls.SpriteTable(Scope, sprites, highlight, ref Filter,
                new SpriteColumn("On", sprite => Cell(sprite, Controls.SetCheckbox("##on", on, sprite))),
                new SpriteColumn("Off", sprite => Cell(sprite, Controls.SetCheckbox("##off", on, sprite, inverted: true))));
        }

        bool Cell(string sprite, bool changed)
        {
            var column = ImGui.TableGetColumnIndex();
            Cells.Add((sprite, ImGui.TableGetColumnNameS(column), column, ImGui.GetItemID(), (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2));
            return changed;
        }

        public void Settle()
        {
            for (var i = 0; i < 3; i++) UiFrame.Run(Window, Body);
        }

        public Vector2 CentreOf(string sprite, string header) => Cells.Last(cell => cell.Sprite == sprite && cell.Header == header).Centre;
    }

    static TableRig Rig(string[] sprites, string filter = "", HashSet<string>? on = null, IReadOnlySet<string>? highlight = null) =>
        new(sprites, highlight ?? new HashSet<string>(), filter, on ?? []);

    [Fact]
    public void ATableWithCheckboxColumnsDrawsOneBalancedFrameAndChangesNothingUntouched()
    {
        var filter = "";
        List<string> on = ["b"];
        var changed = true;

        UiFrame.Run(Window, () =>
        {
            changed = Controls.SpriteTable("sprites", ["b", "a", "b", "c"], new HashSet<string> { "a" }, ref filter,
                new SpriteColumn("On", sprite => Controls.SetCheckbox("##on", on, sprite)),
                new SpriteColumn("Off", sprite => Controls.SetCheckbox("##off", on, sprite, inverted: true)));
        });

        Assert.False(changed);
        Assert.Equal("b", Assert.Single(on));
    }

    [Fact]
    public void AFilterThatMatchesNothingCallsNoCells()
    {
        var rig = Rig(["a", "b"], "zzz");

        UiFrame.Run(Window, rig.Body);

        Assert.Empty(rig.Cells);
        Assert.False(rig.Changed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("zzz")]
    public void TheTableIsClosedWhenSpriteTableReturns(string filter)
    {
        var rig = Rig(["a", "b"], filter);

        UiFrame.Run(Window, () =>
        {
            rig.Body();
            Assert.True(ImGuiP.GetCurrentTable().IsNull);
        });

        Assert.Equal(filter == "" ? 4 : 0, rig.Cells.Count);
    }

    [Fact]
    public void ATableCallsEachColumnOncePerDistinctSortedSpriteUnderItsOwnHeader()
    {
        var rig = Rig(["b", "a", "b", "c"]);

        UiFrame.Run(Window, rig.Body);

        Assert.Equal(["a", "a", "b", "b", "c", "c"], rig.Cells.Select(cell => cell.Sprite));
        Assert.Equal(["On", "Off", "On", "Off", "On", "Off"], rig.Cells.Select(cell => cell.Header));
        Assert.Equal([1, 2, 1, 2, 1, 2], rig.Cells.Select(cell => cell.Column));
    }

    [Fact]
    public void ATableGivesEveryCellItsOwnId()
    {
        var rig = Rig(["a", "b", "c"]);

        UiFrame.Run(Window, rig.Body);

        Assert.Equal(rig.Cells.Count, rig.Cells.Select(cell => cell.Id).Distinct().Count());
    }

    [Fact]
    public void TwoTablesWithDifferentIdsGiveTheSameCellDifferentIds()
    {
        var first = Rig(["a"]);
        var second = Rig(["a"]);
        second.Scope = "other";

        UiFrame.Run(Window, first.Body);
        UiFrame.Run(Window, second.Body);

        Assert.NotEqual(first.Cells[0].Id, second.Cells[0].Id);
    }

    [Fact]
    public void ATableOnlyCallsCellsForRowsThatMatchTheFilter()
    {
        var rig = Rig(["alpha", "beta", "gamma"], "ET");

        UiFrame.Run(Window, rig.Body);

        Assert.Equal(["beta", "beta"], rig.Cells.Select(cell => cell.Sprite));
    }

    [Fact]
    public void ClickingACellCheckboxChangesOnlyThatRowAndReturnsTrue()
    {
        HashSet<string> on = [];
        var rig = Rig(["a", "b", "c"], on: on);
        rig.Settle();

        UiFrame.Click(Window, rig.CentreOf("b", "On"), rig.Body);

        Assert.True(rig.Changed);
        Assert.Equal("b", Assert.Single(on));
    }

    [Fact]
    public void TypingInTheFilterBoxWritesTheFilterAndNarrowsTheRows()
    {
        var rig = Rig(["alpha", "beta", "gamma"]);
        var origin = Origin();
        var io = UiFrame.Io();
        UiFrame.Click(Window, origin + new Vector2(FrameHeight(), FrameHeight() / 2), rig.Body);
        io.AddInputCharactersUTF8("et");
        UiFrame.Run(Window, rig.Body);
        rig.Cells.Clear();
        UiFrame.Run(Window, rig.Body);

        Assert.Equal("et", rig.Filter);
        Assert.False(rig.Changed);
        Assert.Equal(["beta", "beta"], rig.Cells.Select(cell => cell.Sprite));
    }

    [Fact]
    public void AHighlightedSpriteNameIsDrawnInTheAccentColour()
    {
        uint accent = 0;
        UiFrame.Run(Window, () => accent = ImGui.GetColorU32(ImGuiCol.CheckMark));

        Assert.Contains(accent, TableColours(new HashSet<string> { "a" }, ["a"]));
        Assert.DoesNotContain(accent, TableColours(new HashSet<string>(), ["a"]));
        Assert.DoesNotContain(accent, TableColours(new HashSet<string> { "other" }, ["a"]));
    }
}
