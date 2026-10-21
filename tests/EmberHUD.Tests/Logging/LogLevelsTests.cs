using EmberHUD.Host.Logging;
using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class LogLevelsTests
{
    [Fact]
    public void TheMinimumAppliesToEverySource()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Warn });

        Assert.False(filter(LogLevel.Info, "Reader"));
        Assert.False(filter(LogLevel.Info, "Pickit"));
        Assert.True(filter(LogLevel.Warn, "Reader"));
        Assert.True(filter(LogLevel.Error, "Pickit"));
    }

    [Fact]
    public void AnOverrideCanLowerTheMinimumForOneSource()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Info, LogSources = new() { ["Pickit"] = LogLevel.Debug } });

        Assert.True(filter(LogLevel.Debug, "Pickit"));
        Assert.False(filter(LogLevel.Debug, "Reader"));
    }

    [Fact]
    public void AnOverrideCanRaiseTheMinimumForOneSource()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Info, LogSources = { ["Reader"] = LogLevel.Error } });

        Assert.False(filter(LogLevel.Warn, "Reader"));
        Assert.True(filter(LogLevel.Warn, "Pickit"));
    }

    [Fact]
    public void TheFilterReadsTheSettingsLive()
    {
        var sdk = new SdkSettings { LogMinimum = LogLevel.Info };
        var filter = LogLevels.Filter(sdk);
        Assert.False(filter(LogLevel.Debug, "Reader"));

        sdk.LogMinimum = LogLevel.Debug;

        Assert.True(filter(LogLevel.Debug, "Reader"));
    }

    [Fact]
    public void NullSourceCountsAsHost()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = LogLevel.Info, LogSources = { ["Host"] = LogLevel.Error } });

        Assert.False(filter(LogLevel.Warn, null));
        Assert.True(filter(LogLevel.Error, null));
    }

    [Fact]
    public void SetOverrideNullRemovesTheEntry()
    {
        var sdk = new SdkSettings();

        LogLevels.SetOverride(sdk, "Reader", LogLevel.Trace);
        Assert.Equal(LogLevel.Trace, sdk.LogSources["Reader"]);

        LogLevels.SetOverride(sdk, "Reader", null);
        Assert.Empty(sdk.LogSources);
    }

    [Fact]
    public void SetOverrideLeavesTheOldDictionaryUntouched()
    {
        var sdk = new SdkSettings();
        var before = sdk.LogSources;

        LogLevels.SetOverride(sdk, "Reader", LogLevel.Trace);

        Assert.NotSame(before, sdk.LogSources);
        Assert.Empty(before);
    }

    [Fact]
    public void ComboIndexMapsToTheLevelAndBack()
    {
        Assert.Null(LogLevels.FromOverrideIndex(0));
        Assert.Equal(LogLevel.Trace, LogLevels.FromOverrideIndex(1));
        Assert.Equal(LogLevel.Error, LogLevels.FromOverrideIndex(5));
        var sources = new Dictionary<string, LogLevel> { ["Reader"] = LogLevel.Warn, ["Bad"] = (LogLevel)99 };
        Assert.Equal(4, LogLevels.OverrideIndex(sources, "Reader"));
        Assert.Equal(0, LogLevels.OverrideIndex(sources, "Other"));
        Assert.Equal(3, LogLevels.OverrideIndex(sources, "Bad"));
    }

    [Fact]
    public void UndefinedLevelsCountAsInfo()
    {
        var filter = LogLevels.Filter(new SdkSettings { LogMinimum = (LogLevel)99, LogSources = { ["Reader"] = (LogLevel)(-3) } });

        Assert.False(filter(LogLevel.Debug, "Pickit"));
        Assert.True(filter(LogLevel.Info, "Pickit"));
        Assert.False(filter(LogLevel.Debug, "Reader"));
        Assert.True(filter(LogLevel.Info, "Reader"));
    }

    [Fact]
    public void SdkSettingsRoundTripsLogSettings()
    {
        const string json = """{ "LogMinimum": "Debug", "LogSources": { "Pickit": "Trace" } }""";
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, json, _ => { });

        Assert.Equal(LogLevel.Debug, loaded.LogMinimum);
        Assert.Equal(LogLevel.Trace, loaded.LogSources["Pickit"]);
        var saved = SettingsJson.Serialize(loaded);
        Assert.Contains("\"LogMinimum\": \"Debug\"", saved);
        Assert.Contains("\"Pickit\": \"Trace\"", saved);
        Assert.Equal(saved, SettingsJson.Serialize((SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, saved, _ => { })));
    }
}
