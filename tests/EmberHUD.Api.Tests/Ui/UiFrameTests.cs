using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class UiFrameTests
{
    [Fact]
    public void BackToBackClicksOnTheSameSpotNeverCountAsADoubleClick()
    {
        var doubleClicks = 0;
        void Body()
        {
            ImGui.Button("target");
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) doubleClicks++;
        }
        var window = UiFrame.NextId("dbl");
        var at = new Vector2(20f, 20f);
        UiFrame.Run(window, Body);
        UiFrame.Click(window, at, Body);
        UiFrame.Click(window, at, Body);
        UiFrame.Click(window, at, Body);
        Assert.Equal(0, doubleClicks);
    }

    [Fact]
    public void AClickRunsOneSettleFrameThenTheFourClickFrames()
    {
        var window = UiFrame.NextId("frames");
        UiFrame.Run(window, () => { });
        var before = ImGui.GetFrameCount();
        UiFrame.Click(window, new Vector2(20f, 20f), () => { });
        Assert.Equal(4 + 1, ImGui.GetFrameCount() - before);
    }

    [Fact]
    public void ABodyThatLeavesAnIdPushedMakesRunThrow()
    {
        Assert.Throws<InvalidOperationException>(() => UiFrame.Run("leak id", () => ImGui.PushID("leak")));
        UiFrame.Run("leak id", () => ImGui.TextUnformatted("recovered"));
    }

    [Fact]
    public void ABodyThatLeavesATableOpenMakesRunThrow()
    {
        Assert.Throws<InvalidOperationException>(() => UiFrame.Run("leak table", () => ImGui.BeginTable("t", 1)));
        UiFrame.Run("leak table", () => ImGui.TextUnformatted("recovered"));
    }

    [Fact]
    public void ABalancedBodyWithIdsChildrenAndTablesDoesNotThrow() =>
        UiFrame.Run("balanced", () =>
        {
            ImGui.PushID("outer");
            ImGui.PushID(3);
            if (ImGui.BeginTable("t", 2))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("cell");
                ImGui.EndTable();
            }
            if (ImGui.BeginChild("child", new Vector2(0, 50))) ImGui.TextUnformatted("inside");
            ImGui.EndChild();
            ImGui.PopID();
            ImGui.PopID();
        });

    [Fact]
    public void ABodyExceptionComesBackUnchanged()
    {
        var error = Assert.Throws<FormatException>(() => UiFrame.Run("boom", () =>
        {
            ImGui.PushID("open when it throws");
            throw new FormatException("bad body");
        }));
        Assert.Equal("bad body", error.Message);
        UiFrame.Run("boom", () => ImGui.TextUnformatted("recovered"));
    }

    [Fact]
    public void TheStyleIsMeasuredOnceAndSharedByEveryCaller()
    {
        var first = UiFrame.Style;
        Assert.Same(first, UiFrame.Style);
        Assert.True(first.FrameHeight > 0f);
        Assert.True(first.FontSize > 0f);
    }

    [Fact]
    public void ButtonWidthIsWhatImGuiDrawsForAButton()
    {
        var drawn = 0f;
        UiFrame.Run(UiFrame.NextId("button width"), () =>
        {
            ImGui.Button("Save");
            drawn = ImGui.GetItemRectSize().X;
        });
        Assert.Equal(drawn, UiFrame.ButtonWidth("Save"), 0.01f);
    }

    [Fact]
    public void ReadingMeasuredThingsAgainRunsNoFrame()
    {
        _ = UiFrame.Style;
        _ = UiFrame.ButtonWidth("Cached label");
        _ = UiFrame.ReadModalMetrics();
        var frames = ImGui.GetFrameCount();
        _ = UiFrame.Style;
        _ = UiFrame.ButtonWidth("Cached label");
        _ = UiFrame.TextWidth("Cached label");
        _ = UiFrame.ReadModalMetrics();
        Assert.Equal(frames, ImGui.GetFrameCount());
    }
}
