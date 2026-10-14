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
        Assert.Equal(0, plugin.Order);
        Assert.Null(plugin.SettingsType);
        Assert.Null(plugin.SettingsObject);
    }
}
