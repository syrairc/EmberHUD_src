using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class ControlsGridsTests
{
    static Vector2 Centre() => (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;

    static Vector2 CentreOf(Action body)
    {
        var at = Vector2.Zero;
        UiFrame.Run("test", () =>
        {
            body();
            at = Centre();
        });
        return at;
    }

    static int TooltipWindowsWhenHovering(Action body, Vector2 at)
    {
        var before = 0;
        var during = 0;
        UiFrame.Run("test", () =>
        {
            body();
            before = UiFrame.Io().MetricsRenderWindows;
        });
        UiFrame.Io().AddMousePosEvent(at.X, at.Y);
        try
        {
            UiFrame.Run("test", body);
            UiFrame.Run("test", body);
            UiFrame.Run("test", () =>
            {
                body();
                during = UiFrame.Io().MetricsRenderWindows;
            });
        }
        finally
        {
            UiFrame.Unhover();
        }
        return during - before;
    }

    static HashSet<uint> SwatchColours(Action body)
    {
        var colours = new HashSet<uint>();
        UiFrame.Run("test", () =>
        {
            var start = ImGui.GetWindowDrawList().VtxBuffer.Size;
            body();
            var verts = ImGui.GetWindowDrawList().VtxBuffer;
            for (var i = start; i < verts.Size; i++) colours.Add(verts[i].Col);
        });
        return colours;
    }

    static uint Packed(Color c) => ImGui.ColorConvertFloat4ToU32(EColor.ToVector4(c));

    [Theory]
    [InlineData(300f, 100f, 0, 3)]
    [InlineData(290f, 100f, 0, 2)]
    [InlineData(40f, 100f, 0, 1)]
    [InlineData(0f, 0f, 0, 1)]
    [InlineData(100f, 0f, 0, 1)]
    [InlineData(1000f, 100f, 4, 4)]
    public void FitColumnsFloorsNeverHitsZeroAndRespectsMax(float avail, float stride, int max, int expected) =>
        Assert.Equal(expected, Controls.FitColumns(avail, stride, max));

    [Fact]
    public void GridsDrawOneBalancedFrameAndChangeNothingUntouched()
    {
        bool[] values = [false, true, false, false];
        Color[] colours = [Color.FromArgb(255, 200, 200, 200), Color.FromArgb(255, 110, 160, 255)];
        var flag = false;
        var colour = Color.FromArgb(255, 1, 2, 3);
        var changed = false;

        UiFrame.Run("test", () =>
        {
            changed |= Controls.ToggleGrid("options", ["A", "B", "A longer C", "D"], values, 2, ["tip for A"]);
            changed |= Controls.ToggleGrid("items", [new Controls.ToggleItem("Flag", () => flag, v => flag = v, "a 100% tip")]);
            changed |= Controls.ColorGrid("colours", ["Normal", "Magic"], colours);
            changed |= Controls.ColorGrid("colouritems", [new Controls.ColorItem("One", () => colour, c => colour = c)]);
            changed |= Controls.Grid("custom", 5, Controls.LabelStride("cell 0"), i =>
            {
                ImGui.TextUnformatted("cell " + i);
                return false;
            });
        });

        Assert.False(changed);
        Assert.True(values[1]);
    }

    [Fact]
    public void LabelStrideIsWidestLabelPlusSquareAndTwoGaps()
    {
        float none = 0, one = 0, two = 0, wide = 0, gap = 0, frame = 0;
        UiFrame.Run("test", () =>
        {
            none = Controls.LabelStride();
            one = Controls.LabelStride("ab");
            two = Controls.LabelStride("ab", "abcdefghij", "abc");
            wide = ImGui.CalcTextSize("abcdefghij").X;
            gap = ImGui.GetStyle().ItemSpacing.X;
            frame = ImGui.GetFrameHeight();
        });

        Assert.Equal(frame + gap * 2, none, 0.01f);
        Assert.Equal(wide + frame + gap * 2, two, 0.01f);
        Assert.True(none < one);
        Assert.True(one < two);
    }

    [Fact]
    public void GridPlacesCellsOnStrideAndWrapsRows()
    {
        List<float> xs = [], ys = [];
        float x0 = 0, stride = 0, rowHeight = 0;
        var calls = new List<int>();

        UiFrame.Run("test", () =>
        {
            ImGui.Indent(20);
            x0 = ImGui.GetCursorPosX();
            stride = ImGui.GetContentRegionAvail().X / 3;
            rowHeight = ImGui.GetFrameHeightWithSpacing();
            Controls.Grid("g", 5, stride, i =>
            {
                calls.Add(i);
                xs.Add(ImGui.GetCursorPosX());
                ys.Add(ImGui.GetCursorPosY());
                ImGui.Button("b" + i);
                return false;
            }, 2);
            ImGui.Unindent(20);
        });

        Assert.Equal([0, 1, 2, 3, 4], calls);
        Assert.Equal(x0, xs[0], 0.01f);
        Assert.Equal(x0 + stride, xs[1], 0.01f);
        Assert.Equal(x0, xs[2], 0.01f);
        Assert.Equal(x0 + stride, xs[3], 0.01f);
        Assert.Equal(x0, xs[4], 0.01f);
        Assert.Equal(ys[0], ys[1], 0.01f);
        Assert.Equal(ys[0] + rowHeight, ys[2], 0.01f);
        Assert.Equal(ys[2], ys[3], 0.01f);
        Assert.Equal(ys[2] + rowHeight, ys[4], 0.01f);
    }

    [Fact]
    public void GridFitsColumnsToTheAvailableWidthWhenColumnsIsZero()
    {
        List<float> xs = [];
        float stride = 0;

        UiFrame.Run("test", () =>
        {
            stride = ImGui.GetContentRegionAvail().X / 2.5f;
            Controls.Grid("g", 3, stride, i =>
            {
                xs.Add(ImGui.GetCursorPosX());
                ImGui.Button("b" + i);
                return false;
            });
        });

        Assert.Equal(xs[0] + stride, xs[1], 0.01f);
        Assert.Equal(xs[0], xs[2], 0.01f);
    }

    [Fact]
    public void GridHonoursExplicitColumnsEvenWhenTheyDoNotFit()
    {
        List<float> xs = [];
        float stride = 0;

        UiFrame.Run("test", () =>
        {
            stride = ImGui.GetContentRegionAvail().X;
            Controls.Grid("g", 3, stride, i =>
            {
                xs.Add(ImGui.GetCursorPosX());
                ImGui.Button("b" + i);
                return false;
            }, 3);
        });

        Assert.Equal(xs[0] + stride, xs[1], 0.01f);
        Assert.Equal(xs[0] + stride * 2, xs[2], 0.01f);
    }

    [Fact]
    public void GridReturnsTheOrOfEveryCellAndDrawsEachCell()
    {
        var calls = 0;
        var result = new bool[3];

        UiFrame.Run("test", () =>
        {
            result[0] = Controls.Grid("none", 3, 50, _ => { calls++; return false; }, 3);
            result[1] = Controls.Grid("last", 3, 50, i => { calls++; return i == 2; }, 3);
            result[2] = Controls.Grid("first", 3, 50, i => { calls++; return i == 0; }, 3);
        });

        Assert.Equal([false, true, true], result);
        Assert.Equal(9, calls);
    }

    [Fact]
    public void GridDoesNothingForNoCellsOrNoCallback()
    {
        var calls = 0;
        bool zero = true, negative = true, noCell = true;

        UiFrame.Run("test", () =>
        {
            zero = Controls.Grid("zero", 0, 50, _ => { calls++; return true; });
            negative = Controls.Grid("negative", -2, 50, _ => { calls++; return true; });
            noCell = Controls.Grid("nocell", 3, 50, null);
        });

        Assert.False(zero);
        Assert.False(negative);
        Assert.False(noCell);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void GridScopesCellIdsPerCellAndPerGrid()
    {
        var ids = new List<uint>();

        UiFrame.Run("test", () =>
        {
            foreach (var grid in new[] { "a", "b" })
                Controls.Grid(grid, 3, 50, _ => { ids.Add(ImGui.GetID("x")); return false; }, 3);
        });

        Assert.Equal(6, ids.Distinct().Count());
    }

    [Fact]
    public void ToggleGridItemsLayOnStrideOfWidestLabelAndReadTheirGetters()
    {
        List<float> xs = [];
        var flags = new bool[3];
        float expectedStride = 0, x0 = 0;
        Controls.ToggleItem[] items =
        [
            new("A", () => { xs.Add(ImGui.GetCursorPosX()); return flags[0]; }, v => flags[0] = v),
            new("A much longer label", () => { xs.Add(ImGui.GetCursorPosX()); return flags[1]; }, v => flags[1] = v),
            new("B", () => { xs.Add(ImGui.GetCursorPosX()); return flags[2]; }, v => flags[2] = v),
        ];

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            expectedStride = Controls.LabelStride("A", "A much longer label", "B");
            Controls.ToggleGrid("items", items, 3);
        });

        Assert.Equal(3, xs.Count);
        Assert.Equal(x0, xs[0], 0.01f);
        Assert.Equal(x0 + expectedStride, xs[1], 0.01f);
        Assert.Equal(x0 + expectedStride * 2, xs[2], 0.01f);
    }

    [Fact]
    public void ToggleGridArraysLayOnStrideOfWidestLabel()
    {
        bool[] values = [false, false, false];
        string[] labels = ["A", "A much longer label", "B"];
        float x0 = 0, expectedStride = 0, lastX = 0;

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            expectedStride = Controls.LabelStride(labels);
            Controls.ToggleGrid("arr", labels, values, 3);
            lastX = ImGui.GetItemRectMin().X - ImGui.GetWindowPos().X;
        });

        Assert.Equal(x0 + expectedStride * 2, lastX, 0.01f);
    }

    [Fact]
    public void ToggleGridItemsClickFlipsOnlyThatItemAndCallsSetOnce()
    {
        var flags = new[] { true, false };
        var sets = new List<(int Index, bool Value)>();
        Controls.ToggleItem[] items =
        [
            new("One", () => flags[0], v => { flags[0] = v; sets.Add((0, v)); }),
            new("Two", () => flags[1], v => { flags[1] = v; sets.Add((1, v)); }),
        ];
        var changed = false;

        var at = CentreOf(() => Controls.ToggleGrid("items", items, 2));
        UiFrame.Click("test", at, () => changed |= Controls.ToggleGrid("items", items, 2));

        Assert.True(changed);
        Assert.Equal([(1, true)], sets);
        Assert.Equal([true, true], flags);
    }

    [Fact]
    public void ToggleGridItemsClickOnTrueItemWritesFalse()
    {
        var flag = true;
        Controls.ToggleItem[] items = [new("Only", () => flag, v => flag = v)];

        var at = CentreOf(() => Controls.ToggleGrid("items", items, 1));
        UiFrame.Click("test", at, () => Controls.ToggleGrid("items", items, 1));

        Assert.False(flag);
    }

    [Fact]
    public void ToggleGridArraysClickWritesThroughToTheClickedIndexOnly()
    {
        bool[] values = [false, false, true];
        string[] labels = ["A", "B", "C"];
        var changed = false;

        var at = CentreOf(() => Controls.ToggleGrid("arr", labels, values, 3));
        UiFrame.Click("test", at, () => changed |= Controls.ToggleGrid("arr", labels, values, 3));

        Assert.True(changed);
        Assert.Equal([false, false, false], values);
    }

    [Fact]
    public void ToggleGridArraysTolerateMismatchedLengthsAndNulls()
    {
        bool[] shortValues = [true];
        bool[] longValues = [true, false, true];
        bool a = true, b = true, c = true, d = true, e = true;

        UiFrame.Run("test", () =>
        {
            a = Controls.ToggleGrid("short", ["A", "B", "C"], shortValues);
            b = Controls.ToggleGrid("long", ["A"], longValues);
            c = Controls.ToggleGrid("nolabels", (string[]?)null, longValues);
            d = Controls.ToggleGrid("novalues", ["A"], null);
            e = Controls.ToggleGrid("empty", [], []);
            Controls.ToggleGrid("noitems", (Controls.ToggleItem[]?)null);
            Controls.ToggleGrid("emptyitems", []);
        });

        Assert.False(a || b || c || d || e);
        Assert.Equal([true], shortValues);
        Assert.Equal([true, false, true], longValues);
    }

    [Fact]
    public void ToggleGridArrayTipsShowOnlyForCellsThatHaveOne()
    {
        bool[] values = [false, false];
        string[] labels = ["A", "B"];
        string[] tips = ["tip for A"];
        Vector2 first = default, second = default;
        UiFrame.Run("test", () =>
        {
            Controls.ToggleGrid("arr", labels, values, 1);
            second = Centre();
            first = second - new Vector2(0, ImGui.GetFrameHeightWithSpacing());
        });

        var withTip = TooltipWindowsWhenHovering(() => Controls.ToggleGrid("arr", labels, values, 1, tips), first);
        var tipless = TooltipWindowsWhenHovering(() => Controls.ToggleGrid("arr", labels, values, 1, tips), second);
        var noTips = TooltipWindowsWhenHovering(() => Controls.ToggleGrid("arr", labels, values, 1), first);

        Assert.Equal(1, withTip);
        Assert.Equal(0, tipless);
        Assert.Equal(0, noTips);
    }

    [Fact]
    public void ToggleGridItemTipShowsOnHoverOnlyWhenSet()
    {
        var flag = false;
        Controls.ToggleItem[] tipped = [new("Flag", () => flag, v => flag = v, "a 100% tip")];
        Controls.ToggleItem[] plain = [new("Flag", () => flag, v => flag = v)];
        var at = CentreOf(() => Controls.ToggleGrid("items", plain, 1));

        var withTip = TooltipWindowsWhenHovering(() => Controls.ToggleGrid("items", tipped, 1), at);
        var withoutTip = TooltipWindowsWhenHovering(() => Controls.ToggleGrid("items", plain, 1), at);

        Assert.Equal(1, withTip);
        Assert.Equal(0, withoutTip);
        Assert.False(flag);
    }

    [Fact]
    public void ColorGridArraysDrawEachSwatchWithItsOwnColour()
    {
        Color[] values = [Color.FromArgb(255, 10, 200, 30), Color.FromArgb(255, 220, 40, 90), Color.FromArgb(255, 70, 70, 250)];

        var drawn = SwatchColours(() => Controls.ColorGrid("c", ["A", "B", "C"], values, 3));

        foreach (var c in values) Assert.Contains(Packed(c), drawn);
    }

    [Fact]
    public void ColorGridItemsDrawTheirGetterColour()
    {
        var one = Color.FromArgb(255, 12, 220, 34);
        var two = Color.FromArgb(255, 200, 34, 120);
        Controls.ColorItem[] items = [new("One", () => one, _ => { }), new("Two", () => two, _ => { })];

        var drawn = SwatchColours(() => Controls.ColorGrid("c", items, 2));

        Assert.Contains(Packed(one), drawn);
        Assert.Contains(Packed(two), drawn);
    }

    static int PickerVertices(Action body)
    {
        var at = CentreOf(body);
        var baseline = 0;
        UiFrame.Run("test", () =>
        {
            body();
            baseline = UiFrame.Io().MetricsRenderWindows;
        });
        UiFrame.Click("test", at, body);
        var open = 0;
        var vertices = 0;
        try
        {
            UiFrame.Run("test", () =>
            {
                body();
                open = UiFrame.Io().MetricsRenderWindows;
            });
            UiFrame.Run("test", body);
            UiFrame.Run("test", () =>
            {
                body();
                vertices = UiFrame.Io().MetricsRenderVertices;
            });
        }
        finally
        {
            UiFrame.ClickAway("test", body);
        }
        var closed = 0;
        UiFrame.Run("test", () =>
        {
            body();
            closed = UiFrame.Io().MetricsRenderWindows;
        });
        Assert.True(open > baseline);
        Assert.Equal(baseline, closed);
        return vertices;
    }

    [Fact]
    public void ColorGridArraysForwardAlphaToThePicker()
    {
        Color[] values = [Color.FromArgb(100, 20, 180, 60)];

        var withAlpha = PickerVertices(() => Controls.ColorGrid("c", ["A"], values, 1, true));
        var withoutAlpha = PickerVertices(() => Controls.ColorGrid("c", ["A"], values, 1, false));

        Assert.True(withAlpha > withoutAlpha);
    }

    [Fact]
    public void ColorGridItemsForwardAlphaToThePicker()
    {
        var colour = Color.FromArgb(100, 20, 180, 60);
        Controls.ColorItem[] items = [new("A", () => colour, _ => { })];

        var withAlpha = PickerVertices(() => Controls.ColorGrid("c", items, 1, true));
        var withoutAlpha = PickerVertices(() => Controls.ColorGrid("c", items, 1, false));

        Assert.True(withAlpha > withoutAlpha);
    }

    [Fact]
    public void ColorGridItemsLayOnStrideOfWidestLabelInTheGivenColumns()
    {
        List<float> xs = [];
        var colour = Color.White;
        float x0 = 0, expectedStride = 0;
        Controls.ColorItem[] items =
        [
            new("A", () => { xs.Add(ImGui.GetCursorPosX()); return colour; }, _ => { }),
            new("A much longer label", () => { xs.Add(ImGui.GetCursorPosX()); return colour; }, _ => { }),
            new("B", () => { xs.Add(ImGui.GetCursorPosX()); return colour; }, _ => { }),
        ];

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            expectedStride = Controls.LabelStride("A", "A much longer label", "B");
            Controls.ColorGrid("items", items, 3);
        });

        Assert.Equal(x0, xs[0], 0.01f);
        Assert.Equal(x0 + expectedStride, xs[1], 0.01f);
        Assert.Equal(x0 + expectedStride * 2, xs[2], 0.01f);
    }

    [Fact]
    public void ColorGridArraysLayOnStrideOfWidestLabelInTheGivenColumns()
    {
        Color[] values = [Color.White, Color.White, Color.White];
        string[] labels = ["A", "A much longer label", "B"];
        float x0 = 0, expectedStride = 0, lastX = 0;

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            expectedStride = Controls.LabelStride(labels);
            Controls.ColorGrid("arr", labels, values, 3);
            lastX = ImGui.GetItemRectMin().X - ImGui.GetWindowPos().X;
        });

        Assert.Equal(x0 + expectedStride * 2, lastX, 0.01f);
    }

    [Fact]
    public void ColorGridWritesThroughBothOverloads()
    {
        var original = Color.FromArgb(255, 200, 100, 50);
        var viaItems = original;
        Color[] viaArray = [original];
        var itemSets = new List<Color>();
        var itemsChanged = false;
        var arrayChanged = false;
        var target = 0;
        var swatch = Vector2.Zero;
        var picker = Vector2.Zero;
        void Body()
        {
            var start = ImGui.GetCursorScreenPos();
            if (target == 0)
                itemsChanged |= Controls.ColorGrid("c", [new Controls.ColorItem("c", () => viaItems, c => { viaItems = c; itemSets.Add(c); })], 1);
            else
                arrayChanged |= Controls.ColorGrid("c", ["c"], viaArray, 1);
            swatch = start + new Vector2(ImGui.GetFrameHeight() / 2, ImGui.GetFrameHeight() / 2);
            picker = start + new Vector2(ImGui.GetFrameHeight() * 3, ImGui.GetFrameHeight() * 5);
        }

        for (target = 0; target < 2; target++)
        {
            var window = "colour grid write " + target;
            try
            {
                UiFrame.Run(window, Body);
                UiFrame.Click(window, swatch, Body);
                UiFrame.Click(window, picker, Body);
            }
            finally
            {
                UiFrame.ClickAway(window, Body);
            }
        }

        Assert.True(itemsChanged);
        Assert.True(arrayChanged);
        Assert.Single(itemSets);
        Assert.NotEqual(original, itemSets[0]);
        Assert.Equal(itemSets[0], viaItems);
        Assert.NotEqual(original, viaArray[0]);
    }

    [Fact]
    public void ToggleGridDefaultsToTwoColumns()
    {
        List<float> xs = [];
        bool[] values = [false, false, false];
        float x0 = 0, stride = 0, arrayX = 0;
        Controls.ToggleItem[] items =
        [
            new("A", () => { xs.Add(ImGui.GetCursorPosX()); return false; }, _ => { }),
            new("B", () => { xs.Add(ImGui.GetCursorPosX()); return false; }, _ => { }),
            new("C", () => { xs.Add(ImGui.GetCursorPosX()); return false; }, _ => { }),
        ];

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            stride = Controls.LabelStride("A", "B", "C");
            Controls.ToggleGrid("items", items);
            Controls.ToggleGrid("arr", ["A", "B", "C"], values);
            arrayX = ImGui.GetItemRectMin().X - ImGui.GetWindowPos().X;
        });

        Assert.Equal(x0 + stride, xs[1], 0.01f);
        Assert.Equal(x0, xs[2], 0.01f);
        Assert.Equal(x0, arrayX, 0.01f);
    }

    [Fact]
    public void ColorGridDefaultsToAutoFit()
    {
        List<float> shortXs = [], longXs = [];
        Color[] values = [Color.White, Color.White, Color.White];
        float x0 = 0, shortStride = 0, arrayX = 0;
        var wide = "W";
        Action<Color> set = _ => { };

        UiFrame.Run("test", () =>
        {
            x0 = ImGui.GetCursorPosX();
            while (ImGui.CalcTextSize(wide).X < ImGui.GetContentRegionAvail().X * 0.6f) wide += "W";
            shortStride = Controls.LabelStride("a", "b", "c");
            Controls.ColorGrid("short", [
                new Controls.ColorItem("a", () => { shortXs.Add(ImGui.GetCursorPosX()); return Color.White; }, set),
                new Controls.ColorItem("b", () => { shortXs.Add(ImGui.GetCursorPosX()); return Color.White; }, set),
                new Controls.ColorItem("c", () => { shortXs.Add(ImGui.GetCursorPosX()); return Color.White; }, set)]);
            Controls.ColorGrid("long", [
                new Controls.ColorItem(wide, () => { longXs.Add(ImGui.GetCursorPosX()); return Color.White; }, set),
                new Controls.ColorItem(wide, () => { longXs.Add(ImGui.GetCursorPosX()); return Color.White; }, set)]);
            Controls.ColorGrid("arr", ["a", "b", "c"], values);
            arrayX = ImGui.GetItemRectMin().X - ImGui.GetWindowPos().X;
        });

        Assert.Equal(x0 + shortStride, shortXs[1], 0.01f);
        Assert.Equal(x0 + shortStride * 2, shortXs[2], 0.01f);
        Assert.Equal(longXs[0], longXs[1], 0.01f);
        Assert.Equal(x0 + shortStride * 2, arrayX, 0.01f);
    }

    [Fact]
    public void ColorGridToleratesNullsMismatchesAndEmpty()
    {
        Color[] one = [Color.White];
        bool a = true, b = true, c = true, d = true;

        UiFrame.Run("test", () =>
        {
            a = Controls.ColorGrid("a", ["A", "B"], one);
            b = Controls.ColorGrid("b", (string[]?)null, one);
            c = Controls.ColorGrid("c", ["A"], null);
            d = Controls.ColorGrid("d", (Controls.ColorItem[]?)null);
            Controls.ColorGrid("e", []);
            Controls.ColorGrid("f", [], []);
        });

        Assert.False(a || b || c || d);
    }
}
