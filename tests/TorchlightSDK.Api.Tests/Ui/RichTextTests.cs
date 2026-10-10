using System.Drawing;
using System.Numerics;
using TorchlightSDK.Testing;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class RichTextTests
{
    static readonly Color Ink = Color.FromArgb(255, 250, 240, 10);
    static readonly Color Backing = Color.FromArgb(200, 0, 0, 0);
    static readonly Color Clear = Color.FromArgb(0, 0, 0, 0);
    static readonly Vector2 Origin = new(10, 10);
    static readonly Vector2 BoxMin = new(100, 100);
    static readonly Vector2 BoxMax = new(160, 140);

    record Drawn(List<ImDrawVert> Verts, int Indices);

    static Drawn Capture(Action<ImDrawListPtr> draw)
    {
        Drawn? drawn = null;
        TestFrame.Run(() =>
        {
            var list = ImGui.GetForegroundDrawList();
            var firstVert = list.VtxBuffer.Size;
            var firstIndex = list.IdxBuffer.Size;
            draw(list);
            List<ImDrawVert> verts = [];
            for (var i = firstVert; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
            drawn = new Drawn(verts, list.IdxBuffer.Size - firstIndex);
        });
        return drawn!;
    }

    static List<ImDrawVert> Coloured(Drawn drawn, Color colour) => drawn.Verts.Where(v => v.Col == EColor.U32(colour)).ToList();

    static (Vector2 Min, Vector2 Max) Bounds(IEnumerable<ImDrawVert> verts)
    {
        var list = verts.ToList();
        return (new Vector2(list.Min(v => v.Pos.X), list.Min(v => v.Pos.Y)), new Vector2(list.Max(v => v.Pos.X), list.Max(v => v.Pos.Y)));
    }

    static Vector2 Measure(float size, string text)
    {
        var measured = Vector2.Zero;
        TestFrame.Run(() => measured = ImGui.CalcTextSizeA(ImGui.GetFont(), size, float.MaxValue, 0f, text));
        return measured;
    }

    static float DefaultSize()
    {
        var size = 0f;
        TestFrame.Run(() => size = ImGui.GetFontSize());
        return size;
    }

    static Vector2 PillSize(string text, Color bg, float size = 0f, float padX = 4f, float padY = 2f)
    {
        var result = Vector2.Zero;
        TestFrame.Run(() => result = RichText.Pill(ImGui.GetForegroundDrawList(), ImGui.GetFont(), size > 0f ? size : ImGui.GetFontSize(), Origin, text, Ink, bg, padX, padY));
        return result;
    }

    static Drawn Pill(Color bg, float padX = 4f, float padY = 2f, float rounding = 0f) =>
        Capture(list => RichText.Pill(list, ImGui.GetFont(), ImGui.GetFontSize(), Origin, "OVERLAY", Ink, bg, padX, padY, rounding));

    [Fact]
    public void APillIsItsTextPlusThePaddingOnBothSides()
    {
        Vector2 pill = default, text = default;
        TestFrame.Run(() =>
        {
            var dl = ImGui.GetForegroundDrawList();
            pill = RichText.Pill(dl, ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(10, 10), "OVERLAY", Color.White, Color.FromArgb(200, 0, 0, 0));
            text = ImGui.CalcTextSize("OVERLAY");
            RichText.Highlight(dl, new Vector2(10, 10), new Vector2(60, 60), Color.Gold);
        });
        Assert.Equal(text.X + 8f, pill.X, 0.01f);
        Assert.Equal(text.Y + 4f, pill.Y, 0.01f);
    }

    [Fact]
    public void PillPaddingArgumentsReplaceTheDefaults()
    {
        var text = Measure(DefaultSize(), "OVERLAY");
        var pill = PillSize("OVERLAY", Backing, padX: 10f, padY: 6f);
        Assert.Equal(text.X + 20f, pill.X, 0.01f);
        Assert.Equal(text.Y + 12f, pill.Y, 0.01f);
    }

    [Fact]
    public void PillMeasuresTheTextAtTheSizeItWasGiven()
    {
        var size = DefaultSize() * 2f;
        var text = Measure(size, "OVERLAY");
        var pill = PillSize("OVERLAY", Backing, size);
        Assert.Equal(text.X + 8f, pill.X, 0.01f);
        Assert.Equal(text.Y + 4f, pill.Y, 0.01f);
        Assert.True(pill.X > Measure(DefaultSize(), "OVERLAY").X + 8f);
    }

    [Fact]
    public void ThePillSizeIsTheSameWithoutABackground()
    {
        Assert.Equal(PillSize("OVERLAY", Backing), PillSize("OVERLAY", Clear));
    }

    [Fact]
    public void ThePillBackgroundIsTheReturnedSizeRectAroundThePaddedText()
    {
        Vector2 size = default;
        var drawn = Capture(list => size = RichText.Pill(list, ImGui.GetFont(), ImGui.GetFontSize(), Origin, "OVERLAY", Ink, Backing, 4f, 2f, 0f));
        var background = Coloured(drawn, Backing);
        var (min, max) = Bounds(background);
        Assert.Equal(Origin - new Vector2(4f, 2f), min);
        Assert.Equal(min + size, max);
        Assert.Equal(4, background.Count);
    }

    [Fact]
    public void ThePillBackgroundRoundsItsCorners()
    {
        Assert.True(Coloured(Pill(Backing, rounding: 6f), Backing).Count > Coloured(Pill(Backing, rounding: 0f), Backing).Count);
    }

    [Fact]
    public void ThePillDefaultRoundingIsTwoPixels()
    {
        var defaulted = Capture(list => RichText.Pill(list, ImGui.GetFont(), ImGui.GetFontSize(), Origin, "OVERLAY", Ink, Backing));
        Assert.Equal(Coloured(Pill(Backing, rounding: 2f), Backing).Select(v => v.Pos), Coloured(defaulted, Backing).Select(v => v.Pos));
        Assert.True(Coloured(defaulted, Backing).Count > Coloured(Pill(Backing, rounding: 0f), Backing).Count);
    }

    [Fact]
    public void ThePillTextIsDrawnInTheTextColourInsideTheBackground()
    {
        var drawn = Pill(Backing);
        var glyphs = Coloured(drawn, Ink);
        Assert.NotEmpty(glyphs);
        var (glyphMin, glyphMax) = Bounds(glyphs);
        var (boxMin, boxMax) = Bounds(Coloured(drawn, Backing));
        Assert.True(glyphMin.X >= boxMin.X && glyphMin.Y >= boxMin.Y && glyphMax.X <= boxMax.X && glyphMax.Y <= boxMax.Y);
        Assert.InRange(glyphMin.X, Origin.X - 0.5f, Origin.X + 3f);
        Assert.InRange(glyphMin.Y, Origin.Y - 0.5f, Origin.Y + 3f);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void ThePillTextIsThePlainTextAtItsOwnPositionAndSize(float scale)
    {
        var size = DefaultSize() * scale;
        var pilled = Capture(list => RichText.Pill(list, ImGui.GetFont(), size, Origin, "OVERLAY", Ink, Clear));
        var plain = Capture(list => ImGui.AddText(list, ImGui.GetFont(), size, Origin, EColor.U32(Ink), "OVERLAY"));
        Assert.NotEmpty(plain.Verts);
        Assert.Equal(plain.Verts.Select(v => v.Pos), pilled.Verts.Select(v => v.Pos));
    }

    [Fact]
    public void ThePillTextDoesNotMoveWhenThePaddingChanges()
    {
        var tight = Coloured(Pill(Backing, 1f, 1f), Ink).Select(v => v.Pos);
        var loose = Coloured(Pill(Backing, 12f, 8f), Ink).Select(v => v.Pos);
        Assert.Equal(tight, loose);
    }

    [Fact]
    public void AClearBackgroundDrawsTheTextAlone()
    {
        var drawn = Pill(Clear);
        Assert.NotEmpty(drawn.Verts);
        Assert.All(drawn.Verts, v => Assert.Equal(EColor.U32(Ink), v.Col));
        Assert.True(Bounds(drawn.Verts).Min.X >= Origin.X - 0.5f);
    }

    [Fact]
    public void ABackgroundAddsOnlyTheRectOnTopOfTheText()
    {
        var clear = Pill(Clear);
        var backed = Pill(Backing);
        Assert.Equal(clear.Verts.Count + 4, backed.Verts.Count);
        Assert.Equal(EColor.U32(Backing), backed.Verts[0].Col);
    }

    [Fact]
    public void AHighlightIsAnOutlineInTheGivenColourNotAFill()
    {
        var drawn = Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold));
        Assert.NotEmpty(drawn.Verts);
        Assert.All(drawn.Verts, v => Assert.Equal(EColor.U32(Color.Gold), v.Col));
        Assert.True(drawn.Indices > 6);
    }

    [Fact]
    public void AHighlightGrowsTheRectByTheExpandOnEverySide()
    {
        var flush = Bounds(Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 3f, 0f, 0f)).Verts);
        var grown = Bounds(Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 3f, 0f, 5f)).Verts);
        Assert.Equal(flush.Min.X - 5f, grown.Min.X, 0.01f);
        Assert.Equal(flush.Min.Y - 5f, grown.Min.Y, 0.01f);
        Assert.Equal(flush.Max.X + 5f, grown.Max.X, 0.01f);
        Assert.Equal(flush.Max.Y + 5f, grown.Max.Y, 0.01f);
    }

    [Fact]
    public void AHighlightThicknessGrowsTheOutlineOutwardByHalfTheDifference()
    {
        var thin = Bounds(Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 1f, 0f, 0f)).Verts);
        var thick = Bounds(Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 5f, 0f, 0f)).Verts);
        Assert.Equal(thin.Min.X - 2f, thick.Min.X, 0.01f);
        Assert.Equal(thin.Min.Y - 2f, thick.Min.Y, 0.01f);
        Assert.Equal(thin.Max.X + 2f, thick.Max.X, 0.01f);
        Assert.Equal(thin.Max.Y + 2f, thick.Max.Y, 0.01f);
    }

    [Fact]
    public void AHighlightRoundsItsCorners()
    {
        var square = Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 3f, 0f));
        var rounded = Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 3f, 8f));
        Assert.True(rounded.Verts.Count > square.Verts.Count);
    }

    [Fact]
    public void AHighlightDefaultsAreThreePixelsThickAndTwoPixelsOut()
    {
        var defaulted = Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold));
        var explicitValues = Capture(list => RichText.Highlight(list, BoxMin, BoxMax, Color.Gold, 3f, 0f, 2f));
        Assert.Equal(explicitValues.Verts.Select(v => v.Pos), defaulted.Verts.Select(v => v.Pos));
    }
}
