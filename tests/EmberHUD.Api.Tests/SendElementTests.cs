using System.Numerics;

namespace EmberHUD.Api.Tests;

public sealed class SendElementTests
{
    static ScreenRect Rect(float x1, float y1, float x2, float y2) => new(new Vector2(x1, y1), new Vector2(x2, y2));

    static List<(int X, int Y)> Draw(ScreenRect rect, int margin, int count = 5000)
    {
        var rng = new Random(1);
        return Enumerable.Range(0, count).Select(_ => Send.PickPoint(rect, margin, rng)!.Value).ToList();
    }

    [Fact]
    public void PickPointStaysInsideTheInsetRect()
    {
        foreach (var (x, y) in Draw(Rect(100, 200, 300, 260), 4))
        {
            Assert.InRange(x, 104, 296);
            Assert.InRange(y, 204, 256);
        }
    }

    [Fact]
    public void PickPointFavoursTheCentre()
    {
        var points = Draw(Rect(100, 200, 300, 260), 4);
        Assert.True(points.Count(p => p.X is >= 150 and <= 250) > points.Count * 0.6);
        Assert.InRange(points.Average(p => p.X), 197, 203);
    }

    [Fact]
    public void PickPointUsesCentreWhenRectIsSmallerThanTwiceTheMargin()
    {
        var points = Draw(Rect(10, 10, 16, 40), 4);
        Assert.All(points, p => Assert.Equal(13, p.X));
        Assert.All(points, p => Assert.InRange(p.Y, 14, 36));
        Assert.True(points.Select(p => p.Y).Distinct().Count() > 1);
    }

    [Fact]
    public void PickPointRejectsBadRects()
    {
        var rng = new Random(1);
        Assert.Null(Send.PickPoint(null, 4, rng));
        Assert.Null(Send.PickPoint(Rect(5, 5, 5, 20), 4, rng));
        Assert.Null(Send.PickPoint(Rect(20, 20, 10, 10), 4, rng));
        Assert.Null(Send.PickPoint(Rect(float.NaN, 0, 10, 10), 4, rng));
        Assert.Null(Send.PickPoint(Rect(0, 0, float.PositiveInfinity, 10), 4, rng));
    }

    [Fact]
    public void PickPointTreatsNegativeMarginAsZero()
    {
        foreach (var (x, y) in Draw(Rect(0, 0, 10, 10), -5))
        {
            Assert.InRange(x, 0, 10);
            Assert.InRange(y, 0, 10);
        }
    }
}
