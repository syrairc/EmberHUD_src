using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class WidgetSmokeTests
{
    [Fact]
    public void TrailingGroupEndsOnTheRightEdgeFromTheSecondFrame()
    {
        float right = 0, groupRight = 0;
        void Body()
        {
            right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
            ImGui.TextUnformatted("left");
            Layout.BeginTrailing("tail");
            ImGui.Button("right");
            Layout.EndTrailing();
            groupRight = ImGui.GetItemRectMax().X;
        }
        UiFrame.Run("trailing", Body);
        UiFrame.Run("trailing", Body);
        Assert.Equal(right, groupRight, 1f);
    }

    [Fact]
    public void TablesRowsAndDimsBalance()
    {
        var began = false;
        var rows = 0;
        var columns = 0;
        var alpha = new List<float>();
        UiFrame.Run("tables", () =>
        {
            began = Tables.Begin("table", [("Name", 0), ("Value", 80)]);
            if (!began) return;
            columns = ImGui.TableGetColumnCount();
            using (Tables.Row(0, selected: true))
            {
                rows++;
                alpha.Add(ImGui.GetStyle().Alpha);
            }
            using (Tables.Row(1))
            using (Tables.Dim(true))
            {
                rows++;
                alpha.Add(ImGui.GetStyle().Alpha);
            }
            alpha.Add(ImGui.GetStyle().Alpha);
            Tables.End();
        });
        Assert.True(began);
        Assert.Equal(2, columns);
        Assert.Equal(2, rows);
        Assert.Equal([1f, 0.55f, 1f], alpha.Select(a => MathF.Round(a, 2)));
    }

    [Fact]
    public void TabBarRunsTheSelectedTabAndRollsUpItsChange()
    {
        var ran = new List<string>();
        var changed = false;
        bool Tabs() => Windows.TabBar("tabs", ("A", () => { ran.Add("A"); return true; }), ("B", () => { ran.Add("B"); return false; }));
        UiFrame.Run("tab bar", () => changed |= Tabs());
        UiFrame.Run("tab bar", () => changed |= Tabs());
        Assert.Contains("A", ran);
        Assert.DoesNotContain("B", ran);
        Assert.True(changed);
    }

    [Fact]
    public void MasterDetailDrawsBothSides()
    {
        var sides = new List<string>();
        UiFrame.Run("master detail", () => Windows.MasterDetail("md", 190, () => sides.Add("left"), () => sides.Add("right")));
        Assert.Equal(["left", "right"], sides);
    }

    [Fact]
    public void TwoColumnStacksWhenNarrowAndSplitsWhenWide()
    {
        var calls = 0;
        var changed = false;
        UiFrame.Run("two column", () =>
        {
            Windows.TwoColumn("narrow", 5000, () => { calls++; return false; }, () => { calls++; return false; });
            changed = Windows.TwoColumn("wide", 100, () => { calls++; return false; }, () => { calls++; return true; });
        });
        Assert.Equal(4, calls);
        Assert.True(changed);
    }

    [Fact]
    public void FilterPaneDrawsPercentTextLiterally()
    {
        static int Vertices(string title, string hint)
        {
            var filter = "";
            var vertices = 0;
            UiFrame.Run("filter pane", () =>
            {
                var list = ImGui.GetWindowDrawList();
                var start = list.VtxBuffer.Size;
                Windows.FilterPane("pane", title, 3, ref filter, hint, () => { });
                vertices = list.VtxBuffer.Size - start;
            });
            return vertices;
        }
        Assert.Equal(Vertices("50x off xx", "hint 10x xx"), Vertices("50% off %s", "hint 10% %d"));
    }

    [Fact]
    public void ConfirmModalStaysOpenAndUnactedUntilAButtonIsPressed()
    {
        var opened = false;
        var acted = true;
        var confirmed = true;
        UiFrame.Run("confirm", () => ImGui.OpenPopup("sure"));
        UiFrame.Run("confirm", () =>
        {
            acted = Windows.ConfirmModal("sure", "Reset 100% of it?", out confirmed);
            opened = ImGui.IsPopupOpen("sure");
        });
        Assert.True(opened);
        Assert.False(acted);
        Assert.False(confirmed);
    }
}
