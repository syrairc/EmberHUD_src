using System.Drawing;
using System.Globalization;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class WeightTests
{
    static readonly Color Red = Color.FromArgb(255, 224, 103, 103);
    static readonly Color Green = Color.FromArgb(255, 111, 207, 122);
    static readonly Color Gold = Color.FromArgb(255, 217, 193, 103);

    [Theory]
    [InlineData(0f, -10f, 10f, 0.5f)]
    [InlineData(-10f, -10f, 10f, 0f)]
    [InlineData(10f, -10f, 10f, 1f)]
    [InlineData(999f, -10f, 10f, 1f)]
    [InlineData(-999f, -10f, 10f, 0f)]
    [InlineData(5f, 3f, 3f, 0f)]
    public void NormClampsIntoZeroToOne(float v, float min, float max, float expected) => Assert.Equal(expected, Weight.Norm(v, min, max));

    [Fact]
    public void AnInvertedRangeStaysInZeroToOne() => Assert.InRange(Weight.Norm(0f, 10f, -10f), 0f, 1f);

    [Theory]
    [InlineData(4.6f, 1f, 5f)]
    [InlineData(4.6f, 0f, 4.6f)]
    [InlineData(-4.6f, 1f, -5f)]
    [InlineData(1.24f, 0.5f, 1f)]
    [InlineData(2.5f, 1f, 2f)]
    [InlineData(3.5f, 1f, 4f)]
    public void SnapRoundsToTheStepHalfToEven(float v, float step, float expected) => Assert.Equal(expected, Weight.Snap(v, step));

    [Fact]
    public void SnapNeverReturnsNegativeZero()
    {
        Assert.False(float.IsNegative(Weight.Snap(-0.4f, 1f)));
        Assert.Equal("0", Weight.Fmt(Weight.Snap(-0.4f, 1f), 0));
    }

    [Fact]
    public void EffStepIsOneForIntegersContinuousForDecimalsAndAnExplicitStepWins()
    {
        Assert.Equal(1f, Weight.EffStep(new WeightOpts { Decimals = 0 }));
        Assert.Equal(0f, Weight.EffStep(new WeightOpts { Decimals = 1 }));
        Assert.Equal(5f, Weight.EffStep(new WeightOpts { Decimals = 0, Step = 5f }));
    }

    [Theory]
    [InlineData(4.6f, 0, "5")]
    [InlineData(4.25f, 2, "4.25")]
    [InlineData(0.35f, 2, "0.35")]
    public void FmtIsInvariant(float v, int decimals, string expected) => Assert.Equal(expected, Weight.Fmt(v, decimals));

    [Fact]
    public void LerpHitsItsStopsAndClamps()
    {
        Assert.Equal(Red, Weight.Lerp([Red, Green], 0f));
        Assert.Equal(Green, Weight.Lerp([Red, Green], 1f));
        Assert.Equal(Gold, Weight.Lerp([Red, Gold, Green], 0.5f));
        Assert.Equal(Red, Weight.Lerp([Red], 0.7f));
        Assert.Equal(0, Weight.Lerp([], 0.5f).A);
        Assert.Equal(Green, Weight.Lerp([Red, Green], 2f));
    }

    [Fact]
    public void GrabColorPrefersTheScaleThenTheFlatColour()
    {
        Assert.Equal(Red, Weight.GrabColor(0f, new WeightOpts { Flat = Red }));
        Assert.Equal(Red, Weight.GrabColor(-10f, new WeightOpts { Scale = [Red, Green] }));
        Assert.Equal(Green, Weight.GrabColor(10f, new WeightOpts { Scale = [Red, Green] }));
        Assert.Equal(Red, Weight.GrabColor(0f, new WeightOpts { Flat = Green, Scale = [Red, Red] }));
    }

    [Fact]
    public void CopyDoesNotAliasTheOriginal()
    {
        var original = new WeightOpts { Min = 0f, Max = 5f };
        var copy = original.Copy();
        copy.Min = -3f;
        Assert.Equal(0f, original.Min);
        Assert.Equal(5f, copy.Max);
    }

    [Fact]
    public void ScalesHaveTwoAndThreeStops()
    {
        Assert.Equal(2, Weight.Scales.RedGreen.Length);
        Assert.Equal(3, Weight.Scales.RedGoldGreen.Length);
    }

    [Fact]
    public void ATypedValueIsClampedAndSnapped()
    {
        var ints = new WeightOpts { Min = -10f, Max = 10f, Decimals = 0 };
        Assert.Equal(10f, Weight.Commit(999f, ints));
        Assert.Equal(-10f, Weight.Commit(-999f, ints));
        Assert.Equal(5f, Weight.Commit(4.6f, ints));
        Assert.False(float.IsNegative(Weight.Commit(-0.4f, ints)));
        var decimals = new WeightOpts { Min = 0f, Max = 30f, Decimals = 2 };
        Assert.Equal(4.25f, Weight.Commit(4.25f, decimals));
        Assert.Equal(30f, Weight.Commit(31f, decimals));
        Assert.Equal(35f, Weight.Commit(37f, new WeightOpts { Min = 0f, Max = 100f, Decimals = 0, Step = 5f }));
        Assert.Equal(5f, Weight.Commit(5f, new WeightOpts { Min = 10f, Max = -10f, Decimals = 0 }));
    }

    [Fact]
    public void SlidersDrawOneBalancedFrameAndChangeNothingUntouched()
    {
        var v = 2f;
        var changed = false;
        UiFrame.Run("test", () =>
        {
            changed |= Weight.Slider("Weight", ref v);
            changed |= Weight.Slider("Scaled", () => v, x => v = x, new WeightOpts { Scale = Weight.Scales.RedGoldGreen, Rail = true, Decimals = 1 });
            changed |= Weight.Slider("", ref v, new WeightOpts { EditReadout = false }, "bare");
        });
        Assert.False(changed);
        Assert.Equal(2f, v);
    }

    static (Vector2 Origin, float Height, Vector2 ReadoutMin, Vector2 ReadoutMax) Probe(float v, WeightOpts opts)
    {
        var value = v;
        Vector2 origin = default, readoutMin = default, readoutMax = default;
        var height = 0f;
        UiFrame.Run("weight", () =>
        {
            origin = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            Weight.Slider("", ref value, opts, "w");
            readoutMin = ImGui.GetItemRectMin();
            readoutMax = ImGui.GetItemRectMax();
        });
        return (origin, height, readoutMin, readoutMax);
    }

    [Fact]
    public void DraggingTheBarMovesTheValueToTheRangeEnds()
    {
        UiFrame.Unhover();
        var v = 5f;
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        var changedFlags = new List<bool>();
        void Body() => changedFlags.Add(Weight.Slider("", ref v, opts, "w"));
        try
        {
            var (origin, height, _, _) = Probe(v, opts);
            var barPoint = origin + new Vector2(5f, height / 2f);
            UiFrame.Press("weight", barPoint, Body);
            Assert.Equal(0f, v);
            UiFrame.MoveTo("weight", new Vector2(UiFrame.WindowSize.X * 4f, barPoint.Y), Body);
            Assert.Equal(10f, v);
            Assert.Contains(true, changedFlags);
        }
        finally
        {
            UiFrame.Release("weight", Body);
        }
    }

    [Fact]
    public void TypingIntoTheReadoutCommitsAClampedValue()
    {
        UiFrame.Unhover();
        var v = 5f;
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        void Body() => Weight.Slider("", ref v, opts, "w");
        var (_, _, min, max) = Probe(v, opts);
        var box = (min + max) / 2f;
        UiFrame.EraseText("weight", Body, box);
        UiFrame.Click("weight", box, Body);
        UiFrame.Io().AddInputCharactersUTF8("99");
        UiFrame.Run("weight", Body);
        UiFrame.ClickAway("weight", Body);
        Assert.Equal(10f, v);
    }

    [Fact]
    public void ARailedBarDrawsTheFadedRailColour()
    {
        var v = 10f;
        var opts = new WeightOpts { Min = 0f, Max = 10f, Rail = true, EditReadout = false, Flat = Green };
        uint[] colours = [];
        UiFrame.Run("weight", () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            Weight.Slider("", ref v, opts, "w");
            colours = Enumerable.Range(from, list.VtxBuffer.Size - from).Select(i => list.VtxBuffer[i].Col).ToArray();
        });
        Assert.Contains(EColor.U32(Green), colours);
        Assert.Contains(EColor.U32(EColor.Fade(Green, opts.RailAlpha)), colours);
    }

    static ((Vector2 Min, Vector2 Max) Bar, (Vector2 Min, Vector2 Max) Grab) BarAndGrab(float v, WeightOpts opts)
    {
        var style = ImGui.GetStyle();
        var saved = style.Colors[(int)ImGuiCol.FrameBg];
        var frame = new Vector4(0.9f, 0.1f, 0.9f, 1f);
        List<ImDrawVert> verts = [];
        try
        {
            style.Colors[(int)ImGuiCol.FrameBg] = frame;
            UiFrame.Run("weight", () =>
            {
                var list = ImGui.GetWindowDrawList();
                var from = list.VtxBuffer.Size;
                Weight.Slider("", ref v, opts, "w");
                for (var i = from; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
            });
        }
        finally
        {
            style.Colors[(int)ImGuiCol.FrameBg] = saved;
        }
        static (Vector2, Vector2) Extent(IEnumerable<ImDrawVert> points) => (new Vector2(points.Min(p => p.Pos.X), points.Min(p => p.Pos.Y)), new Vector2(points.Max(p => p.Pos.X), points.Max(p => p.Pos.Y)));
        return (Extent(verts.Where(p => p.Col == ImGui.GetColorU32(frame))), Extent(verts.Where(p => p.Col == EColor.U32(Green))));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(5f, 0.5f)]
    [InlineData(10f, 1f)]
    public void TheGrabSitsInsideTheBarAtTheValuesFraction(float v, float fraction)
    {
        var opts = new WeightOpts { Min = 0f, Max = 10f, GrabWidth = 9f, Flat = Green, EditReadout = false };
        var (bar, grab) = BarAndGrab(v, opts);
        var travel = bar.Max.X - bar.Min.X - opts.GrabWidth - 2f;
        Assert.Equal(bar.Min.X + 1f + travel * fraction, grab.Min.X, 0.01f);
        Assert.Equal(opts.GrabWidth, grab.Max.X - grab.Min.X, 0.01f);
        Assert.Equal(bar.Min.Y + 2f, grab.Min.Y, 0.01f);
        Assert.Equal(bar.Max.Y - 2f, grab.Max.Y, 0.01f);
    }

    [Fact]
    public void TheTrackHeightSetsTheBarHeight()
    {
        var (tall, _) = BarAndGrab(5f, new WeightOpts { Min = 0f, Max = 10f, TrackHeight = 40f, Flat = Green, EditReadout = false });
        Assert.Equal(40f, tall.Max.Y - tall.Min.Y, 0.01f);
    }

    [Fact]
    public void FmtIgnoresTheCurrentCulture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("4.25", Weight.Fmt(4.25f, 2));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void HoldingTheBarAtTheEndReportsNoFurtherChange()
    {
        UiFrame.Unhover();
        var v = 5f;
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        var changedFlags = new List<bool>();
        void Body() => changedFlags.Add(Weight.Slider("", ref v, opts, "w"));
        try
        {
            var (origin, height, _, _) = Probe(v, opts);
            var barPoint = origin + new Vector2(5f, height / 2f);
            UiFrame.Press("weight", barPoint, Body);
            UiFrame.MoveTo("weight", new Vector2(UiFrame.WindowSize.X * 4f, barPoint.Y), Body);
            changedFlags.Clear();
            UiFrame.MoveTo("weight", new Vector2(UiFrame.WindowSize.X * 5f, barPoint.Y), Body);
            Assert.Equal(10f, v);
            Assert.DoesNotContain(true, changedFlags);
        }
        finally
        {
            UiFrame.Release("weight", Body);
        }
    }

    [Fact]
    public void TypingPastTheRangeEndReportsNoChange()
    {
        UiFrame.Unhover();
        var v = 10f;
        var changed = false;
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        void Body() => changed |= Weight.Slider("", ref v, opts, "w");
        var (_, _, min, max) = Probe(v, opts);
        UiFrame.Click("weight", (min + max) / 2f, Body);
        UiFrame.Io().AddInputCharactersUTF8("99");
        UiFrame.Run("weight", Body);
        UiFrame.ClickAway("weight", Body);
        Assert.False(changed);
        Assert.Equal(10f, v);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheReadoutIsAnInputOnlyWhenEditable(bool editable)
    {
        var v = 1f;
        uint lastItem = 0;
        UiFrame.Run("weight", () =>
        {
            Weight.Slider("", ref v, new WeightOpts { EditReadout = editable }, "w");
            lastItem = ImGui.GetItemID();
        });
        Assert.Equal(editable, lastItem != 0);
    }

    [Fact]
    public void DraggingThroughGetSetCallsSetAndReturnsTrue()
    {
        UiFrame.Unhover();
        var stored = 5f;
        var sets = new List<float>();
        var returns = new List<bool>();
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        void Body() => returns.Add(Weight.Slider("", () => stored, x => { stored = x; sets.Add(x); }, opts, "w"));
        try
        {
            var (origin, height, _, _) = Probe(stored, opts);
            var barPoint = origin + new Vector2(5f, height / 2f);
            UiFrame.Press("weight", barPoint, Body);
            UiFrame.MoveTo("weight", new Vector2(UiFrame.WindowSize.X * 4f, barPoint.Y), Body);
            Assert.Equal(10f, stored);
            Assert.Equal(10f, sets[^1]);
            Assert.Contains(true, returns);
        }
        finally
        {
            UiFrame.Release("weight", Body);
        }
    }

    [Fact]
    public void TypingThroughGetSetCallsSetAndReturnsTrue()
    {
        UiFrame.Unhover();
        var stored = 5f;
        var sets = new List<float>();
        var returns = new List<bool>();
        var opts = new WeightOpts { Min = 0f, Max = 10f, Decimals = 0 };
        void Body() => returns.Add(Weight.Slider("", () => stored, x => { stored = x; sets.Add(x); }, opts, "w"));
        var (_, _, min, max) = Probe(stored, opts);
        var box = (min + max) / 2f;
        UiFrame.Click("weight", box, Body);
        UiFrame.Io().AddInputCharactersUTF8("99");
        UiFrame.Run("weight", Body);
        UiFrame.ClickAway("weight", Body);
        Assert.Equal(10f, stored);
        Assert.Equal([10f], sets);
        Assert.Contains(true, returns);
    }

    [Fact]
    public void GetSetLeavesTheValueAloneUntouched()
    {
        var stored = 3f;
        var sets = 0;
        var changed = false;
        UiFrame.Run("weight", () => changed |= Weight.Slider("", () => stored, _ => sets++, null, "w"));
        Assert.False(changed);
        Assert.Equal(0, sets);
    }
}
