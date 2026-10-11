using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

public sealed class PluginTests
{
    public sealed class ShowSettings : PluginSettings
    {
        public ToggleNode Show = new(true);
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

    [Fact]
    public void RangeNodeClampsToItsBounds()
    {
        var high = new RangeNode<float>(50, 0, 10);
        high.Clamp();
        Assert.Equal(10, high.Value);
        var low = new RangeNode<int>(-5, 0, 10);
        low.Clamp();
        Assert.Equal(0, low.Value);
        var inside = new RangeNode<int>(4, 0, 10);
        inside.Clamp();
        Assert.Equal(4, inside.Value);
    }

    [Fact]
    public void ChoiceNodeKeepsItsOptions() => Assert.Equal(["Fit", "Follow"], new ChoiceNode("Fit", "Fit", "Follow").Options);

    [Fact]
    public void TextNodeNeverHoldsNull() => Assert.Equal("", new TextNode(null).Value);

    [Fact]
    public void HotkeyNodeIsNotPressedHeadless() => Assert.False(new HotkeyNode(new Hotkey(Key.F10)).Pressed);

    [Fact]
    public void DefaultDrawSettingsDrawsTheNodes()
    {
        var plugin = new ShowPlugin();
        var drawn = 0f;
        UiFrame.Run("default settings", () =>
        {
            var before = ImGui.GetCursorPosY();
            plugin.DrawSettings();
            drawn = ImGui.GetCursorPosY() - before;
        });
        Assert.True(drawn > 0);
        var bare = 0f;
        UiFrame.Run("bare settings", () =>
        {
            var before = ImGui.GetCursorPosY();
            new BarePlugin().DrawSettings();
            bare = ImGui.GetCursorPosY() - before;
        });
        Assert.Equal(0, bare);
    }
}
