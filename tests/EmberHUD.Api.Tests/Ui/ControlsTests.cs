using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class ControlsTests : IDisposable
{
    Hotkey _value = new(Key.F8);
    bool _changed;
    Vector2 _button;

    public ControlsTests()
    {
        Input.CancelCapture();
        TestInput.Hold();
        TestInput.Hold();
    }

    public void Dispose()
    {
        Input.CancelCapture();
        TestInput.Hold();
    }

    void Body()
    {
        _changed |= Controls.Hotkey("", ref _value, "toggle");
        _button = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
    }

    void ClickTheButton(string window)
    {
        UiFrame.Run(window, Body);
        UiFrame.Click(window, _button, Body);
    }

    [Fact]
    public void AClickStartsACaptureAndTheNextKeyBinds()
    {
        ClickTheButton("hotkey capture");
        Assert.True(Input.Capturing);

        TestInput.Hold(Key.Ctrl, Key.F5);
        UiFrame.Run("hotkey capture", Body);

        Assert.True(_changed);
        Assert.Equal(new Hotkey(Key.F5, KeyModifiers.Ctrl), _value);
        Assert.False(Input.Capturing);
    }

    [Fact]
    public void EscapeLeavesTheBindingAlone()
    {
        ClickTheButton("hotkey escape");
        TestInput.Hold(Key.Escape);
        UiFrame.Run("hotkey escape", Body);

        Assert.False(_changed);
        Assert.Equal(new Hotkey(Key.F8), _value);
        Assert.False(Input.Capturing);
    }

    [Fact]
    public void AHotkeyWithALabelDrawsTheLabelBesideTheButton() => UiFrame.Run("hotkey label", () =>
    {
        var value = new Hotkey(Key.F12, KeyModifiers.Ctrl);
        Assert.False(Controls.Hotkey("Quit", ref value));
        Assert.True(ImGui.GetItemRectMin().X >= ImGui.CalcItemWidth());
    });

    [Fact]
    public void TheButtonShowsTheBoundKeyAndItsModifiers()
    {
        var f8 = Drawn("hotkey keys", new Hotkey(Key.F8), "keys");
        var f9 = Drawn("hotkey keys", new Hotkey(Key.F9), "keys");
        var ctrlF5 = Drawn("hotkey keys", new Hotkey(Key.F5, KeyModifiers.Ctrl), "keys");
        var altF5 = Drawn("hotkey keys", new Hotkey(Key.F5, KeyModifiers.Alt), "keys");
        var f5 = Drawn("hotkey keys", new Hotkey(Key.F5), "keys");

        Assert.Equal(f8, Drawn("hotkey keys", new Hotkey(Key.F8), "keys"));
        Assert.NotEqual(f8, f9);
        Assert.NotEqual(ctrlF5, altF5);
        Assert.NotEqual(ctrlF5, f5);
    }

    [Fact]
    public void TheButtonPromptsWhileCapturingAndShowsTheKeyAgainAfterEscape()
    {
        var idle = Drawn("hotkey prompt", _value, "toggle");
        ClickTheButton("hotkey prompt");
        Assert.True(Input.Capturing);
        UiFrame.Run("hotkey prompt", Body);
        Assert.NotEqual(idle, Drawn("hotkey prompt", _value, "toggle"));

        TestInput.Hold(Key.Escape);
        UiFrame.Run("hotkey prompt", Body);
        Assert.False(Input.Capturing);
        Assert.Equal(idle, Drawn("hotkey prompt", _value, "toggle"));
    }

    [Fact]
    public void ClickingAnotherPickerWhileOneCapturesDoesNotStealTheCapture()
    {
        Hotkey first = new(Key.F8), second = new(Key.F9);
        Vector2 firstAt = default, secondAt = default;
        var firstIdle = Drawn("hotkey two", first, "first");
        var secondIdle = Drawn("hotkey two", second, "second");
        void Both()
        {
            Controls.Hotkey("", ref first, "first");
            firstAt = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            Controls.Hotkey("", ref second, "second");
            secondAt = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run("hotkey two", Both);
        UiFrame.Click("hotkey two", firstAt, Both);
        UiFrame.Click("hotkey two", secondAt, Both);
        UiFrame.Run("hotkey two", Both);

        Assert.True(Input.Capturing);
        Assert.NotEqual(firstIdle, Drawn("hotkey two", first, "first"));
        Assert.Equal(secondIdle, Drawn("hotkey two", second, "second"));
        Assert.Equal(new Hotkey(Key.F8), first);
        Assert.Equal(new Hotkey(Key.F9), second);
    }

    [Fact]
    public void LogViewTakesLinesWithPercentSigns() => UiFrame.Run("log view", () =>
    {
        Controls.LogView("log", ["first", "100% done %s %d"], 120);
        Assert.Equal(120, ImGui.GetItemRectSize().Y);
        Controls.LogView("rest", [], 0);
        Assert.True(ImGui.GetItemRectSize().Y > 120);
    });

    static List<(Vector2 Pos, Vector2 Uv)> Drawn(string window, Hotkey value, string id)
    {
        List<(Vector2 Pos, Vector2 Uv)> drawn = [];
        UiFrame.Run(window, () => drawn = UiFrame.Drawn(() => Controls.Hotkey("", ref value, id)).Select(v => (v.Pos, v.Uv)).ToList());
        return drawn;
    }

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
