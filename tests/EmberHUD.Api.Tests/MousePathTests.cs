namespace EmberHUD.Api.Tests;

public sealed class MousePathTests
{
    static readonly HumanizerConfig Default = new();

    [Theory]
    [InlineData(0, 0, 800, 450)]
    [InlineData(500, 500, 10, 20)]
    public void LastWaypointIsExactlyTheTargetAtZeroTolerance(int fx, int fy, int tx, int ty)
    {
        var path = MousePath.Generate((fx, fy), (tx, ty), Default with { StopTolerance = 0 }, new Random(1));
        Assert.Equal(new Waypoint(tx, ty, path[^1].DelayMs), path[^1]);
    }

    [Fact]
    public void ZeroToleranceKeepsTheExactFinalWaypoint()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var path = MousePath.Generate((0, 0), (333, 77), Default with { StopTolerance = 0 }, new Random(seed));
            Assert.Equal((333, 77), (path[^1].X, path[^1].Y));
        }
    }

    [Fact]
    public void ToleranceBiggerThanTheMoveStillTerminates()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var path = MousePath.Generate((100, 100), (102, 100), Default with { StopTolerance = 20 }, new Random(seed));
            Assert.InRange(path.Count, 1, 10);
            var last = path[^1];
            Assert.True(Math.Sqrt((last.X - 102) * (last.X - 102) + (last.Y - 100) * (last.Y - 100)) <= 20);
        }
    }

    internal static IEnumerable<HumanizerConfig> SweepConfigs()
    {
        yield return new HumanizerConfig();
        foreach (var speed in new[] { 50, 75, 100, 125, 150 })
        {
            int Delay(int atFullSpeed) => (int)Math.Round(atFullSpeed * 150.0 / speed);
            yield return new HumanizerConfig
            {
                MaxStep = (float)Math.Round(60.0 * speed / 150, 1),
                StepDelayMin = Delay(0), StepDelayMax = Delay(2),
                PreClickSettleMin = Delay(4), PreClickSettleMax = Delay(12),
                ClickDwellMin = Delay(8), ClickDwellMax = Delay(20),
                PostClickSettleMin = Delay(4), PostClickSettleMax = Delay(12),
            };
        }
    }

    internal static IEnumerable<PathScores> Sweep(HumanizerConfig config)
    {
        foreach (var distance in new[] { 30, 120, 400, 1200, 2400 })
            foreach (var angle in new[] { 0.3, 1.9, 4.4 })
                for (var seed = 1; seed <= 300; seed++)
                {
                    var from = (1500, 1500);
                    var to = (1500 + (int)Math.Round(distance * Math.Cos(angle)), 1500 + (int)Math.Round(distance * Math.Sin(angle)));
                    var path = MousePath.Generate(from, to, config, new Random(seed));
                    yield return PathMetrics.Measure(PathMetrics.FromWaypoints(from, path), new MetricsRect(to.Item1, to.Item2, to.Item1, to.Item2));
                }
    }

    [Fact]
    public void HumanizedPathEndsWithinStopToleranceAndNeverBacktracksPast()
    {
        foreach (var config in SweepConfigs())
        {
            var tolerance = config.Clamped().StopTolerance;
            foreach (var m in Sweep(config))
            {
                Assert.True(m.MaxBacktrack <= tolerance, $"backtrack {m.MaxBacktrack} maxstep {config.MaxStep}");
                Assert.True(m.Overshoot <= tolerance, $"overshoot {m.Overshoot} maxstep {config.MaxStep}");
                Assert.True(m.FinalError <= tolerance, $"final {m.FinalError} maxstep {config.MaxStep}");
            }
        }
    }

    [Fact]
    public void NoStepIsLongerThanMaxStepPlusRounding()
    {
        var path = MousePath.Generate((0, 0), (800, 450), Default with { MaxStep = 15 }, new Random(1));
        var prev = (X: 0, Y: 0);
        foreach (var p in path)
        {
            var d = Math.Sqrt((p.X - prev.X) * (p.X - prev.X) + (p.Y - prev.Y) * (p.Y - prev.Y));
            Assert.True(d <= 15 + 1.5, $"step {d}");
            prev = (p.X, p.Y);
        }
    }

    [Fact]
    public void SameSeedSamePathDifferentSeedDifferentPath()
    {
        var a = MousePath.Generate((0, 0), (800, 450), Default, new Random(1));
        var b = MousePath.Generate((0, 0), (800, 450), Default, new Random(1));
        var c = MousePath.Generate((0, 0), (800, 450), Default, new Random(2));
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void CursorAlreadyOnTargetGivesOnlyTheFinalWaypoint()
    {
        var path = MousePath.Generate((40, 50), (40, 50), Default, new Random(1));
        var only = Assert.Single(path);
        Assert.Equal((40, 50), (only.X, only.Y));
    }

    [Fact]
    public void DelaysStayInsideTheConfiguredRange()
    {
        var path = MousePath.Generate((0, 0), (800, 450), Default, new Random(1));
        Assert.All(path, p => Assert.InRange(p.DelayMs, Default.StepDelayMin, Default.StepDelayMax));
    }

    [Fact]
    public void PathTerminatesWithBadConfig()
    {
        var bad = Default with { MaxStep = 0, StepDelayMin = 9, StepDelayMax = 2 };
        var path = MousePath.Generate((0, 0), (800, 450), bad, new Random(1));
        Assert.True(path.Count < 10_001);
        Assert.InRange(Math.Sqrt(Math.Pow(path[^1].X - 800, 2) + Math.Pow(path[^1].Y - 450, 2)), 0, 3);
    }

    [Fact]
    public void HugeDelaysClampToTenSecondsAndPathStillGenerates()
    {
        var huge = (Default with { StepDelayMax = int.MaxValue, PreClickSettleMax = int.MaxValue }).Clamped();
        Assert.True(huge.StepDelayMax <= 10_000);
        Assert.True(huge.PreClickSettleMax <= 10_000);
        var path = MousePath.Generate((0, 0), (800, 450), huge, new Random(1));
        Assert.InRange(Math.Sqrt(Math.Pow(path[^1].X - 800, 2) + Math.Pow(path[^1].Y - 450, 2)), 0, 3);
    }

    [Fact]
    public void PathIsNotAStraightLine()
    {
        var path = MousePath.Generate((0, 0), (800, 0), Default, new Random(1));
        Assert.Contains(path, p => p.Y != 0);
    }

    [Fact]
    public void StopToleranceClampsToZeroTwenty()
    {
        Assert.Equal(0, (Default with { StopTolerance = -5 }).Clamped().StopTolerance);
        Assert.Equal(20, (Default with { StopTolerance = 99 }).Clamped().StopTolerance);
        Assert.Equal(3, Default.Clamped().StopTolerance);
    }
}