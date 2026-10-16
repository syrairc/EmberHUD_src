using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class TexturePickerTests : IDisposable
{
    const string Window = "texpicker";

    Func<string, Sprite?> preview = _ => null;
    Color? tint;
    string value = "";
    bool changed;
    string[] names = [];
    Vector2 origin;

    void Body()
    {
        origin = ImGui.GetCursorScreenPos();
        changed |= TexturePicker.Combo("Icon", ref value, names, preview, "icon", tint);
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

    [Fact]
    public void PickingFirstNameWhileValueIsUnknownChanges()
    {
        names = ["a", "b"];
        value = "zzz";
        Open();
        PickRow(0);
        Assert.True(changed);
        Assert.Equal("a", value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewDrawsIntoThePopupListNotTheBackground(bool tinted)
    {
        names = ["a", "b"];
        value = "a";
        tint = tinted ? Color.Red : null;
        var sprite = new Sprite(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One);
        var previewed = 0;
        var inPopupList = true;
        preview = _ =>
        {
            previewed++;
            inPopupList &= !ImGui.GetWindowDrawList().Equals(ImGui.GetBackgroundDrawList());
            return sprite;
        };
        Open();
        Idle();
        Assert.True(previewed >= 2);
        Assert.True(inPopupList);
    }
}
