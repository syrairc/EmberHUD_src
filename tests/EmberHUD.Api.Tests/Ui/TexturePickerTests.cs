using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class TexturePickerTests : IDisposable
{
    const string Window = "texpicker";

    string value = "";
    bool changed;
    string[] names = [];
    Vector2 origin;

    void Body()
    {
        origin = ImGui.GetCursorScreenPos();
        changed |= TexturePicker.Combo("Icon", ref value, names, _ => null, "icon");
    }

    void Idle() => UiFrame.Run(Window, Body);

    void Open()
    {
        Idle();
        UiFrame.Click(Window, origin + new Vector2(20f, UiFrame.Style.FrameHeight / 2), Body);
    }

    void PickRow(int row)
    {
        var y = origin.Y + UiFrame.Style.FrameHeight + UiFrame.Style.WindowPadding.Y + row * (UiFrame.Style.FontSize + UiFrame.Style.Spacing.Y) + UiFrame.Style.FontSize / 2;
        UiFrame.Click(Window, new Vector2(origin.X + 10f, y), Body);
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void PickingChangesTheValue()
    {
        names = ["a", "b", "c"];
        value = "a";
        Open();
        PickRow(1);
        Assert.True(changed);
        Assert.Equal("b", value);
    }

    [Fact]
    public void UnknownValueDoesNotThrowAndKeepsTheValue()
    {
        names = ["a", "b"];
        value = "zzz";
        Open();
        Idle();
        Assert.False(changed);
        Assert.Equal("zzz", value);
    }

    [Fact]
    public void EmptyNamesDoesNotThrow()
    {
        names = [];
        value = "x";
        Open();
        Idle();
        Assert.False(changed);
        Assert.Equal("x", value);
    }
}
