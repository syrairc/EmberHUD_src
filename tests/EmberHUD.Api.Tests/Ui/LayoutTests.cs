using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void SectionDrawsALabelledSeparatorAndRestoresTheFont()
    {
        float before = 0, after = 0, height = 0;
        UiFrame.Run("section", () =>
        {
            before = ImGui.GetFontSize();
            Controls.Section("Fonts");
            height = ImGui.GetItemRectSize().Y;
            after = ImGui.GetFontSize();
        });
        Assert.True(height > 0);
        Assert.Equal(before, after);
    }

    [Fact]
    public void TabBarRunsTheSelectedTabAndRollsUpItsChange()
    {
        var ran = new List<string>();
        var changed = false;
        bool Tabs() => Layout.TabBar("tabs", ("A", () => { ran.Add("A"); return true; }), ("B", () => { ran.Add("B"); return false; }));
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
        UiFrame.Run("master detail", () => Layout.MasterDetail("md", 190, () => sides.Add("left"), () => sides.Add("right")));
        Assert.Equal(["left", "right"], sides);
    }

    [Fact]
    public void TwoColumnStacksWhenNarrowAndSplitsWhenWide()
    {
        var calls = 0;
        var changed = false;
        UiFrame.Run("two column", () =>
        {
            Layout.TwoColumn("narrow", 5000, () => { calls++; return false; }, () => { calls++; return false; });
            changed = Layout.TwoColumn("wide", 100, () => { calls++; return false; }, () => { calls++; return true; });
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
            UiFrame.Run("filter pane", () => vertices = UiFrame.Drawn(() => Layout.FilterPane("pane", title, 3, ref filter, hint, () => { })).Count);
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
            acted = Layout.ConfirmModal("sure", "Reset 100% of it?", out confirmed);
            opened = ImGui.IsPopupOpen("sure");
        });
        Assert.True(opened);
        Assert.False(acted);
        Assert.False(confirmed);
    }

    [Fact]
    public void TrailingWidthIsCachedPerIdStack()
    {
        float right = 0, a = 0, b = 0;
        for (var frame = 0; frame < 3; frame++)
            UiFrame.Run("test", () =>
            {
                right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
                a = Trailing("a", "x");
                b = Trailing("b", "a much longer label");
            });
        Assert.Equal(right, a, 1f);
        Assert.Equal(right, b, 1f);
    }

    static float Trailing(string scope, string button)
    {
        ImGui.PushID(scope);
        ImGui.TextUnformatted(scope);
        var trail = Layout.BeginTrailing("##head");
        ImGui.Button(button);
        Layout.EndTrailing(trail);
        ImGui.PopID();
        return ImGui.GetItemRectMax().X;
    }

    [Fact]
    public void NestedTrailingRecordsEachWidthUnderItsOwnId()
    {
        float outerAfter = 0, innerAfter = 0;
        void Body()
        {
            var outer = Layout.BeginTrailing("outer");
            ImGui.TextUnformatted("outer outer outer outer");
            var inner = Layout.BeginTrailing("inner");
            ImGui.TextUnformatted("in");
            Layout.EndTrailing(inner);
            Layout.EndTrailing(outer);
        }
        UiFrame.Run("nested trailing", Body);
        UiFrame.Run("nested trailing", Body);
        UiFrame.Run("nested trailing", () =>
        {
            var outer = Layout.BeginTrailing("outer");
            outerAfter = ImGui.GetCursorPosX();
            ImGui.TextUnformatted("outer outer outer outer");
            var inner = Layout.BeginTrailing("inner");
            innerAfter = ImGui.GetCursorPosX();
            ImGui.TextUnformatted("in");
            Layout.EndTrailing(inner);
            Layout.EndTrailing(outer);
        });

        Assert.True(outerAfter < innerAfter, $"the inner group sits further right than the outer one ({outerAfter} vs {innerAfter})");
    }
}
