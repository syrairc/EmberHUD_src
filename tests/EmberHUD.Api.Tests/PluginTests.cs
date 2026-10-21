namespace EmberHUD.Api.Tests;

public sealed class PluginTests
{
    public sealed class ShowSettings : PluginSettings
    {
        public bool Show = true;

        public override void Draw() { }
    }

    public sealed class ShowPlugin : Plugin<ShowSettings>;

    sealed class BarePlugin : Plugin;

    sealed class DebugPlugin : Plugin
    {
        public override LogLevel DefaultLogLevel => LogLevel.Debug;
    }

    [Fact]
    public void ThePluginLogLevelDefaultsToInfoAndCanBeOverridden()
    {
        Assert.Equal(LogLevel.Info, new BarePlugin().DefaultLogLevel);
        Assert.Equal(LogLevel.Debug, new DebugPlugin().DefaultLogLevel);
    }

    [Fact]
    public void PluginOfTHandsItsSettingsToTheHost()
    {
        var plugin = new ShowPlugin();
        Assert.Same(plugin.Settings, plugin.SettingsObject);
        Assert.Equal(typeof(ShowSettings), plugin.SettingsType);
        var replaced = new ShowSettings();
        plugin.SettingsObject = replaced;
        Assert.Same(replaced, plugin.Settings);
    }

    [Fact]
    public void ABarePluginHasNoSettings()
    {
        var plugin = new BarePlugin();
        Assert.Null(plugin.SettingsType);
        Assert.Null(plugin.SettingsObject);
    }
}
