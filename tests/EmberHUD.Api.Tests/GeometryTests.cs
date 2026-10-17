namespace EmberHUD.Api.Tests;

public sealed class GeometryTests
{
    static readonly Vector2[] Unit = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    static Vector2[] Clip(Vector2[] subject, Vector2[] clip)
    {
        var into = new Vector2[subject.Length + clip.Length];
        return into[..Geometry.ClipConvex(subject, clip, into)];
    }

    [Fact]
    public void ContainsRejectsDegenerateShapesAndNaN()
    {
        Assert.False(Geometry.Contains([new(0, 0), new(1, 1)], new Vector2(0.5f, 0.5f)));
        Assert.False(Geometry.Contains([new(0, 0), new(1, 1), new(2, 2)], new Vector2(1, 1)));
        Assert.False(Geometry.Contains(Unit, new Vector2(float.NaN, 0.5f)));
        Assert.True(Geometry.Contains(Unit, new Vector2(0.5f, 0.5f)));
    }

    [Fact]
    public void ATooSmallOutputBufferReturnsZero()
    {
        Vector2[] clip = [new(0.5f, 0.5f), new(2, 0.5f), new(2, 2), new(0.5f, 2)];

        Assert.Equal(0, Geometry.ClipConvex(Unit, clip, new Vector2[3]));
        Assert.Equal(0, Geometry.ClipConvex(Unit, clip, new Vector2[4]));
    }

    [Fact]
    public void ClippingAgainstAShiftedSquareKeepsTheOverlap()
    {
        var result = Clip(Unit, [new(0.5f, 0), new(1.5f, 0), new(1.5f, 1), new(0.5f, 1)]);

        Assert.Equal(4, result.Length);
        Assert.Equal(0.5f, result.Min(p => p.X), 0.001f);
        Assert.Equal(1f, result.Max(p => p.X), 0.001f);
    }

    [Fact]
    public void ASubjectInsideTheClipIsReturnedWhole()
    {
        var result = Clip(Unit, [new(-1, -1), new(2, -1), new(2, 2), new(-1, 2)]);

        Assert.Equal(Unit.Length, result.Length);
        Assert.All(Unit, point => Assert.Contains(point, result));
    }

    [Fact]
    public void DisjointShapesClipToNothing() => Assert.Empty(Clip(Unit, [new(5, 5), new(6, 5), new(6, 6), new(5, 6)]));

    [Fact]
    public void ClippingWorksForEitherWinding()
    {
        Vector2[] reversed = [.. Unit.Reverse()];

        Assert.Equal(4, Clip(reversed, [new(0.5f, 0), new(1.5f, 0), new(1.5f, 1), new(0.5f, 1)]).Length);
    }

    [Fact]
    public void ContainsIncludesTheEdge()
    {
        Assert.True(Geometry.Contains(Unit, new Vector2(0.5f, 0.5f)));
        Assert.True(Geometry.Contains(Unit, new Vector2(1, 0.5f)));
        Assert.False(Geometry.Contains(Unit, new Vector2(1.01f, 0.5f)));
    }

    [Fact]
    public void DistanceToASegmentIsPerpendicularOrToTheNearerEnd()
    {
        Vector2 a = new(0, 0), b = new(10, 0);

        Assert.Equal(3, Geometry.DistanceToSegment(a, b, new Vector2(5, 3)), 0.001f);
        Assert.Equal(5, Geometry.DistanceToSegment(a, b, new Vector2(-3, 4)), 0.001f);
        Assert.Equal(5, Geometry.DistanceToSegment(a, b, new Vector2(13, -4)), 0.001f);
    }

    [Fact]
    public void ADegenerateSegmentIsAPoint() => Assert.Equal(5, Geometry.DistanceToSegment(new Vector2(1, 1), new Vector2(1, 1), new Vector2(4, 5)), 0.001f);

    [Fact]
    public void ARegularPolygonHasTheRequestedPointsOnTheRadius()
    {
        var points = new Vector2[6];

        Geometry.RegularPolygon(new Vector2(10, 10), 5, points);

        Assert.All(points, point => Assert.Equal(5, Vector2.Distance(point, new Vector2(10, 10)), 0.001f));
    }

    [Fact]
    public void OrientedRectAlongXIsAxisAligned()
    {
        var corners = new Vector2[4];
        Geometry.OrientedRect(new Vector2(10, 20), Vector2.UnitX, 4, 100, corners);

        Assert.Equal([new(-40, 18), new(60, 18), new(60, 22), new(-40, 22)], corners);
    }

    [Fact]
    public void OrientedRectContainsItsCentreButNotPastItsEnd()
    {
        var corners = new Vector2[4];
        Geometry.OrientedRect(Vector2.Zero, Vector2.Normalize(new Vector2(1, 1)), 10, 100, corners);

        Assert.True(Geometry.Contains(corners, Vector2.Zero));
        Assert.True(Geometry.Contains(corners, new Vector2(30, 30)));
        Assert.False(Geometry.Contains(corners, new Vector2(40, 40)));
        Assert.False(Geometry.Contains(corners, new Vector2(10, -10)));
    }
}
