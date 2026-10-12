using Aember.Ui;
using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

public sealed class HotkeyTableTests
{
    sealed class Probe(bool two = false, bool none = false) : Plugin
    {
        public override void Initialize()
        {
            if (none) return;
            RegisterHotkey("Toggle", "Shows it", () => { }, new Hotkey(Key.F1));
            if (two) RegisterHotkey("Unbound", "No default", () => { });
        }
    }

    static Probe Started(bool two = false, bool none = false)
    {
        var plugin = new Probe(two, none);
        plugin.Initialize();
        return plugin;
    }

    static Vector2 DrawAndMeasureLastItem(string window, Plugin plugin)
    {
        var centre = Vector2.Zero;
        UiFrame.Run(window, () =>
        {
            Controls.HotkeyTable(plugin);
            centre = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        });
        return centre;
    }

    [Fact]
    public void APluginWithNoHotkeysDrawsNothing()
    {
        var plugin = Started(none: true);
        int before = -1, after = -1;
        UiFrame.Run("hotkey table empty", () =>
        {
            var list = ImGui.GetWindowDrawList();
            before = list.VtxBuffer.Size;
            Controls.HotkeyTable(plugin);
            after = list.VtxBuffer.Size;
        });
        Assert.Equal(before, after);
    }

    [Fact]
    public void ResetPutsTheDefaultBack()
    {
        var plugin = Started();
        var binding = plugin.Hotkeys[0];
        binding.Value = new Hotkey(Key.F2);
        var reset = DrawAndMeasureLastItem("hotkey table reset", plugin);

        UiFrame.Click("hotkey table reset", reset, () => Controls.HotkeyTable(plugin));

        Assert.Equal(binding.Default, binding.Value);
    }

    [Fact]
    public void ResetIsGoneWhileTheBindingIsTheDefault()
    {
        var plugin = Started();
        var binding = plugin.Hotkeys[0];
        var withoutReset = DrawAndMeasureLastItem("hotkey table no reset", plugin);
        binding.Value = new Hotkey(Key.F2);
        var withReset = DrawAndMeasureLastItem("hotkey table no reset", plugin);

        Assert.True(withReset.X > withoutReset.X);
    }

    [Fact]
    public void TheConflictHookIsAskedForBoundKeysOnly()
    {
        var plugin = Started(two: true);
        List<(Hotkey Key, HotkeyBinding? Binding)> asked = [];
        Controls.HotkeyConflict = (key, binding) =>
        {
            asked.Add((key, binding));
            return null;
        };
        try
        {
            UiFrame.Run("hotkey table conflict", () => Controls.HotkeyTable(plugin));
        }
        finally
        {
            Controls.HotkeyConflict = (_, _) => null;
        }

        Assert.Contains(asked, entry => entry.Key == new Hotkey(Key.F1) && entry.Binding == plugin.Hotkeys[0]);
        Assert.DoesNotContain(asked, entry => entry.Binding == plugin.Hotkeys[1]);
    }
}
