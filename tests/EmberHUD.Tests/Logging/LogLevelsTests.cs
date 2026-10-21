using EmberHUD.Host.Logging;
using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class LogLevelsTests
{
    [Fact]
    public void HostSourcesUseTheMinimum()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Warn }, new PluginLogLevels());

        Assert.False(filter(LogLevel.Info, "Reader"));
        Assert.False(filter(LogLevel.Info, "Whatever"));
        Assert.True(filter(LogLevel.Warn, "Reader"));
        Assert.True(filter(LogLevel.Error, "Loader"));
    }

    [Fact]
    public void ThePluginLevelCanLowerTheMinimumForThatPlugin()
    {
        var plugins = new PluginLogLevels();
        plugins.Set("Pickit", LogLevel.Debug);
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Info }, plugins);

        Assert.True(filter(LogLevel.Debug, "Pickit"));
        Assert.False(filter(LogLevel.Debug, "Other"));
        Assert.False(filter(LogLevel.Debug, "Reader"));
    }

    [Fact]
    public void ThePluginLevelCanRaiseTheMinimumForThatPlugin()
    {
        var plugins = new PluginLogLevels();
        plugins.Set("Pickit", LogLevel.Error);
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Debug }, plugins);

        Assert.False(filter(LogLevel.Warn, "Pickit"));
        Assert.True(filter(LogLevel.Error, "Pickit"));
        Assert.True(filter(LogLevel.Debug, "Reader"));
    }

    [Fact]
    public void TheFilterReadsTheMinimumAndThePluginLevelsLive()
    {
        var sdk = new SdkSettings { LogMinimum = LogLevel.Info };
        var plugins = new PluginLogLevels();
        var filter = LogLevels.Filter(sdk, plugins);
        Assert.False(filter(LogLevel.Debug, "Reader"));
        Assert.False(filter(LogLevel.Debug, "Pickit"));

        sdk.LogMinimum = LogLevel.Debug;
        plugins.Set("Pickit", LogLevel.Error);

        Assert.True(filter(LogLevel.Debug, "Reader"));
        Assert.False(filter(LogLevel.Debug, "Pickit"));
        plugins.Remove("Pickit");
        Assert.True(filter(LogLevel.Debug, "Pickit"));
    }

    [Fact]
    public void NullSourceCountsAsHost()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Warn }, new PluginLogLevels());

        Assert.False(filter(LogLevel.Info, null));
        Assert.True(filter(LogLevel.Warn, null));
    }

    [Fact]
    public void UndefinedLevelsCountAsInfo()
    {
        var plugins = new PluginLogLevels();
        plugins.Set("Pickit", (LogLevel)(-3));
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = (LogLevel)99 }, plugins);

        Assert.False(filter(LogLevel.Debug, "Reader"));
        Assert.True(filter(LogLevel.Info, "Reader"));
        Assert.False(filter(LogLevel.Debug, "Pickit"));
        Assert.True(filter(LogLevel.Info, "Pickit"));
    }

    [Fact]
    public void PluginLevelsAreReplacedNotMutated()
    {
        var plugins = new PluginLogLevels();
        plugins.Set("A", LogLevel.Debug);
        var before = plugins.Snapshot;

        plugins.Set("B", LogLevel.Warn);
        plugins.Remove("A");

        Assert.Single(before);
        Assert.Equal(LogLevel.Debug, before["A"]);
        Assert.Equal(LogLevel.Warn, Assert.Single(plugins.Snapshot).Value);
    }

    [Fact]
    public void SdkSettingsRoundTripsTheMinimum()
    {
        const string json = """{ "LogMinimum": "Debug" }""";
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, json, _ => { });

        Assert.Equal(LogLevel.Debug, loaded.LogMinimum);
        var saved = SettingsJson.Serialize(loaded);
        Assert.Contains("\"LogMinimum\": \"Debug\"", saved);
        Assert.Equal(saved, SettingsJson.Serialize((SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, saved, _ => { })));
    }

    [Fact]
    public void ALegacyLogSourcesKeyLoadsWithoutAProblemAndIsNotSavedBack()
    {
        const string json = """{ "LogMinimum": "Warn", "LogSources": { "Pickit": "Debug" } }""";
        var problems = new List<string>();

        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, json, problems.Add);

        Assert.Empty(problems);
        Assert.Equal(LogLevel.Warn, loaded.LogMinimum);
        Assert.DoesNotContain("LogSources", SettingsJson.Serialize(loaded));
    }
}
