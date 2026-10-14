using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class SettingTests
{
    enum Mode { Fit, Follow }

    static Vector2 Centre() => (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;

    [Fact]
    public void ClickingTheCheckboxFlipsTheValue()
    {
        var on = false;
        var changed = false;
        var at = Vector2.Zero;
        void Body()
        {
            changed |= Setting.Checkbox("Show", ref on, "tip");
            at = Centre();
        }

        UiFrame.Run("setting checkbox", Body);
        UiFrame.Click("setting checkbox", at, Body);

        Assert.True(changed);
        Assert.True(on);
    }

    [Fact]
    public void ClickingTheSliderMovesTheValue()
    {
        var whole = 0;
        var changed = false;
        var at = Vector2.Zero;
        void Body()
        {
            changed |= Setting.Slider("Width", ref whole, 0, 100);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            at = new Vector2(min.X + (max.X - min.X) * 0.4f, (min.Y + max.Y) / 2);
        }

        UiFrame.Run("setting slider", Body);
        UiFrame.Click("setting slider", at, Body);

        Assert.True(changed);
        Assert.InRange(whole, 25, 55);
    }

    [Fact]
    public void TheWidgetStartsAfterTheLabelColumn()
    {
        float start = 0, avail = 0, widget = 0;
        var on = false;

        UiFrame.Run("setting column", () =>
        {
            start = ImGui.GetCursorScreenPos().X;
            avail = ImGui.GetContentRegionAvail().X;
            Setting.Checkbox("Show", ref on);
            widget = ImGui.GetItemRectMin().X;
        });

        Assert.Equal(start + MathF.Max(160, avail * 0.4f), widget, 1f);
    }

    [Fact]
    public void TheWidgetStartsAfterTheLabelWhenTheLabelIsWiderThanTheColumn()
    {
        float labelRight = 0, widget = 0;
        var on = false;
        var label = new string('W', 80);

        UiFrame.Run("setting long label", () =>
        {
            var start = ImGui.GetCursorScreenPos().X;
            Setting.Checkbox(label, ref on);
            labelRight = start + ImGui.CalcTextSize(label).X;
            widget = ImGui.GetItemRectMin().X;
        });

        Assert.True(widget > labelRight, $"{widget} <= {labelRight}");
    }

    [Fact]
    public void ANullStringInputDrawsAndBecomesEmpty()
    {
        string? text = null;

        UiFrame.Run("setting null input", () => Setting.Input("Text", ref text!));

        Assert.Equal("", text);
    }

    [Fact]
    public void TheIdDefaultsToTheLabelAndIdSeparatesTwoSameLabelRows()
    {
        bool a = false, b = false, c = false;
        uint first = 0, second = 0, third = 0;

        UiFrame.Run("setting ids", () =>
        {
            Setting.Checkbox("Show", ref a);
            first = ImGui.GetItemID();
            Setting.Checkbox("Show", ref b);
            second = ImGui.GetItemID();
            Setting.Checkbox("Show", ref c, id: "other");
            third = ImGui.GetItemID();
        });

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    [Theory]
    [InlineData(0f, 10f, "%.3f")]
    [InlineData(0.002f, 4f, "%.3f")]
    [InlineData(1f, 1000f, "%.3f")]
    [InlineData(0.5f, 6f, "%.3f")]
    [InlineData(0f, 100f, "%.1f")]
    [InlineData(-180f, 180f, "%.1f")]
    [InlineData(1f, 99f, "%.1f")]
    public void TheFloatFormatFollowsTheRange(float min, float max, string format) => Assert.Equal(format, Setting.FloatFormat(min, max));

    [Fact]
    public void EveryHelperDrawsABalancedFrameAndChangesNothingUntouched()
    {
        var on = true;
        var whole = 3;
        var fraction = 0.5f;
        var colour = Color.FromArgb(255, 200, 100, 50);
        var key = new Hotkey(Key.F8);
        var text = "map";
        var mode = Mode.Follow;
        var style = TextStyle.Hud();
        var changed = false;

        UiFrame.Run("setting all", () =>
        {
            changed |= Setting.Checkbox("Check", ref on);
            changed |= Setting.Slider("Int", ref whole, 0, 10);
            changed |= Setting.Slider("Float", ref fraction, 0, 1, fmt: "%.2f", log: false);
            changed |= Setting.Color("Colour", ref colour, alpha: false);
            changed |= Setting.Hotkey("Key", ref key);
            changed |= Setting.Input("Text", ref text, max: 64);
            changed |= Setting.Choice("Mode", ref mode);
            changed |= Setting.Style("Style", style);
        });

        Assert.False(changed);
        Assert.Equal(Mode.Follow, mode);
        Assert.Equal("map", text);
    }

    [Fact]
    public void TheActionTabBarRunsTheOpenTabBody()
    {
        var first = 0;
        var second = 0;

        UiFrame.Run("setting tabs", () => Windows.TabBar("tabs", ("One", () => first++), ("Two", () => second++)));

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }
}
