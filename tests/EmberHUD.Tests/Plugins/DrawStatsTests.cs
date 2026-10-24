using EmberHUD.Host.Plugins;
using EmberHUD.Testing;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public sealed class DrawStatsTests : IDisposable
{
    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();

    public void Dispose()
    {
        _log.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void ReportsOnlyAfterTheIntervalAndNamesTheHeavyPlugin()
    {
        var stats = new DrawStats(1000, 100);
        stats.Add("A", 50);
        stats.Add("B", 500);
        stats.Add("B", 20);
        Assert.Null(stats.Report(0, 3));
        Assert.Null(stats.Report(999, 3));
        Assert.Equal("heavy draw - B max 500 avg 260 verts/frame, 7 projectiles", stats.Report(1000, 7));
        Assert.Null(stats.Report(2000, 7));
    }

    [Fact]
    public void QuietFramesReportNothing()
    {
        var stats = new DrawStats(0, 100);
        stats.Add("A", 99);
        Assert.Null(stats.Report(0, 0));
        Assert.Null(stats.Report(1, 0));
    }

    [Fact]
    public void HostWarnsWhenAPluginDrawsTooMuch()
    {
        _fixture.Write("Hog", "Main.cs", Source("Hog", """
            public override void Render()
            {
                for (var i = 0; i < 100; i++) Draw.RectFilled(new System.Numerics.Vector2(i, 0), new System.Numerics.Vector2(i + 5, 5), System.Drawing.Color.White);
            }
            """));
        using var host = _fixture.Start();
        host.Stats = new DrawStats(0, 100);
        foreach (var slot in host.Slots) host.SetEnabled(slot, true);
        TestFrame.Run(host.RenderAll);
        Assert.Contains(_log.Messages(), message => message.StartsWith("heavy draw - Hog max 400"));
    }
}
