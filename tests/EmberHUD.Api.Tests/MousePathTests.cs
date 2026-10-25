namespace EmberHUD.Api.Tests;

public sealed class MousePathTests
{
    static readonly HumanizerConfig Default = new();

    [Theory]
    [InlineData(0, 0, 800, 450)]
    [InlineData(500, 500, 10, 20)]
    public void LastWaypointIsExactlyTheTarget(int fx, int fy, int tx, int ty)
    {
        var path = MousePath.Generate((fx, fy), (tx, ty), Default, new Random(1));
        Assert.Equal(new Waypoint(tx, ty, path[^1].DelayMs), path[^1]);
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
        Assert.Equal((800, 450), (path[^1].X, path[^1].Y));
    }

    [Fact]
    public void HugeDelaysClampToTenSecondsAndPathStillGenerates()
    {
        var huge = (Default with { StepDelayMax = int.MaxValue, PreClickSettleMax = int.MaxValue }).Clamped();
        Assert.True(huge.StepDelayMax <= 10_000);
        Assert.True(huge.PreClickSettleMax <= 10_000);
        var path = MousePath.Generate((0, 0), (800, 450), huge, new Random(1));
        Assert.Equal((800, 450), (path[^1].X, path[^1].Y));
    }

    [Fact]
    public void PathIsNotAStraightLine()
    {
        var path = MousePath.Generate((0, 0), (800, 0), Default, new Random(1));
        Assert.Contains(path, p => p.Y != 0);
    }
}
