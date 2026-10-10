using Hexa.NET.ImGui;
using TorchlightSDK.Ui;

namespace TorchlightSDK.Api.Tests;

public class BarsTests
{
    [Fact]
    public void BarsDrawOneBalancedFrame() => UiFrame.Run("test", () =>
    {
        Bars.Progress(0.4f, "40%");
        Bars.Progress(-1f);
        Bars.Progress(2f, "");
        Bars.Strip([0xFF0000FFu, 0xFF00FF00u, 0xFFFF0000u]);
        Bars.Strip([]);
    });

    [Fact]
    public void ProgressAndStripAdvanceTheCursorByTheirHeight() => UiFrame.Run("test", () =>
    {
        var height = ImGui.GetTextLineHeight() * 2f;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var start = ImGui.GetCursorPosY();
        Bars.Progress(0.5f, null, height);
        Assert.Equal(start + height + spacing, ImGui.GetCursorPosY(), 1e-3f);
        Bars.Strip([0xFF0000FFu], height);
        Assert.Equal(start + 2 * (height + spacing), ImGui.GetCursorPosY(), 1e-3f);
    });

    [Fact]
    public void AnEmptyStripLeavesTheCursorAlone() => UiFrame.Run("test", () =>
    {
        var start = ImGui.GetCursorPosY();
        Bars.Strip([]);
        Assert.Equal(start, ImGui.GetCursorPosY());
    });
}
