using System.Collections.Generic;
using System.Drawing;
using Aember.Testing;
using Aember.Ui;
using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

public sealed class DrawTests
{
    static readonly Vector2 Centre = new(100, 50);

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
    public void ArrowHeadSitsBehindTheTipOnBothSides()
    {
        Assert.Equal((new Vector2(6, 2), new Vector2(6, -2)), Draw.ArrowHead(Vector2.Zero, new Vector2(10, 0), 4));
        Assert.Null(Draw.ArrowHead(new Vector2(3, 3), new Vector2(3, 3), 4));
    }

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
        var sprite = new Sprite(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One);
        Assert.Equal(Draw.MeasureText("hello"), Draw.Text("hello", new Vector2(10, 10), Color.White));
        Assert.True(Draw.MeasureText("abc", 26).Y > Draw.MeasureText("abc", 13).Y);
        Assert.True(Draw.MeasureText("abc", face: FontFace.Mono).X > 0);
        Draw.TextOutlined("outlined", new Vector2(10, 30), Color.White, Color.Black, 2, Align.Center | Align.Middle);
        Draw.TextShadow("shadow", new Vector2(10, 50), Color.White, Color.Black);
        Assert.Equal(Draw.MeasureText("box") + new Vector2(8, 4), Draw.TextBackground("box", new Vector2(10, 70), Color.White, Color.DarkSlateGray, rounding: 3));
        Draw.Line(Vector2.Zero, new Vector2(50), Color.Red, 2);
        Draw.Polyline([new Vector2(0, 0), new Vector2(5, 5), new Vector2(9, 0)], Color.Red, closed: true);
        Draw.Polyline([new Vector2(1, 1)], Color.Red);
        Draw.Arrow(Vector2.Zero, new Vector2(40, 0), Color.Red);
        Draw.Arrow(Vector2.Zero, Vector2.Zero, Color.Red);
        Draw.Rect(Vector2.Zero, new Vector2(20), Color.Red, 1, 2);
        Draw.RectFilled(Vector2.Zero, new Vector2(20), Color.Red, 2);
        Draw.Circle(new Vector2(30), 5, Color.Red);
        Draw.CircleFilled(new Vector2(30), 5, Color.Red, 12);
        Draw.Triangle(Vector2.Zero, new Vector2(5, 0), new Vector2(0, 5), Color.Red);
        Draw.TriangleFilled(Vector2.Zero, new Vector2(5, 0), new Vector2(0, 5), Color.Red);
        Draw.Quad(Vector2.Zero, new Vector2(5, 0), new Vector2(5), new Vector2(0, 5), Color.Red);
        Draw.QuadFilled(Vector2.Zero, new Vector2(5, 0), new Vector2(5), new Vector2(0, 5), Color.Red);
        Draw.ConvexPolyFilled([new Vector2(0, 0), new Vector2(5, 0), new Vector2(5, 5)], Color.Red);
        Draw.Glow(Centre, 12, Color.Gold, 4);
        Draw.Marker(Centre, 5, Color.Red, MarkerShape.Dot, 1);
        Draw.Marker(Centre, 6, Color.Red, MarkerShape.Star, 1);
        foreach (var shape in Enum.GetValues<MarkerShape>()) Draw.Marker(Centre, 6, Color.Red, shape, 1);
        Draw.Icon("marker:Hexagon", Centre, 6, Color.Red, null, 1);
        Draw.Image(sprite, Vector2.Zero, new Vector2(16), list: Draw.Foreground);
        Draw.ImageRotated(sprite, Centre, new Vector2(16), new Vector2(0, -1), Color.Gold);
        Draw.ImageRotated(sprite, Centre, new Vector2(16), Vector2.Zero);
        using (Draw.Clip(Vector2.Zero, new Vector2(50))) Draw.Line(Vector2.Zero, new Vector2(100), Color.Red);
    });

    [Fact]
    public void FontScopesSetTheSizeAndPopCleanly() => TestFrame.Run(() =>
    {
        using (Fonts.UseMono(20)) Assert.Equal(20f, ImGui.GetFontSize());
        using (Fonts.UseRegular(18)) Assert.Equal(18f, ImGui.GetFontSize());
        using (Fonts.UseBold(19)) Assert.Equal(19f, ImGui.GetFontSize());
        using (Fonts.UseLabel(21)) Assert.Equal(21f, ImGui.GetFontSize());
    });

    [Fact]
    public void EachFaceMeasuresWithItsOwnFontAndSize() => TestFrame.Run(() =>
    {
        var (label, labelSize) = (Fonts.Label, Fonts.LabelSize);
        try
        {
            Fonts.Label = ImGui.GetFont();
            Fonts.LabelSize = 40;
            Assert.True(Draw.MeasureText("abc", face: FontFace.Label).Y > Draw.MeasureText("abc", face: FontFace.Regular).Y);
            Assert.Equal(Draw.MeasureText("abc", 40), Draw.MeasureText("abc", face: FontFace.Label));
        }
        finally
        {
            Fonts.Label = label;
            Fonts.LabelSize = labelSize;
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
        var texture = new ImTextureRef { TexID = new ImTextureID(1ul) };
        var start = list.VtxBuffer.Size;
        Draw.ImageQuad(new Sprite(texture, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1)), new Vector2(10, 20), new Vector2(50, 25), new Vector2(45, 70), new Vector2(5, 60), Color.Gold);
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
        Draw.ImageQuad(new Sprite(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One), Vector2.Zero, new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
        Assert.Equal(EColor.U32(Color.White), list.VtxBuffer[start].Col);
    });

    [Fact]
    public void ImageRotatedPutsTheQuadCornersAndUvsWhereTheConventionSays() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var texture = new ImTextureRef { TexID = new ImTextureID(1ul) };
        var start = list.VtxBuffer.Size;
        Draw.ImageRotated(new Sprite(texture, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1)), Centre, new Vector2(20), new Vector2(1, 0), Color.Gold);
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
    public void TextOutlinedDrawsEightRingCopiesThenTheTextAndShadowDrawsTwo() => TestFrame.Run(() =>
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

        var shadowStart = list.VtxBuffer.Size;
        Draw.TextShadow("A", new Vector2(10, 10), Color.White, Color.Black);
        Assert.Equal(single * 2, list.VtxBuffer.Size - shadowStart);
        Assert.Equal(list.VtxBuffer[shadowStart + single].Pos + Vector2.One, list.VtxBuffer[shadowStart].Pos);
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
    public void TextBackgroundFillsThePaddedBoxOnlyWhenTheBackgroundIsVisible() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.TextBackground("A", new Vector2(10, 10), Color.White, Color.FromArgb(0, 0, 0, 0));
        var withoutFill = list.VtxBuffer.Size - start;
        var filledStart = list.VtxBuffer.Size;
        Draw.TextBackground("A", new Vector2(10, 10), Color.White, Color.Red);
        Assert.Equal(withoutFill + 4, list.VtxBuffer.Size - filledStart);
        Assert.Equal(new Vector2(6, 8), list.VtxBuffer[filledStart].Pos);
        Assert.Equal(EColor.U32(Color.Red), list.VtxBuffer[filledStart].Col);
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

    [Fact]
    public void ArrowPolylineAndConvexPolySkipDegenerateInput() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        Draw.Arrow(Vector2.Zero, Vector2.Zero, Color.Red);
        Draw.Polyline([new Vector2(1, 1)], Color.Red);
        Draw.ConvexPolyFilled([new Vector2(1, 1), new Vector2(2, 2)], Color.Red);
        Assert.Equal(start, list.VtxBuffer.Size);
        Draw.Arrow(Vector2.Zero, new Vector2(40, 0), Color.Red);
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
    public void GlowIsSolidInsideTheInnerRadiusAndFadesToClearAtTheEdge() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var start = list.VtxBuffer.Size;
        var colour = Color.FromArgb(100, 200, 150, 50);
        Draw.Glow(Centre, 20, colour, 8);
        Assert.Equal(65, list.VtxBuffer.Size - start);
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
        Assert.Equal(plain + 65, list.VtxBuffer.Size - glowing);
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
    public void AnIconDrawsTheAtlasSpriteTintedAndFallsBackToADotWhenItIsMissing() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var atlas = new IconAtlas(new Dictionary<string, Sprite> { ["door"] = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.25f), new Vector2(0.5f)) });
        var start = list.VtxBuffer.Size;
        Draw.Icon("door", Centre, 8, Color.Gold, atlas);
        Assert.Equal(4, list.VtxBuffer.Size - start);
        Assert.Equal(new Vector2(92, 42), list.VtxBuffer[start].Pos);
        Assert.Equal(new Vector2(0.25f), list.VtxBuffer[start].Uv);
        Assert.Equal(EColor.U32(Color.Gold), list.VtxBuffer[start].Col);

        var glowing = list.VtxBuffer.Size;
        Draw.Icon("door", Centre, 8, Color.Gold, atlas, 1);
        Assert.Equal(65 + 4, list.VtxBuffer.Size - glowing);

        var marker = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Gold, MarkerShape.Star);
        var star = list.VtxBuffer.Size - marker;
        var named = list.VtxBuffer.Size;
        Draw.Icon("marker:Star", Centre, 8, Color.Gold, atlas);
        Assert.Equal(star, list.VtxBuffer.Size - named);

        var dot = list.VtxBuffer.Size;
        Draw.Marker(Centre, 8, Color.Gold);
        var dotCount = list.VtxBuffer.Size - dot;
        var missing = list.VtxBuffer.Size;
        Draw.Icon("not-loaded", Centre, 8, Color.Gold, atlas);
        Draw.Icon("door", Centre, 8, Color.Gold, null);
        Assert.Equal(dotCount * 2, list.VtxBuffer.Size - missing);
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

    static readonly Sprite Ready = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.25f), new Vector2(0.5f));

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
            Assert.False(Draw.TryGameIcon(null!, out _));
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
    public void AGameIconDrawsTheResolvedSpriteTintedAndFallsBackToADotUntilItIsReady() => TestFrame.Run(() =>
    {
        try
        {
            var list = Draw.Background;
            TestGame.UseGameIcons(name => name == "UI_MiniMap_Portal" ? Ready : null);
            var start = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold, null);
            Assert.Equal(4, list.VtxBuffer.Size - start);
            Assert.Equal(new Vector2(92, 42), list.VtxBuffer[start].Pos);
            Assert.Equal(new Vector2(0.25f), list.VtxBuffer[start].Uv);
            Assert.Equal(EColor.U32(Color.Gold), list.VtxBuffer[start].Col);

            var glowing = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold, null, 1);
            Assert.Equal(65 + 4, list.VtxBuffer.Size - glowing);

            var dot = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold);
            var dotCount = list.VtxBuffer.Size - dot;
            var missing = list.VtxBuffer.Size;
            Draw.Icon("game:UI_MiniMap_NotLoaded", Centre, 8, Color.Gold, null);
            Draw.Icon("game:", Centre, 8, Color.Gold, null);
            TestGame.UseGameIcons(null);
            Draw.Icon("game:UI_MiniMap_Portal", Centre, 8, Color.Gold, null);
            Assert.Equal(dotCount * 3, list.VtxBuffer.Size - missing);
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    });

    [Fact]
    public void GameIconsDoNotDisturbMarkerAndAtlasKeys() => TestFrame.Run(() =>
    {
        try
        {
            var list = Draw.Background;
            var atlas = new IconAtlas(new Dictionary<string, Sprite> { ["door"] = Ready });
            TestGame.UseGameIcons(_ => Ready);
            var start = list.VtxBuffer.Size;
            Draw.Icon("door", Centre, 8, Color.Gold, atlas);
            Assert.Equal(4, list.VtxBuffer.Size - start);
            var marker = list.VtxBuffer.Size;
            Draw.Icon("marker:Star", Centre, 8, Color.Gold, atlas);
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

    static readonly Sprite Whole = new(new ImTextureRef { TexID = new ImTextureID(2ul) }, Vector2.Zero, Vector2.One);

    static readonly Color DarkAccent = Color.FromArgb(0x1A, 0x1A, 0x1A);

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
            Draw.Icon("svg:quest", Centre, 7.2f, Color.Gold, null, accent: Color.Navy);
            Draw.Icon("svg:Plugin/ring", Centre, 8, Color.Gold, null);

            Assert.Equal(("svg:quest", 15, Color.Gold, Color.Navy), asked[0]);
            Assert.Equal("svg:Plugin/ring", asked[1].Icon);
            Assert.Equal(16, asked[1].Pixels);
            Assert.Equal(DarkAccent.ToArgb(), asked[1].Accent.ToArgb());
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
            Draw.Icon("svg:quest", Centre, 8, Color.FromArgb(128, Color.Gold), null);
            Assert.Equal(4, list.VtxBuffer.Size - start);
            Assert.Equal(new Vector2(92, 42), list.VtxBuffer[start].Pos);
            Assert.Equal(EColor.U32(Color.FromArgb(128, Color.White)), list.VtxBuffer[start].Col);

            var glowing = list.VtxBuffer.Size;
            Draw.Icon("svg:quest", Centre, 8, Color.Gold, null, 1);
            Assert.Equal(65 + 4, list.VtxBuffer.Size - glowing);

            var dot = list.VtxBuffer.Size;
            Draw.Marker(Centre, 8, Color.Gold);
            var dotCount = list.VtxBuffer.Size - dot;
            var missing = list.VtxBuffer.Size;
            TestGame.UseSvgIcons((_, _, _, _) => null);
            Draw.Icon("svg:quest", Centre, 8, Color.Gold, null);
            TestGame.UseSvgIcons(null);
            Draw.Icon("svg:quest", Centre, 8, Color.Gold, null);
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
            foreach (var radius in new[] { 0f, -3f, float.NaN, float.PositiveInfinity }) Draw.Icon("svg:quest", Centre, radius, Color.Gold, null);
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

            var right = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, null, facing: new Vector2(1, 0)));
            Assert.Equal(4, right.Count);
            Assert.True(right.Up.X > 9 && MathF.Abs(right.Up.Y) < 0.01f, $"top edge on the right {right.Up}");
            Assert.True(right.TopLeft.Y < right.TopRight.Y, "turned clockwise, not mirrored");

            var up = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, null, facing: new Vector2(0, -3)));
            Assert.True(up.Up.Y < -9 && MathF.Abs(up.Up.X) < 0.01f, $"top edge above {up.Up}");
            Assert.True(up.TopLeft.X < up.TopRight.X, "upright, not mirrored");
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }

    [Fact]
    public void FacingWorksForGameAndAtlasSpritesToo()
    {
        try
        {
            TestGame.UseGameIcons(_ => Whole);
            var game = DrawnFacing(() => Draw.Icon("game:UI_MiniMap_Player1", Centre, 10, Color.White, null, facing: new Vector2(1, 0)));
            Assert.True(game.Up.X > 9, $"game sprite {game.Up}");

            var atlas = new IconAtlas(new Dictionary<string, Sprite> { ["door"] = Whole });
            var door = DrawnFacing(() => Draw.Icon("door", Centre, 10, Color.White, atlas, facing: new Vector2(0, 1)));
            Assert.True(door.Up.Y > 9, $"atlas sprite {door.Up}");
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
                var drawn = DrawnFacing(() => Draw.Icon("svg:quest", Centre, 10, Color.White, null, facing: facing));
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
}
