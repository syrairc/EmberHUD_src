namespace EmberHUD.Api.Tests;

public sealed class LogTests : IDisposable
{
    readonly List<string> _lines = [];

    public LogTests() => Log.Sink = (level, plugin, message) => { lock (_lines) _lines.Add($"{level}|{plugin}|{message}"); };

    public void Dispose()
    {
        Log.Sink = null;
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
}
