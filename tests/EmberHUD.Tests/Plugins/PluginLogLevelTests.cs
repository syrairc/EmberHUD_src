using EmberHUD.Host.Logging;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class PluginLogLevelTests : IDisposable
{
    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();

    public void Dispose()
    {
        Log.Filter = null;
        _log.Dispose();
        _fixture.Dispose();
    }

    static string Logging(string name, string level) => PluginFixture.Source(name, level + """

        public override void Tick()
        {
            Log.Debug("d");
            Log.Info("i");
        }
        """);

    PluginHost Started(params (string Name, string Level)[] plugins)
    {
        foreach (var (name, level) in plugins) _fixture.Write(name, "Main.cs", Logging(name, level));
        var host = _fixture.Start();
        Log.Filter = LogLevels.Filter(_fixture.Sdk, host.PluginLevels);
        foreach (var slot in host.Slots) host.SetEnabled(slot, true);
        return host;
    }

    [Fact]
    public void ADebugPluginPassesDebugWhileADefaultPluginDropsIt()
    {
        using var host = Started(("Loud", "public override LogLevel DefaultLogLevel => LogLevel.Debug;"), ("Quiet", ""));

        host.TickAll();

        Assert.Equal(["d", "i"], _log.Messages("Loud"));
        Assert.Equal(["i"], _log.Messages("Quiet"));
    }

    [Fact]
    public void HostTaggedLinesFollowTheMinimum()
    {
        using var host = Started(("Quiet", ""));
        var reader = Log.For("Reader");

        reader.Debug("before");
        _fixture.Sdk.LogMinimum = LogLevel.Debug;
        reader.Debug("after");

        Assert.Equal(["after"], _log.Messages("Reader"));
    }

    [Fact]
    public void TheLevelIsReadOnceAtCreationAndDroppedOnUnload()
    {
        using var host = Started(("Hot", "public override LogLevel DefaultLogLevel => LogLevel.Debug;"));
        Assert.Equal(LogLevel.Debug, host.PluginLevels.Snapshot["Hot"]);
        var slot = host.Slots[0];

        _fixture.Write("Hot", "Main.cs", Logging("Hot", "public override LogLevel DefaultLogLevel => LogLevel.Error;"));
        host.Reload(slot);
        Assert.False(host.PluginLevels.Snapshot.ContainsKey("Hot"));
        PluginFixture.Settle(host);

        Assert.Equal(LogLevel.Error, host.PluginLevels.Snapshot["Hot"]);
    }

    [Fact]
    public void AThrowingGetterFallsBackToInfoWithAWarning()
    {
        using var host = Started(("Bad", "public override LogLevel DefaultLogLevel => throw new InvalidOperationException(\"boom\");"));

        host.TickAll();

        Assert.Equal(LogLevel.Info, host.PluginLevels.Snapshot["Bad"]);
        Assert.Contains(_log.Messages("Bad"), message => message.Contains("DefaultLogLevel") && message.Contains("boom"));
        Assert.Contains("i", _log.Messages("Bad"));
        Assert.DoesNotContain("d", _log.Messages("Bad"));
    }
}
