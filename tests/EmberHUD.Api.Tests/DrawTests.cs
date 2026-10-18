using System.Collections.Generic;
using System.Drawing;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class DrawTests
{
    static readonly Vector2 Centre = new(100, 50);
    const int GlowVertices = 1 + 2 * Draw.GlowSegments;

    [Theory]
    [InlineData(Align.TopLeft, 0, 0)]
    [InlineData(Align.Center, 20, 0)]
    [InlineData(Align.Right, 40, 0)]
    [InlineData(Align.Middle, 0, 5)]
    [InlineData(Align.Bottom, 0, 10)]
    [InlineData(Align.Center | Align.Middle, 20, 5)]
    [InlineData(Align.Right | Align.Bottom, 40, 10)]
    public void AlignOffsetMovesTheTopLeftCorner(Align align, float x, float y) => Assert.Equal(new Vector2(x, y), Draw.AlignOffset(new Vector2(40, 10), align));

    [Fact]
    public void RotatedQuadFacingUpTurnsTheSpriteLikeAPlayerArrow() =>
        Assert.Equal((new Vector2(110, 40), new Vector2(110, 60), new Vector2(90, 60), new Vector2(90, 40)), Draw.RotatedQuad(Centre, new Vector2(20), new Vector2(0, -1)));

    [Fact]
    public void RotatedQuadFacingRight() =>
        Assert.Equal((new Vector2(110, 60), new Vector2(90, 60), new Vector2(90, 40), new Vector2(110, 40)), Draw.RotatedQuad(Centre, new Vector2(20), new Vector2(1, 0)));

    [Fact]
    public void RotatedQuadOnlyCaresAboutTheDirection() =>
        Assert.Equal((new Vector2(90, 60), new Vector2(90, 40), new Vector2(110, 40), new Vector2(110, 60)), Draw.RotatedQuad(Centre, new Vector2(20), new Vector2(0, 5)));

    [Fact]
    public void RotatedQuadUsesSizeXAcrossAndSizeYAlong() =>
        Assert.Equal((new Vector2(105, 40), new Vector2(105, 60), new Vector2(95, 60), new Vector2(95, 40)), Draw.RotatedQuad(Centre, new Vector2(20, 10), new Vector2(0, -1)));

    [Fact]
    public void RotatedQuadWithoutADirectionIsNull() => Assert.Null(Draw.RotatedQuad(Centre, new Vector2(20), Vector2.Zero));

    [Fact]
    public void EveryMethodDrawsInsideAFrame() => TestFrame.Run(() =>
    {
        var sprite = Unit();
        Assert.Equal(Draw.MeasureText("hello"), Draw.Text("hello", new Vector2(10, 10), Color.White));
        Assert.True(Draw.MeasureText("abc", 26).Y > Draw.MeasureText("abc", 13).Y);
        Assert.True(Draw.MeasureText("abc", face: FontFace.Mono).X > 0);
        Draw.TextOutlined("outlined", new Vector2(10, 30), Color.White, Color.Black, 2, Align.Center | Align.Middle);
        Draw.Line(Vector2.Zero, new Vector2(50), Color.Red, 2);
        Draw.Polyline([new Vector2(0, 0), new Vector2(5, 5), new Vector2(9, 0)], Color.Red, closed: true);
        Draw.Polyline([new Vector2(1, 1)], Color.Red);
        Draw.Rect(Vector2.Zero, new Vector2(20), Color.Red, 1, 2);
        Draw.RectFilled(Vector2.Zero, new Vector2(20), Color.Red, 2);
        Draw.Circle(new Vector2(30), 5, Color.Red);
        Draw.CircleFilled(new Vector2(30), 5, Color.Red, 12);
        Draw.TriangleFilled(Vector2.Zero, new Vector2(5, 0), new Vector2(0, 5), Color.Red);
        Draw.ConvexPolyFilled([new Vector2(0, 0), new Vector2(5, 0), new Vector2(5, 5)], Color.Red);
        Draw.Glow(Centre, 12, Color.Gold, 4);
        Draw.Marker(Centre, 5, Color.Red, MarkerShape.Dot, 1);
        Draw.Marker(Centre, 6, Color.Red, MarkerShape.Star, 1);
        foreach (var shape in Enum.GetValues<MarkerShape>()) Draw.Marker(Centre, 6, Color.Red, shape, 1);
        Draw.Icon("marker:Hexagon", Centre, 6, Color.Red, 1);
        Draw.Image(sprite, Vector2.Zero, new Vector2(16), list: Draw.Foreground);
        Draw.ImageRotated(sprite, Centre, new Vector2(16), new Vector2(0, -1), Color.Gold);
        Draw.ImageRotated(sprite, Centre, new Vector2(16), Vector2.Zero);
        using (Draw.Clip(Vector2.Zero, new Vector2(50))) Draw.Line(Vector2.Zero, new Vector2(100), Color.Red);
    });

    [Fact]
    public void FontScopesSetTheSizeAndPopCleanly() => TestFrame.Run(() =>
    {
        using (Fonts.Use(FontFace.Mono, 20)) Assert.Equal(20f, ImGui.GetFontSize());
        using (Fonts.Use(FontFace.Regular, 18)) Assert.Equal(18f, ImGui.GetFontSize());
        using (Fonts.Use(FontFace.Bold, 19)) Assert.Equal(19f, ImGui.GetFontSize());
        using (Fonts.Use(FontFace.Label, 21)) Assert.Equal(21f, ImGui.GetFontSize());
    });

    [Fact]
    public void AnUnknownFaceMeasuresLikeRegular() => TestFrame.Run(() =>
        Assert.Equal(Draw.MeasureText("abc", face: FontFace.Regular), Draw.MeasureText("abc", face: (FontFace)99)));

    [Fact]
    public void EachFaceMeasuresWithItsOwnFontAndSize() => TestFrame.Run(() =>
    {
        var (label, labelSize) = (Fonts.Of(FontFace.Label).Font, Fonts.Of(FontFace.Label).Size);
        try
        {
            Fonts.Of(FontFace.Label).Font = ImGui.GetFont();
            Fonts.Of(FontFace.Label).Size = 40;
            Assert.True(Draw.MeasureText("abc", face: FontFace.Label).Y > Draw.MeasureText("abc", face: FontFace.Regular).Y);
            Assert.Equal(Draw.MeasureText("abc", 40), Draw.MeasureText("abc", face: FontFace.Label));
        }
        finally
        {
            Fonts.Of(FontFace.Label).Font = label;
            Fonts.Of(FontFace.Label).Size = labelSize;
        }
    });

    [Fact]
    public void BackDrawsUnderMiddleAndTheScopePutsTheChannelBack() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        using (Draw.Layer(DrawLayer.Front)) Draw.Line(Vector2.Zero, new Vector2(5), Color.Red);
        Draw.ResetLayer();
        Draw.MergeLayers();
        var start = list.IdxBuffer.Size;
        Draw.SplitLayers();
        Assert.Equal((int)DrawLayer.Middle, list.Splitter.Current);
        Draw.RectFilled(Vector2.Zero, new Vector2(10), Color.Red);
        using (Draw.Layer(DrawLayer.Back))
        {
            Assert.Equal((int)DrawLayer.Back, list.Splitter.Current);
            Draw.RectFilled(Vector2.Zero, new Vector2(10), Color.Blue);
        }
        Assert.Equal((int)DrawLayer.Middle, list.Splitter.Current);
        Draw.MergeLayers();
        Assert.Equal(EColor.U32(Color.Blue), list.VtxBuffer[list.IdxBuffer[start]].Col);
        Assert.Equal(EColor.U32(Color.Red), list.VtxBuffer[list.IdxBuffer[start + 6]].Col);
    });

    [Theory]
    [InlineData(1, 40000, true)]
    [InlineData(300, 150, true)]
    [InlineData(1, 40000, false)]
    public void AShapeDrawnAfterAHugePolylineStillPointsAtItsOwnVertices(int lines, int points, bool layered) => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        Draw.ResetLayer();
        Draw.MergeLayers();
        if (layered) Draw.SplitLayers();
        var outline = new Vector2[points];
        for (var i = 0; i < points; i++) outline[i] = new Vector2(i % 1000, i / 1000 * 3 + i % 7);
        using (Draw.Layer(DrawLayer.Back))
            for (var l = 0; l < lines; l++) Draw.Polyline(outline, Color.White, 2, true);
        Draw.RectFilled(new Vector2(500), new Vector2(510), Color.Red);
        Draw.MergeLayers();
        var red = EColor.U32(Color.Red);
        var redCorners = 0;
        for (var c = 0; c < list.CmdBuffer.Size; c++)
        {
            var cmd = list.CmdBuffer[c];
            for (var i = 0; i < cmd.ElemCount; i++)
            {
                var vertex = (int)cmd.VtxOffset + list.IdxBuffer[(int)cmd.IdxOffset + i];
                if (vertex < list.VtxBuffer.Size && list.VtxBuffer[vertex].Col == red) redCorners++;
            }
        }
        Assert.True(list.VtxBuffer.Size > 65536, $"only {list.VtxBuffer.Size} vertices");
        Assert.Equal(6, redCorners);
    });

    [Fact]
    public void ResetLayerPutsAnAbandonedFrontChannelBackToMiddle() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        Draw.SplitLayers();
        list.ChannelsSetCurrent((int)DrawLayer.Front);
        Draw.ResetLayer();
        Assert.Equal((int)DrawLayer.Middle, list.Splitter.Current);
        Draw.MergeLayers();
        Assert.Equal(1, list.Splitter.Count);
    });

    [Fact]
    public void ImageQuadMapsTheSpriteCornersOntoTheFourPointsInOrder() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var texture = Texture();
        var start = list.VtxBuffer.Size;
        Draw.ImageQuad(Cell(texture), new Vector2(10, 20), new Vector2(50, 25), new Vector2(45, 70), new Vector2(5, 60), Color.Gold);
        Assert.Equal(4, list.VtxBuffer.Size - start);
        var expected = new[] { (new Vector2(10, 20), new Vector2(0.25f, 0.5f)), (new Vector2(50, 25), new Vector2(0.75f, 0.5f)), (new Vector2(45, 70), new Vector2(0.75f, 1)), (new Vector2(5, 60), new Vector2(0.25f, 1)) };
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].Item1, list.VtxBuffer[start + i].Pos);
            Assert.Equal(expected[i].Item2, list.VtxBuffer[start + i].Uv);
            Assert.Equal(EColor.U32(Color.Gold), list.VtxBuffer[start + i].Col);
        }
    });

    [Fact]
    public void ImageQuadWithoutATintIsWhite() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.ImageQuad(Unit(), Vector2.Zero, new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
        Assert.Equal(EColor.U32(Color.White), list.VtxBuffer[start].Col);
    });

    [Fact]
    public void ImageRotatedPutsTheQuadCornersAndUvsWhereTheConventionSays() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var texture = Texture();
        var start = list.VtxBuffer.Size;
        Draw.ImageRotated(Cell(texture), Centre, new Vector2(20), new Vector2(1, 0), Color.Gold);
        Assert.Equal(4, list.VtxBuffer.Size - start);
        var expected = new[] { (new Vector2(110, 60), new Vector2(0.25f, 0.5f)), (new Vector2(90, 60), new Vector2(0.75f, 0.5f)), (new Vector2(90, 40), new Vector2(0.75f, 1)), (new Vector2(110, 40), new Vector2(0.25f, 1)) };
        for (var i = 0; i < 4; i++)
        {
            var vertex = list.VtxBuffer[start + i];
            Assert.Equal(expected[i].Item1, vertex.Pos);
            Assert.Equal(expected[i].Item2, vertex.Uv);
            Assert.Equal(EColor.U32(Color.Gold), vertex.Col);
        }
        var before = list.VtxBuffer.Size;
        Draw.ImageRotated(new Sprite(texture, Vector2.Zero, Vector2.One), Centre, new Vector2(20), Vector2.Zero);
        Assert.Equal(before, list.VtxBuffer.Size);
    });

    [Fact]
    public void TextOutlinedDrawsEightRingCopiesThenTheText() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var singleStart = list.VtxBuffer.Size;
        Draw.Text("A", new Vector2(10, 10), Color.White);
        var single = list.VtxBuffer.Size - singleStart;
        Assert.True(single > 0);

        var outlinedStart = list.VtxBuffer.Size;
        Draw.TextOutlined("A", new Vector2(10, 10), Color.White, Color.Black, 2);
        Assert.Equal(single * 9, list.VtxBuffer.Size - outlinedStart);
        Assert.Equal(EColor.U32(Color.Black), list.VtxBuffer[outlinedStart].Col);
        Assert.Equal(EColor.U32(Color.White), list.VtxBuffer[list.VtxBuffer.Size - 1].Col);
    });

    [Fact]
    public void TextAlignmentMovesTheDrawnGlyphsByTheMeasuredOffset() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var size = Draw.MeasureText("A");
        var leftStart = list.VtxBuffer.Size;
        Draw.Text("A", new Vector2(50, 50), Color.White);
        var rightStart = list.VtxBuffer.Size;
        Draw.Text("A", new Vector2(50, 50), Color.White, Align.Right | Align.Bottom);
        Assert.Equal(list.VtxBuffer[leftStart].Pos - size, list.VtxBuffer[rightStart].Pos);
    });

    [Fact]
    public void ClipScopePopsTheClipRectOnDispose() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var top = () => list.ClipRectStack[list.ClipRectStack.Size - 1];
        var outer = top();
        using (Draw.Clip(new Vector2(10, 10), new Vector2(50, 50))) Assert.Equal(new Vector4(10, 10, 50, 50), top());
        Assert.Equal(outer, top());
    });

    static float Area(IEnumerable<(Vector2 Min, Vector2 Max)> strips) => strips.Sum(s => (s.Max.X - s.Min.X) * (s.Max.Y - s.Min.Y));

    static bool Overlaps((Vector2 Min, Vector2 Max) a, (Vector2 Min, Vector2 Max) b) => a.Min.X < b.Max.X && b.Min.X < a.Max.X && a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;

    [Fact]
    public void StripsWithoutRectsAreTheWholeScreen() =>
        Assert.Equal([(Vector2.Zero, new Vector2(100, 50))], Draw.Strips(new Vector2(100, 50), []));

    [Fact]
    public void StripsAroundOneRectCoverEverythingElse()
    {
        var hole = (new Vector2(10, 10), new Vector2(30, 20));
        var strips = Draw.Strips(new Vector2(100, 50), [hole]);
        Assert.Equal(5000 - 200, Area(strips));
        Assert.DoesNotContain(strips, s => Overlaps(s, hole));
    }

    [Fact]
    public void StripsAroundTwoRectsSkipBoth()
    {
        var holes = new[] { (new Vector2(10, 10), new Vector2(30, 20)), (new Vector2(60, 0), new Vector2(80, 50)) };
        var strips = Draw.Strips(new Vector2(100, 50), holes);
        Assert.Equal(5000 - 200 - 1000, Area(strips));
        Assert.DoesNotContain(strips, s => holes.Any(h => Overlaps(s, h)));
    }

    [Fact]
    public void StripsAroundOverlappingRectsDoNotDoubleCount()
    {
        var holes = new[] { (new Vector2(10, 10), new Vector2(30, 30)), (new Vector2(20, 20), new Vector2(40, 40)) };
        Assert.Equal(5000 - 400 - 400 + 100, Area(Draw.Strips(new Vector2(100, 50), holes)));
    }

    [Fact]
    public void StripsHangingOffTheScreenAreCutToIt() =>
        Assert.Equal(5000 - 10 * 50, Area(Draw.Strips(new Vector2(100, 50), [(new Vector2(-20, -5), new Vector2(10, 60))])));

    [Fact]
    public void StripsAreEmptyWhenARectCoversTheScreen() =>
        Assert.Empty(Draw.Strips(new Vector2(100, 50), [(new Vector2(-1), new Vector2(200))]));

    [Fact]
    public void StripsIgnoreRectsOffTheScreen() =>
        Assert.Equal([(Vector2.Zero, new Vector2(100, 50))], Draw.Strips(new Vector2(100, 50), [(new Vector2(200), new Vector2(300))]));

    [Fact]
    public void ExcludingRunsTheDrawOncePerStripInsideThatStripsClip() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var top = () => list.ClipRectStack[list.ClipRectStack.Size - 1];
        var outer = top();
        var hole = (new Vector2(10, 10), new Vector2(30, 20));
        var expected = Draw.Strips(ImGui.GetIO().DisplaySize, [hole]);
        var seen = new List<Vector4>();
        Draw.Excluding([hole], () => seen.Add(top()));
        Assert.Equal(expected.Select(s => new Vector4(s.Min.X, s.Min.Y, s.Max.X, s.Max.Y)), seen);
        Assert.Equal(outer, top());
    });

    [Fact]
    public void ExcludingWithNoRectsDrawsOnceUnclipped() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var outer = list.ClipRectStack[list.ClipRectStack.Size - 1];
        var calls = 0;
        Draw.Excluding([], () =>
        {
            calls++;
            Assert.Equal(outer, list.ClipRectStack[list.ClipRectStack.Size - 1]);
        });
        Assert.Equal(1, calls);
    });

    [Fact]
    public void ExcludingKeepsDrawsOutOfTheRect() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var hole = (new Vector2(10, 10), new Vector2(30, 20));
        var start = list.CmdBuffer.Size;
        var firstIndex = list.IdxBuffer.Size;
        Draw.Excluding([hole], () => Draw.RectFilled(Vector2.Zero, new Vector2(100, 50), Color.Red));
        var drawn = Enumerable.Range(start - 1, list.CmdBuffer.Size - start + 1).Select(i => list.CmdBuffer[i]).Where(cmd => cmd.ElemCount > 0 && cmd.IdxOffset >= firstIndex).ToList();
        Assert.NotEmpty(drawn);
        Assert.DoesNotContain(drawn, cmd => Overlaps((new Vector2(cmd.ClipRect.X, cmd.ClipRect.Y), new Vector2(cmd.ClipRect.Z, cmd.ClipRect.W)), hole));
    });

    [Fact]
    public void PolylineAndConvexPolySkipDegenerateInput() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Polyline([new Vector2(1, 1)], Color.Red);
        Draw.ConvexPolyFilled([new Vector2(1, 1), new Vector2(2, 2)], Color.Red);
        Assert.Equal(start, list.VtxBuffer.Size);
        Draw.Polyline([Vector2.Zero, new Vector2(40, 0)], Color.Red);
        Assert.True(list.VtxBuffer.Size > start);
    });

    [Fact]
    public void StarPointsStartAtTheTopAndAlternateOuterAndInner()
    {
        Span<Vector2> star = stackalloc Vector2[8];
        Draw.StarPoints(star, Centre, 10, 4);
        Assert.Equal(new Vector2(100, 40), star[0]);
        Assert.True(Vector2.Distance(new Vector2(110, 50), star[2]) < 1e-3f);
        for (var i = 1; i < star.Length; i += 2) Assert.Equal(4, Vector2.Distance(Centre, star[i]), 3);
    }

    [Fact]
    public void RowWithASwatchDrawsMoreVerticesAndTheSwatchColour() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var style = TextStyle.Hud();
        var plain = list.VtxBuffer.Size;
        Draw.Row(Centre, 200, "Name", "12", style);
        var withoutSwatch = list.VtxBuffer.Size - plain;
        var start = list.VtxBuffer.Size;
        Draw.Row(Centre, 200, "Name", "12", style, Color.Red);
        Assert.Equal(withoutSwatch + 4, list.VtxBuffer.Size - start);
        Assert.Equal(EColor.U32(Color.Red), list.VtxBuffer[start].Col);
    });

    [Fact]
    public void DimmedRowFadesTheSwatch() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Row(Centre, 200, "Name", "12", TextStyle.Hud(), Color.FromArgb(200, 255, 0, 0), dimmed: true);
        Assert.Equal(EColor.U32(Color.FromArgb(90, 255, 0, 0)), list.VtxBuffer[start].Col);
    });

    [Fact]
    public void RowReturnsTheAdvanceTheLegendSizesFrom() => TestFrame.Run(() =>
    {
        var style = TextStyle.Hud();
        Assert.Equal(Draw.RowHeight(style), Draw.Row(Centre, 200, "Name", "12", style));
        Assert.True(Draw.RowHeight(style) > Draw.MeasureText("Ag", style).Y);
    });

    [Theory]
    [InlineData("", "", 200f)]
    [InlineData("Name", "12", 0f)]
    [InlineData("Name", "12", -5f)]
    public void RowSurvivesEmptyTextAndTinyWidth(string left, string right, float width) => TestFrame.Run(() =>
        Draw.Row(Centre, width, left, right, TextStyle.Hud(), Color.Blue, true));

    [Fact]
    public void GlowIsSolidInsideTheInnerRadiusAndFadesToClearAtTheEdge() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        var colour = Color.FromArgb(100, 200, 150, 50);
        Draw.Glow(Centre, 20, colour, 8);
        Assert.Equal(GlowVertices, list.VtxBuffer.Size - start);
        Assert.Equal(Centre, list.VtxBuffer[start].Pos);
        Assert.Equal(EColor.U32(colour), list.VtxBuffer[start].Col);
        Assert.Equal(new Vector2(108, 50), list.VtxBuffer[start + 1].Pos);
        Assert.Equal(EColor.U32(colour), list.VtxBuffer[start + 1].Col);
        Assert.Equal(new Vector2(120, 50), list.VtxBuffer[start + 2].Pos);
        Assert.Equal(EColor.U32(Color.FromArgb(0, colour)), list.VtxBuffer[start + 2].Col);
        var before = list.VtxBuffer.Size;
        Draw.Glow(Centre, 0, colour);
        Draw.Glow(Centre, 20, Color.FromArgb(0, colour));
        Assert.Equal(before, list.VtxBuffer.Size);
    });

    [Fact]
    public void GlowRingsSitOnTheUnitCircleAtInnerAndOuterRadius() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Glow(Centre, 12, Color.Gold, 4);
        Assert.Equal(Centre, list.VtxBuffer[start].Pos);
        for (var i = 0; i < Draw.GlowSegments; i++)
        {
            var direction = new Vector2(MathF.Cos(i * MathF.Tau / Draw.GlowSegments), MathF.Sin(i * MathF.Tau / Draw.GlowSegments));
            Assert.True(Vector2.Distance(Centre + direction * 4, list.VtxBuffer[start + 1 + i * 2].Pos) < 1e-3f);
            Assert.True(Vector2.Distance(Centre + direction * 12, list.VtxBuffer[start + 2 + i * 2].Pos) < 1e-3f);
        }
    });

    [Fact]
    public void GlowRectIsTheBorderPlusOneFadingRingPerTwoPixelsOfSpread() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var min = new Vector2(10, 10);
        var max = new Vector2(60, 90);
        var start = list.VtxBuffer.Size;
        Draw.Rect(min, max, Color.Gold, 3);
        var border = list.VtxBuffer.Size - start;
        start = list.VtxBuffer.Size;
        Draw.Rect(min, max, Color.Gold, 2);
        var ring = list.VtxBuffer.Size - start;

        start = list.VtxBuffer.Size;
        Draw.GlowRect(min, max, Color.Gold, 3, 0);
        Assert.Equal(border, list.VtxBuffer.Size - start);

        start = list.VtxBuffer.Size;
        Draw.GlowRect(min, max, Color.Gold, 3, 8);
        Assert.Equal(border + 4 * ring, list.VtxBuffer.Size - start);

        start = list.VtxBuffer.Size;
        Draw.GlowRect(min, max, Color.FromArgb(0, Color.Gold), 3, 8);
        Draw.GlowRect(min, max, Color.Gold, 0, 8);
        Assert.Equal(start, list.VtxBuffer.Size);
    });

    [Fact]
    public void ADotMarkerFillsItsRadiusAndGlowOnlyAddsTheHalo() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Marker(Centre, 6, Color.Red);
        var plain = list.VtxBuffer.Size - start;
        var furthest = 0f;
        for (var i = start; i < start + plain; i++)
            if (list.VtxBuffer[i].Col == EColor.U32(Color.Red)) furthest = MathF.Max(furthest, Vector2.Distance(Centre, list.VtxBuffer[i].Pos));
        Assert.InRange(furthest, 5.4f, 6f);

        var glowing = list.VtxBuffer.Size;
        Draw.Marker(Centre, 6, Color.Red, glow: 1);
        Assert.Equal(plain + GlowVertices, list.VtxBuffer.Size - glowing);
        Assert.Equal(EColor.U32(Color.FromArgb(51, Color.Red)), list.VtxBuffer[glowing].Col);
        Assert.Equal(6 * 2.2f, Vector2.Distance(Centre, list.VtxBuffer[glowing + 2].Pos), 3);

        var none = list.VtxBuffer.Size;
        Draw.Marker(Centre, 0, Color.Red, glow: 1);
        Assert.Equal(none, list.VtxBuffer.Size);
    });

    [Fact]
    public void ABadgeMarkerIsADarkDiscWithAColouredRingAndStar() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Red, MarkerShape.Star);
        var coloured = new List<Vector2>();
        for (var i = start; i < list.VtxBuffer.Size; i++)
            if (list.VtxBuffer[i].Col == EColor.U32(Color.Red)) coloured.Add(list.VtxBuffer[i].Pos);
        Assert.Equal(EColor.U32(Color.FromArgb(0x14, 0x16, 0x1A)), list.VtxBuffer[start].Col);
        Assert.Contains(coloured, p => Vector2.Distance(Centre, p) < 1f);
        Assert.Contains(coloured, p => MathF.Abs(p.X - 100) < 1f && p.Y < 47 && p.Y > 44);
        Assert.All(coloured, p => Assert.True(Vector2.Distance(Centre, p) <= 8.01f));
    });

    [Theory]
    [InlineData("marker:Star", true, MarkerShape.Star)]
    [InlineData("marker:Diamond", true, MarkerShape.Diamond)]
    [InlineData("marker:Nope", false, MarkerShape.Dot)]
    [InlineData("marker:99", false, MarkerShape.Dot)]
    [InlineData("door", false, MarkerShape.Dot)]
    [InlineData(null, false, MarkerShape.Dot)]
    public void TryMarkerReadsOnlyPrefixedShapeNames(string? icon, bool ok, MarkerShape shape)
    {
        Assert.Equal(ok, Draw.TryMarker(icon, out var parsed));
        Assert.Equal(shape, parsed);
    }

    [Fact]
    public void RegularPointsStartAtTheGivenAngle()
    {
        Span<Vector2> square = stackalloc Vector2[4];
        Draw.RegularPoints(square, Vector2.Zero, 10, 0);
        Assert.Equal(new Vector2(10, 0), square[0]);
        Assert.True(Vector2.Distance(new Vector2(0, 10), square[1]) < 1e-4f);
    }

    [Fact]
    public void AnIconWithoutAKnownPrefixFallsBackToADot() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var marker = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Gold, MarkerShape.Star);
        var star = list.VtxBuffer.Size - marker;
        var named = list.VtxBuffer.Size;
        Draw.Icon("marker:Star", Centre, 8, Color.Gold);
        Assert.Equal(star, list.VtxBuffer.Size - named);

        var dot = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Gold);
        var dotCount = list.VtxBuffer.Size - dot;
        var missing = list.VtxBuffer.Size;
        Draw.Icon("not-loaded", Centre, 8, Color.Gold);
        Draw.Icon("door", Centre, 8, Color.Gold);
        Draw.Icon(null, Centre, 8, Color.Gold);
        Assert.Equal(dotCount * 3, list.VtxBuffer.Size - missing);
    });

    [Fact]
    public void APolygonMarkerFillsItsCornersInTheColourInsideTheRadius() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Red, MarkerShape.Diamond);
        var coloured = new List<Vector2>();
        for (var i = start; i < list.VtxBuffer.Size; i++)
            if (list.VtxBuffer[i].Col == EColor.U32(Color.Red)) coloured.Add(list.VtxBuffer[i].Pos);
        Assert.NotEmpty(coloured);
        Assert.All(coloured, p => Assert.True(Vector2.Distance(Centre, p) <= 8.01f));
        Assert.Contains(coloured, p => MathF.Abs(p.X - 100) < 0.5f && p.Y < 44);
    });

    static ImTextureRef Texture(ulong id = 1ul) => new() { TexID = new ImTextureID(id) };

    static Sprite Cell(ImTextureRef texture) => new(texture, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1));

    static Sprite Unit(ulong id = 1ul) => new(Texture(id), Vector2.Zero, Vector2.One);

    static readonly Sprite Ready = new(Texture(), new Vector2(0.25f), new Vector2(0.5f));

    [Fact]
    public void TryGameIconSaysNoToAnEmptyNameWithoutAskingTheHost()
    {
        var asked = 0;
        try
        {
            TestGame.UseGameIcons(_ =>
            {
                asked++;
                return Ready;
            });
            Assert.False(Draw.TryGameIcon("", out _));
            Assert.False(Draw.TryGameIcon(default, out _));
            Assert.Equal(0, asked);
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Fact]
    public void TryGameIconAsksTheHostAndSaysNoWhenItHasNothing()
    {
        try
        {
            TestGame.UseGameIcons(null);
            Assert.False(Draw.TryGameIcon("UI_MiniMap_Portal", out _));

            TestGame.UseGameIcons(name => name == "UI_MiniMap_Portal" ? Ready : null);
            Assert.True(Draw.TryGameIcon("UI_MiniMap_Portal", out var sprite));
            Assert.Equal(Ready, sprite);
            Assert.False(Draw.TryGameIcon("UI_MiniMap_Other", out _));
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Fact]
    public void TryGameIconIsPublicAndTheSpanOverloadIsTheOnlyOne() =>
        Assert.Equal(typeof(ReadOnlySpan<char>), typeof(Draw).GetMethods().Single(m => m.Name == "TryGameIcon").GetParameters()[0].ParameterType);

    [Fact]
    public void TryGameIconTakesASliceOfALongerKeyWithoutACopy()
    {
        try
        {
            TestGame.UseGameIcons(name => name == "UI_MiniMap_Portal" ? Ready : null);
            var key = "game:UI_MiniMap_Portal";
            Assert.True(Draw.TryGameIcon(key.AsSpan(Draw.GameIconPrefix.Length), out var sprite));
            Assert.Equal(Ready, sprite);
            Assert.False(Draw.TryGameIcon(key.AsSpan(0, Draw.GameIconPrefix.Length), out _));
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Fact]
    public void AGameIconDrawsTheResolvedSpriteTintedAndFallsBackToADotUntilItIsReady() => TestFrame.Run(() =>
    {
        try
        {
            var list = Draw.Background;
            TestGame.UseGameIcons(name => name == "UI_MiniMap_Portal" ? Ready : null);
            var start = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold);
            Assert.Equal(4, list.VtxBuffer.Size - start);
            Assert.Equal(new Vector2(92, 42), list.VtxBuffer[start].Pos);
            Assert.Equal(new Vector2(0.25f), list.VtxBuffer[start].Uv);
            Assert.Equal(EColor.U32(Color.Gold), list.VtxBuffer[start].Col);

            var glowing = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold, 1);
            Assert.Equal(GlowVertices + 4, list.VtxBuffer.Size - glowing);

            var dot = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold);
            var dotCount = list.VtxBuffer.Size - dot;
            var missing = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_NotLoaded", Centre, 8, Color.Gold);
            Draw.Icon("game:", Centre, 8, Color.Gold);
            TestGame.UseGameIcons(null);
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold);
            Assert.Equal(dotCount * 3, list.VtxBuffer.Size - missing);
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    });

    [Fact]
    public void GameIconsDoNotDisturbMarkerKeysOrPlainNames() => TestFrame.Run(() =>
    {
        try
        {
            var list = Draw.Background;
            TestGame.UseGameIcons(_ => Ready);
            var dot = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold);
            var dotCount = list.VtxBuffer.Size - dot;
            var plain = list.VtxBuffer.Size;
            Draw.Icon("door", Centre, 8, Color.Gold);
            Assert.Equal(dotCount, list.VtxBuffer.Size - plain);
            var marker = list.VtxBuffer.Size;
            Draw.Icon("marker:Star", Centre, 8, Color.Gold);
            var star = list.VtxBuffer.Size - marker;
            var expected = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold, MarkerShape.Star);
            Assert.Equal(list.VtxBuffer.Size - expected, star);
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    });

    static readonly Sprite Whole = new(Texture(2ul), Vector2.Zero, Vector2.One);

    [Fact]
    public void AnSvgIconAsksTheHostWithTheFullKeyItsScreenSizeAndBothColours() => TestFrame.Run(() =>
    {
        var asked = new List<(string Icon, int Pixels, Color Colour, Color Accent)>();
        try
        {
            TestGame.UseSvgIcons((icon, pixels, colour, accent) =>
            {
                asked.Add((icon, pixels, colour, accent));
                return null;
            });
            Draw.Icon("svg:quest", Centre, 7.2f, Color.Gold, accent: Color.Navy);
            Draw.Icon("svg:Plugin/ring", Centre, 8, Color.Gold);

            Assert.Equal(("svg:quest", 15, Color.Gold, Color.Navy), asked[0]);
            Assert.Equal("svg:Plugin/ring", asked[1].Icon);
            Assert.Equal(16, asked[1].Pixels);
            Assert.Equal(GameColors.MinimapIconAccent.ToArgb(), asked[1].Accent.ToArgb());
            Assert.Equal(0x1A1A1A, GameColors.MinimapIconAccent.ToArgb() & 0xFFFFFF);
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    });

    [Fact]
    public void AReadySvgIconIsOneQuadTintedOnlyByAlphaAndADotUntilThen() => TestFrame.Run(() =>
    {
        try
        {
            var list = Draw.Background;
            TestGame.UseSvgIcons((_, _, _, _) => Whole);
            var start = list.VtxBuffer.Size;
            Draw.Icon("svg:quest", Centre, 8, Color.FromArgb(128, Color.Gold));
            Assert.Equal(4, list.VtxBuffer.Size - start);
            Assert.Equal(new Vector2(92, 42), list.VtxBuffer[start].Pos);
            Assert.Equal(EColor.U32(Color.FromArgb(128, Color.White)), list.VtxBuffer[start].Col);

            var glowing = list.VtxBuffer.Size;
            Draw.Icon("svg:quest", Centre, 8, Color.Gold, 1);
            Assert.Equal(GlowVertices + 4, list.VtxBuffer.Size - glowing);

            var dot = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold);
            var dotCount = list.VtxBuffer.Size - dot;
            var missing = list.VtxBuffer.Size;
            TestGame.UseSvgIcons((_, _, _, _) => null);
            Draw.Icon("svg:quest", Centre, 8, Color.Gold);
            TestGame.UseSvgIcons(null);
            Draw.Icon("svg:quest", Centre, 8, Color.Gold);
            Assert.Equal(dotCount * 2, list.VtxBuffer.Size - missing);
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    });

    [Fact]
    public void ABadRadiusNeverAsksForAnSvgIconAndDoesNotThrow() => TestFrame.Run(() =>
    {
        var calls = 0;
        try
        {
            TestGame.UseSvgIcons((_, _, _, _) =>
            {
                calls++;
                return Whole;
            });
            foreach (var radius in new[] { 0f, -3f, float.NaN, float.PositiveInfinity }) Draw.Icon("svg:quest", Centre, radius, Color.Gold);
            Assert.Equal(0, calls);
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    });

    static (Vector2 Up, Vector2 TopLeft, Vector2 TopRight, int Count) DrawnFacing(Action draw)
    {
        var result = default((Vector2, Vector2, Vector2, int));
        TestFrame.Run(() =>
        {
            var list = Draw.Background;
            var start = list.VtxBuffer.Size;
            draw();
            var count = list.VtxBuffer.Size - start;
            var top = Vector2.Zero;
            Vector2 topLeft = default, topRight = default;
            for (var i = start; i < list.VtxBuffer.Size; i++)
            {
                var vertex = list.VtxBuffer[i];
                if (vertex.Uv.Y != 0) continue;
                top += (vertex.Pos - Centre) / 2;
                if (vertex.Uv.X == 0) topLeft = vertex.Pos;
                else topRight = vertex.Pos;
            }
            result = (top, topLeft, topRight, count);
        });
        return result;
    }

    [Fact]
    public void FacingTurnsTheIconsUpToPointAlongIt()
    {
        try
        {
            TestGame.UseSvgIcons((_, _, _, _) => Whole);

            var right = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, facing: new Vector2(1, 0)));
            Assert.Equal(4, right.Count);
            Assert.True(right.Up.X > 9 && MathF.Abs(right.Up.Y) < 0.01f, $"top edge on the right {right.Up}");
            Assert.True(right.TopLeft.Y < right.TopRight.Y, "turned clockwise, not mirrored");

            var up = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, facing: new Vector2(0, -3)));
            Assert.True(up.Up.Y < -9 && MathF.Abs(up.Up.X) < 0.01f, $"top edge above {up.Up}");
            Assert.True(up.TopLeft.X < up.TopRight.X, "upright, not mirrored");
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }

    [Fact]
    public void FacingWorksForGameSpritesToo()
    {
        try
        {
            TestGame.UseGameIcons(_ => Whole);
            var game = DrawnFacing(() => Draw.Icon("game:UI_MiniMap_Player1", Centre, 10, Color.White, facing: new Vector2(1, 0)));
            Assert.True(game.Up.X > 9, $"game sprite {game.Up}");
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Fact]
    public void AZeroOrBrokenFacingDrawsTheIconUpright()
    {
        try
        {
            TestGame.UseSvgIcons((_, _, _, _) => Whole);
            foreach (var facing in new[] { Vector2.Zero, new Vector2(float.NaN, 1), new Vector2(1, float.PositiveInfinity), new Vector2(1e-4f, 0) })
            {
                var drawn = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, facing: facing));
                Assert.Equal(4, drawn.Count);
                Assert.True(drawn.Up.Y < -9, $"facing {facing} -> {drawn.Up}");
                Assert.Equal(new Vector2(90, 40), drawn.TopLeft);
            }
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }

    [Fact]
    public void MeasureTextWithAStyleMatchesWhatTextReturns()
    {
        var style = new TextStyle { Scale = 1.5f, Face = FontFace.Bold };
        Vector2 measured = default, drawn = default;
        TestFrame.Run(() =>
        {
            measured = Draw.MeasureText("Monster level 78", style);
            drawn = Draw.Text("Monster level 78", new Vector2(10, 10), style, Align.TopLeft, Draw.Foreground);
        });
        Assert.Equal(drawn, measured);
        Assert.True(measured.X > 0);
    }

    [Fact]
    public void RectGradientAddsOneQuad()
    {
        var added = 0;
        TestFrame.Run(() =>
        {
            var list = Draw.Foreground;
            var before = list.VtxBuffer.Size;
            Draw.RectGradient(new Vector2(0, 0), new Vector2(100, 2), Color.Gold, Color.Transparent, list);
            added = list.VtxBuffer.Size - before;
        });
        Assert.Equal(4, added);
    }

    [Fact]
    public void BarFillsFromTheLeftAndClampsTheFraction()
    {
        var widths = new List<float>();
        TestFrame.Run(() =>
        {
            var list = Draw.Foreground;
            foreach (var fraction in new[] { 0.5f, float.NaN, -1f, 2f })
            {
                var before = list.VtxBuffer.Size;
                Draw.Bar(new Vector2(10, 0), new Vector2(110, 10), fraction, Color.Gold, Color.Black, list);
                Assert.Equal(8, list.VtxBuffer.Size - before);
                var fill = Enumerable.Range(before + 4, 4).Select(v => list.VtxBuffer[v].Pos.X).ToList();
                Assert.Equal(10, fill.Min());
                widths.Add(fill.Max() - fill.Min());
            }
        });
        Assert.Equal([50f, 0f, 0f, 100f], widths);
    }

    [Fact]
    public void SegmentedBarLightsTheFirstSegmentsOnAGradientAndWraps()
    {
        Vector2 size = default;
        var xs = new List<float>();
        var ys = new List<float>();
        var colours = new List<uint>();
        TestFrame.Run(() =>
        {
            var list = Draw.Foreground;
            var before = list.VtxBuffer.Size;
            size = Draw.SegmentedBar(new Vector2(10, 20), 5, 2, new Vector2(4, 6), 1, 3, [Color.Lime, Color.Red], Color.Gray, list);
            Assert.Equal(5 * 4, list.VtxBuffer.Size - before);
            for (var i = 0; i < 5; i++)
            {
                var corner = list.VtxBuffer[before + i * 4];
                xs.Add(corner.Pos.X);
                ys.Add(corner.Pos.Y);
                colours.Add(corner.Col);
            }
        });
        Assert.Equal([10f, 15f, 20f, 10f, 15f], xs);
        Assert.Equal([20f, 20f, 20f, 27f, 27f], ys);
        Assert.Equal(EColor.U32(Color.Lime), colours[0]);
        Assert.NotEqual(EColor.U32(Color.Lime), colours[1]);
        Assert.NotEqual(EColor.U32(Color.Gray), colours[1]);
        Assert.Equal([EColor.U32(Color.Gray)], colours.Skip(2).Distinct());
        Assert.Equal(new Vector2(14, 13), size);
    }

    [Fact]
    public void SegmentedBarWithNothingToDrawIsEmpty() => TestFrame.Run(() =>
    {
        var list = Draw.Foreground;
        var before = list.VtxBuffer.Size;
        Assert.Equal(Vector2.Zero, Draw.SegmentedBar(Vector2.Zero, 0, 0, new Vector2(4, 6), 1, 0, [Color.Lime, Color.Red], Color.Gray, list));
        Assert.Equal(0, list.VtxBuffer.Size - before);
    });

    [Fact]
    public void TheIconCacheKeepsOneEntryPerStringAndStaysBounded() => TestFrame.Run(() =>
    {
        Draw.ParsedIcons.Clear();
        for (var i = 0; i < 3; i++) Draw.Icon("marker:Star", Centre, 6, Color.Gold);
        Draw.Icon("door", Centre, 6, Color.Gold);
        Draw.Icon(null, Centre, 6, Color.Gold);
        Assert.Equal(2, Draw.ParsedIcons.Count);
        Assert.Equal(MarkerShape.Star, Draw.ParsedIcons["marker:Star"].Shape);

        for (var i = 0; i < 1000; i++) Draw.Icon($"marker:Nope{i}", Centre, 6, Color.Gold);
        Assert.InRange(Draw.ParsedIcons.Count, 1, 256);
        Draw.ParsedIcons.Clear();
    });
}
