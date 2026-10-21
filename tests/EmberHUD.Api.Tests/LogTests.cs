namespace EmberHUD.Api.Tests;

public sealed class LogTests : IDisposable
{
    readonly List<string> _lines = [];

    public LogTests() => Log.Sink = (level, plugin, message) => { lock (_lines) _lines.Add($"{level}|{plugin}|{message}"); };

    public void Dispose()
    {
        Log.Sink = null;
        Log.Filter = null;
        Log.CurrentPlugin.Value = null;
    }

    [Fact]
    public void EachLevelReachesTheSink()
    {
        Log.Info("a");
        Log.Warn("b");
        Log.Error("c");
        Assert.Equal(["Info||a", "Warn||b", "Error||c"], _lines);
    }

    [Fact]
    public async Task TheCurrentPluginFlowsIntoBackgroundWork()
    {
        Log.CurrentPlugin.Value = "Sample";
        await Task.Run(() => Log.Info("from a job"), TestContext.Current.CancellationToken);
        Assert.Equal(["Info|Sample|from a job"], _lines);
    }

    [Fact]
    public void AnExceptionCarriesItsContextAndType()
    {
        Log.Error(new InvalidOperationException("boom"), "loading");
        Assert.StartsWith("Error||loading - System.InvalidOperationException: boom", Assert.Single(_lines));
    }

    [Fact]
    public void NoSinkDropsTheLine()
    {
        Log.Sink = null;
        Log.Info("dropped");
        Assert.Empty(_lines);
    }

    [Fact]
    public void TraceAndDebugReachTheSink()
    {
        Log.Trace("a");
        Log.Debug("b");
        Assert.Equal(["Trace||a", "Debug||b"], _lines);
    }

    [Fact]
    public void TheFilterDropsLinesBelowTheMinimum()
    {
        Log.Filter = (level, _) => level >= LogLevel.Info;
        Log.Trace("a");
        Log.Debug("b");
        Log.Info("c");
        Assert.Equal(["Info||c"], _lines);
    }

    [Fact]
    public void IsEnabledFollowsTheFilterForTheCurrentPlugin()
    {
        Log.Filter = (level, source) => source == "Pickit" || level >= LogLevel.Warn;
        Assert.False(Log.IsEnabled(LogLevel.Debug));
        Log.CurrentPlugin.Value = "Pickit";
        Assert.True(Log.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void ADroppedLineNeverInvokesTheSink()
    {
        var calls = 0;
        Log.Sink = (_, _, _) => calls++;
        Log.Filter = (level, _) => level >= LogLevel.Info;
        for (var i = 0; i < 1000; i++) Log.Debug("x");
        Assert.Equal(0, calls);
    }

    [Fact]
    public void SourceLogTagsHostLines()
    {
        Log.For("Loader").Warn("x");
        Assert.Equal(["Warn|Loader|x"], _lines);
    }

    [Fact]
    public void ThePluginTagWinsOverTheSourceLog()
    {
        Log.CurrentPlugin.Value = "Sample";
        Log.For("Loader").Info("y");
        Assert.Equal(["Info|Sample|y"], _lines);
    }

    [Fact]
    public void SourceLogUsesItsSourceForTheFilter()
    {
        Log.Filter = (_, source) => source == "Reader";
        Log.For("Loader").Info("a");
        Log.For("Reader").Info("b");
        Assert.Equal(["Info|Reader|b"], _lines);
    }
}
