using System.Drawing;
using System.Numerics;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class IconShapesTests
{
    static readonly Vector2 Min = new(10, 20);
    static readonly Vector2 Max = new(50, 60);
    static readonly IconShape[] All = Enum.GetValues<IconShape>();

    static (Vector2 Min, Vector2 Max) Bounds(IEnumerable<Vector2> points) =>
        (new Vector2(points.Min(p => p.X), points.Min(p => p.Y)), new Vector2(points.Max(p => p.X), points.Max(p => p.Y)));

    static void AssertNear(Vector2 expected, Vector2 actual) => Assert.True(Vector2.Distance(expected, actual) < 1e-3f, $"{expected} vs {actual}");

    [Fact]
    public void SquareOutlineIsTheFourCorners() =>
        Assert.Equal([Min, new Vector2(Max.X, Min.Y), Max, new Vector2(Min.X, Max.Y)], IconShapes.Outline(IconShape.Square, Min, Max));

    [Theory]
    [InlineData(IconShape.Rounded)]
    [InlineData(IconShape.Circle)]
    [InlineData(IconShape.Hexagon)]
    public void CircleAndHexagonPointsStayInsideTheRect(IconShape shape)
    {
        foreach (var p in IconShapes.Outline(shape, Min, Max, 8))
        {
            Assert.InRange(p.X, Min.X - 1e-3f, Max.X + 1e-3f);
            Assert.InRange(p.Y, Min.Y - 1e-3f, Max.Y + 1e-3f);
        }
    }

    [Fact]
    public void HexagonHasSixPoints() => Assert.Equal(6, IconShapes.Outline(IconShape.Hexagon, Min, Max).Length);

    [Fact]
    public void HexagonIsPointyTopAndRegular()
    {
        var p = IconShapes.Outline(IconShape.Hexagon, Vector2.Zero, new Vector2(40, 40));
        Assert.Equal(6, p.Length);
        AssertNear(new Vector2(20, 0), p[0]);
        AssertNear(new Vector2(20, 40), p[3]);
        AssertNear(new Vector2(20 + 17.3205f, 10), p[1]);
        AssertNear(new Vector2(20 + 17.3205f, 30), p[2]);
        AssertNear(new Vector2(20 - 17.3205f, 30), p[4]);
        AssertNear(new Vector2(20 - 17.3205f, 10), p[5]);
        for (var i = 0; i < 6; i++) Assert.Equal(20f, Vector2.Distance(p[i], p[(i + 1) % 6]), 1e-3f);
    }

    [Fact]
    public void HexagonFitsANarrowRectWithoutStretching()
    {
        var (lo, hi) = Bounds(IconShapes.Outline(IconShape.Hexagon, Vector2.Zero, new Vector2(20, 40)));
        var height = 20 / (MathF.Sqrt(3) / 2);
        Assert.Equal(0f, lo.X, 1e-3f);
        Assert.Equal(20f, hi.X, 1e-3f);
        Assert.Equal(height, hi.Y - lo.Y, 1e-3f);
        Assert.Equal(20f, (lo.Y + hi.Y) / 2, 1e-3f);
    }

    [Fact]
    public void HexagonInAWideRectStaysCentredAndRegular()
    {
        var (lo, hi) = Bounds(IconShapes.Outline(IconShape.Hexagon, Vector2.Zero, new Vector2(80, 40)));
        Assert.Equal(40f, (lo.X + hi.X) / 2, 1e-3f);
        Assert.Equal(34.641f, hi.X - lo.X, 1e-3f);
        Assert.Equal(40f, hi.Y - lo.Y, 1e-3f);
    }

    [Fact]
    public void HexagonSurvivesDegenerateRects()
    {
        foreach (var (a, b) in new[] { (Vector2.Zero, Vector2.One), (new Vector2(5, 5), new Vector2(5, 5)), (new Vector2(10, 10), Vector2.Zero) })
            Assert.All(IconShapes.Outline(IconShape.Hexagon, a, b), p => Assert.False(float.IsNaN(p.X) || float.IsNaN(p.Y)));
    }

    [Fact]
    public void CirclePointsAreEquidistantOnASquareRect()
    {
        var centre = (Min + Max) / 2;
        var points = IconShapes.Outline(IconShape.Circle, Min, new Vector2(Max.X, Min.Y + 40));
        Assert.Equal(32, points.Length);
        Assert.All(points, p => Assert.Equal(20f, Vector2.Distance(new Vector2(centre.X, Min.Y + 20), p), 1e-3f));
    }

    [Fact]
    public void RoundedRadiusClampsToHalfTheSide()
    {
        var max = new Vector2(40, 40);
        var points = IconShapes.Outline(IconShape.Rounded, Vector2.Zero, max, 999);
        var (lo, hi) = Bounds(points);
        AssertNear(Vector2.Zero, lo);
        AssertNear(max, hi);
        Assert.Contains(points, p => MathF.Abs(p.X - 20) < 1e-3f && MathF.Abs(p.Y) < 1e-3f);
        Assert.Contains(points, p => MathF.Abs(p.X - 40) < 1e-3f && MathF.Abs(p.Y - 20) < 1e-3f);
        Assert.Contains(points, p => MathF.Abs(p.X - 20) < 1e-3f && MathF.Abs(p.Y - 40) < 1e-3f);
        Assert.Contains(points, p => MathF.Abs(p.X) < 1e-3f && MathF.Abs(p.Y - 20) < 1e-3f);
    }

    [Fact]
    public void RoundedZeroRadiusEqualsSquareBounds()
    {
        var (lo, hi) = Bounds(IconShapes.Outline(IconShape.Rounded, Min, Max, 0));
        AssertNear(Min, lo);
        AssertNear(Max, hi);
    }

    [Fact]
    public void OneByOneRectDoesNotProduceNaN()
    {
        foreach (var shape in All)
            Assert.All(IconShapes.Outline(shape, Vector2.Zero, Vector2.One, 4), p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y), $"{shape} {p}"));
    }

    [Fact]
    public void ClipTopHalfOfASquare()
    {
        var clipped = IconShapes.ClipTop(IconShapes.Outline(IconShape.Square, Vector2.Zero, new Vector2(10, 10)), 5);
        var (lo, hi) = Bounds(clipped);
        AssertNear(Vector2.Zero, lo);
        AssertNear(new Vector2(10, 5), hi);
    }

    [Fact]
    public void RoundedOnInvertedRectDoesNotThrow() =>
        Assert.Equal(4, IconShapes.Outline(IconShape.Rounded, Max, Min, 8).Length);

    [Fact]
    public void ClipTopWithAVertexOnTheCutHasNoDuplicateConsecutivePoints()
    {
        Vector2[] diamond = [new(5, 0), new(10, 5), new(5, 10), new(0, 5)];
        var clipped = IconShapes.ClipTop(diamond, 5);
        Assert.Equal(3, clipped.Length);
        for (var i = 0; i < clipped.Length; i++)
            Assert.NotEqual(clipped[i], clipped[(i + 1) % clipped.Length]);
    }

    [Fact]
    public void ClipTopAboveTopIsEmpty()
    {
        var square = IconShapes.Outline(IconShape.Square, Vector2.Zero, new Vector2(10, 10));
        Assert.Empty(IconShapes.ClipTop(square, 0));
        Assert.Empty(IconShapes.ClipTop(square, -3));
    }

    [Fact]
    public void ClipTopBelowBottomIsUnchanged()
    {
        var square = IconShapes.Outline(IconShape.Square, Vector2.Zero, new Vector2(10, 10));
        Assert.Equal(square, IconShapes.ClipTop(square, 10));
        Assert.Equal(square, IconShapes.ClipTop(square, 99));
    }

    [Fact]
    public void ClipTopOfACircleStaysInsideTheCircle()
    {
        var clipped = IconShapes.ClipTop(IconShapes.Outline(IconShape.Circle, Vector2.Zero, new Vector2(20, 20)), 7);
        Assert.NotEmpty(clipped);
        Assert.All(clipped, p =>
        {
            Assert.True(p.Y <= 7 + 1e-3f);
            Assert.True(Vector2.Distance(new Vector2(10, 10), p) <= 10 + 1e-3f);
        });
    }

    static Sprite Unit() => new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 1));

    [Fact]
    public void FillStrokeAndImageDrawSomething() => TestFrame.Run(() =>
    {
        var hexagon = IconShapes.Outline(IconShape.Hexagon, Min, Max);
        var list = Draw.Background;

        var before = list.VtxBuffer.Size;
        IconShapes.FillPolygon(hexagon, Color.Red);
        Assert.True(list.VtxBuffer.Size > before);

        before = list.VtxBuffer.Size;
        IconShapes.StrokePolygon(hexagon, Color.Red, 2);
        Assert.True(list.VtxBuffer.Size > before);

        before = list.VtxBuffer.Size;
        IconShapes.DrawImage(Unit(), hexagon, Min, Max, Color.White);
        Assert.Equal(before + 6, list.VtxBuffer.Size);
        var sprite = Unit();
        Assert.Equal((sprite.Uv0.X + sprite.Uv1.X) / 2, list.VtxBuffer[before].Uv.X, 1e-4f);
        Assert.Equal(sprite.Uv0.Y, list.VtxBuffer[before].Uv.Y, 1e-4f);
    });

    [Fact]
    public void EmptyOrTinyInputsDrawNothing() => TestFrame.Run(() =>
    {
        var list = Draw.Background;
        var before = list.VtxBuffer.Size;
        Vector2[] two = [Min, Max];
        var hexagon = IconShapes.Outline(IconShape.Hexagon, Min, Max);
        IconShapes.FillPolygon(two, Color.Red);
        IconShapes.StrokePolygon(two, Color.Red, 2);
        IconShapes.DrawImage(Unit(), two, Min, Max, Color.White);
        IconShapes.FillPolygon(hexagon, Color.FromArgb(0, Color.Red));
        IconShapes.StrokePolygon(hexagon, Color.FromArgb(0, Color.Red), 2);
        IconShapes.StrokePolygon(hexagon, Color.Red, 0);
        Assert.Equal(before, list.VtxBuffer.Size);
    });
}
