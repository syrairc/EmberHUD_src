using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class ControlsTests : IDisposable
{
    readonly HashSet<int> _keys = [];
    Hotkey _value = new(Key.F8);
    bool _changed;
    Vector2 _button;

    public ControlsTests()
    {
        Input.KeyDown = _keys.Contains;
        Input.CancelCapture();
        Input.NewFrame(false);
        Input.NewFrame(false);
    }

    public void Dispose()
    {
        _keys.Clear();
        Input.CancelCapture();
        Input.NewFrame(false);
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

        _keys.UnionWith([0x11, 0x74]);
        Input.NewFrame(false);
        UiFrame.Run("hotkey capture", Body);

        Assert.True(_changed);
        Assert.Equal(new Hotkey(Key.F5, KeyModifiers.Ctrl), _value);
        Assert.False(Input.Capturing);
    }

    [Fact]
    public void EscapeLeavesTheBindingAlone()
    {
        ClickTheButton("hotkey escape");
        _keys.Add(0x1B);
        Input.NewFrame(false);
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

        _keys.Add(0x1B);
        Input.NewFrame(false);
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
        List<(Vector2, Vector2)> drawn = [];
        UiFrame.Run(window, () =>
        {
            var list = ImGui.GetWindowDrawList();
            var before = list.VtxBuffer.Size;
            Controls.Hotkey("", ref value, id);
            for (var i = before; i < list.VtxBuffer.Size; i++) drawn.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Uv));
        });
        return drawn;
    }
}
