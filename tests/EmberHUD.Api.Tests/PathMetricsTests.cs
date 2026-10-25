namespace EmberHUD.Api.Tests;

public sealed class PathMetricsTests
{
    static readonly MetricsRect Target = new(990, -5, 1010, 5);

    static List<PathSample> Line(double fromX, double toX, int steps, double stepMs)
    {
        var list = new List<PathSample>();
        for (var i = 0; i <= steps; i++) list.Add(new PathSample(i * stepMs, fromX + (toX - fromX) * i / steps, 0));
        return list;
    }

    [Fact]
    public void StraightConstantSpeedLineScoresClean()
    {
        var s = PathMetrics.Measure(Line(0, 1000, 100, 10), Target);
        Assert.Equal(1000, s.DurationMs, 6);
        Assert.Equal(1000, s.Distance, 6);
        Assert.Equal(1000, s.PathLength, 6);
        Assert.Equal(1000, s.MeanSpeed, 6);
        Assert.Equal(1000, s.PeakSpeed, 6);
        Assert.Equal(0, s.FinalError, 6);
        Assert.Equal(0, s.Overshoot, 6);
        Assert.Equal(0, s.MaxBacktrack, 6);
        Assert.True(s.HeadingJitter < 0.01);
        Assert.True(s.SpeedRoughness < 0.01);
        Assert.Equal(0, s.LongestStallMs, 6);
    }

    [Fact]
    public void OvershootAndReturnReportsBoth()
    {
        var path = Line(0, 1020, 102, 10);
        for (var i = 1; i <= 20; i++) path.Add(new PathSample(1020 + i * 10, 1020 - i, 0));
        var s = PathMetrics.Measure(path, Target);
        Assert.Equal(20, s.Overshoot, 3);
        Assert.Equal(20, s.MaxBacktrack, 3);
    }

    [Fact]
    public void StallInTheMiddleIsMeasured()
    {
        var path = Line(0, 500, 50, 10);
        path.Add(new PathSample(600, 500, 0));
        for (var i = 1; i <= 50; i++) path.Add(new PathSample(600 + i * 10, 500 + i * 10, 0));
        var s = PathMetrics.Measure(path, Target);
        Assert.Equal(100, s.LongestStallMs, 6);
    }

    [Fact]
    public void WobblyPathHasMoreHeadingJitterThanStraight()
    {
        var straight = PathMetrics.Measure(Line(0, 1000, 100, 10), Target);
        var wobbly = new List<PathSample>();
        for (var i = 0; i <= 100; i++) wobbly.Add(new PathSample(i * 10, i * 10, i % 2 == 0 ? 6 : -6));
        Assert.True(PathMetrics.Measure(wobbly, Target).HeadingJitter > straight.HeadingJitter + 0.1);
    }

    [Fact]
    public void EndingInsideTheTargetRectIsZeroFinalError()
    {
        var path = Line(0, 995, 100, 10);
        Assert.Equal(0, PathMetrics.Measure(path, Target).FinalError);
        Assert.Equal(15, PathMetrics.Measure(Line(0, 1015, 100, 10), Target).FinalError, 6);
    }

    [Fact]
    public void StationarySamplesBeforeAndAfterDoNotExtendTheDuration()
    {
        var path = new List<PathSample>();
        for (var i = 0; i < 150; i++) path.Add(new PathSample(i, 0, 0));
        foreach (var s in Line(0, 1000, 100, 10)) path.Add(s with { TimeMs = s.TimeMs + 150 });
        for (var i = 1; i <= 150; i++) path.Add(new PathSample(1150 + i, 1000, 0));
        var scores = PathMetrics.Measure(path, Target);
        Assert.Equal(1000, scores.DurationMs, 6);
        Assert.Equal(1000, scores.MeanSpeed, 6);
    }

    [Fact]
    public void PeakSpeedOfAOneMsLineIsItsSpeed()
    {
        Assert.Equal(1000, PathMetrics.Measure(Line(0, 1000, 1000, 1), Target).PeakSpeed, 3);
    }

    [Fact]
    public void OneSampleJitterSpikeDoesNotDominatePeakSpeed()
    {
        var path = Line(0, 1000, 1000, 1);
        path[500] = path[500] with { X = path[500].X + 3 };
        var peak = PathMetrics.Measure(path, Target).PeakSpeed;
        Assert.InRange(peak, 1000, 1300);
    }

    [Fact]
    public void NothingMovingScoresZeroDurationAndSpeed()
    {
        var s = PathMetrics.Measure([new PathSample(0, 5, 5), new PathSample(50, 5, 5), new PathSample(100, 5.2, 5)], Target);
        Assert.Equal((0, 0, 0), (s.DurationMs, s.MeanSpeed, s.PeakSpeed));
    }

    [Fact]
    public void EmptyOneSampleAndZeroLengthMovesDoNotThrowOrNaN()
    {
        var cases = new[]
        {
            new List<PathSample>(),
            [new PathSample(0, 5, 5)],
            [new PathSample(0, 5, 5), new PathSample(0, 5, 5), new PathSample(10, 5, 5)],
            [new PathSample(0, 0, 0), new PathSample(0, 10, 0)],
        };
        foreach (var c in cases)
        {
            var s = PathMetrics.Measure(c, Target);
            double[] all = [s.DurationMs, s.Distance, s.PathLength, s.MeanSpeed, s.PeakSpeed, s.FinalError, s.Overshoot, s.MaxBacktrack, s.HeadingJitter, s.SpeedRoughness, s.LongestStallMs];
            Assert.All(all, v => Assert.True(double.IsFinite(v)));
        }
    }

    [Fact]
    public void FromWaypointsAccumulatesDelays()
    {
        var samples = PathMetrics.FromWaypoints((0, 0), [new Waypoint(10, 0, 5), new Waypoint(20, 0, 7), new Waypoint(30, 0, 9)]);
        Assert.Equal([0.0, 0.0, 5.0, 12.0], samples.Select(s => s.TimeMs));
        Assert.Equal([0.0, 10.0, 20.0, 30.0], samples.Select(s => s.X));
    }
}
