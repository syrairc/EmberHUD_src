using EmberHUD.Ui;
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

    [Fact]
    public void ABodyThatLeavesAStyleColourPushedMakesRunThrow()
    {
        Assert.Throws<InvalidOperationException>(() => UiFrame.Run("leak colour", () => ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFFu)));
        UiFrame.Run("leak colour", () => ImGui.TextUnformatted("recovered"));
    }

    [Fact]
    public void DrawnReturnsOnlyWhatTheBodyAdded()
    {
        var window = UiFrame.NextId("drawn");
        var plainCount = 0;
        List<ImDrawVert> verts = [];
        UiFrame.Run(window, () =>
        {
            ImGui.Button("before");
            verts = UiFrame.Drawn(() => ImGui.Button("added"));
            plainCount = ImGui.GetWindowDrawList().VtxBuffer.Size;
        });
        Assert.NotEmpty(verts);
        Assert.True(plainCount > verts.Count);
        Assert.All(verts, v => Assert.True(v.Pos.X >= 0f));
    }

    [Fact]
    public void BoundsIsTheMinAndMaxOfTheVertexPositions()
    {
        List<ImDrawVert> verts = [new() { Pos = new Vector2(3, 9) }, new() { Pos = new Vector2(-2, 4) }, new() { Pos = new Vector2(7, -1) }];
        Assert.Equal((new Vector2(-2, -1), new Vector2(7, 9)), UiFrame.Bounds(verts));
    }

    [Fact]
    public void ActiveWindowFindsAnOpenPopupAndSkipsTheOneTheCallerExcludes()
    {
        var window = UiFrame.NextId("active");
        void Body()
        {
            if (ImGui.BeginPopup("first")) ImGui.EndPopup();
        }
        UiFrame.Run(window, () => ImGui.OpenPopup("first"));
        UiFrame.Run(window, Body);
        var found = Vector2.Zero;
        UiFrame.Run(window, () =>
        {
            Body();
            var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup);
            found = popup.IsNull ? new Vector2(-1) : popup.Pos;
            Assert.True(UiFrame.ActiveWindow(ImGuiWindowFlags.Popup, w => w.Pos != found).IsNull);
        });
        Assert.NotEqual(new Vector2(-1), found);
        UiFrame.CloseAllPopups();
    }

    [Fact]
    public void TooltipAtFindsTheTipOfAHoveredItemAndNothingAway()
    {
        var window = UiFrame.NextId("tip");
        void Body()
        {
            ImGui.Button("target");
            Text.Tip("50% done %s");
        }
        UiFrame.Run(window, Body);
        var tip = UiFrame.TooltipAt(window, new Vector2(20f, 40f), Body);
        Assert.NotNull(tip);
        Assert.True(tip.Size.X > 0f);
        Assert.NotEmpty(tip.Verts);
        Assert.Null(UiFrame.TooltipAt(window, new Vector2(700f, 500f), Body));
    }

    [Fact]
    public void CloseAllPopupsClosesAnOpenPopupAndReleasesTheMouse()
    {
        var window = UiFrame.NextId("close");
        void Body()
        {
            if (ImGui.BeginPopup("menu")) ImGui.EndPopup();
        }
        UiFrame.Run(window, () => ImGui.OpenPopup("menu"));
        UiFrame.Run(window, Body);
        Assert.True(UiFrame.PopupOpen(window, Body));
        UiFrame.Io().AddMouseButtonEvent(0, true);
        UiFrame.CloseAllPopups();
        Assert.False(UiFrame.PopupOpen(window, Body));
        Assert.False(ImGui.GetIO().MouseDown[0]);
    }

    [Fact]
    public void CloseAllPopupsLeavesNoActiveItemAfterAnInputTextWasClicked()
    {
        var window = UiFrame.NextId("input");
        var text = "";
        var box = Vector2.Zero;
        void Body()
        {
            ImGui.InputText("##box", ref text, 32);
            box = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run(window, Body);
        UiFrame.Click(window, box, Body);
        Assert.NotEqual(0u, ImGui.GetCurrentContext().ActiveId);
        UiFrame.CloseAllPopups();
        Assert.Equal(0u, ImGui.GetCurrentContext().ActiveId);
    }

    [Fact]
    public void WithColorsSetsTheColoursAndRestoresThemOnDispose()
    {
        UiFrame.Io();
        var style = ImGui.GetStyle();
        var before = style.Colors[(int)ImGuiCol.Header];
        var chosen = new Vector4(0.1f, 0.8f, 0.2f, 1f);
        using (UiFrame.WithColors((ImGuiCol.Header, chosen)))
            Assert.Equal(chosen, style.Colors[(int)ImGuiCol.Header]);
        Assert.Equal(before, style.Colors[(int)ImGuiCol.Header]);
    }

    [Fact]
    public void SameInkPassesWhenPercentSignsDrawLikeAnyOtherText() =>
        UiFrame.Run(UiFrame.NextId("ink"), () => UiFrame.SameInk(text => ImGui.TextUnformatted(text)));

    [Fact]
    public void SameInkFailsWhenAPercentSignChangesWhatIsDrawn()
    {
        var failure = Record.Exception(() => UiFrame.Run(UiFrame.NextId("ink bad"), () => UiFrame.SameInk(text => ImGui.TextUnformatted(text.Replace("%s", "")))));
        Assert.NotNull(failure);
    }

    [Fact]
    public void ItemRunsFindsEachButtonAlongARowInOrder()
    {
        List<(Vector2 Min, Vector2 Max)> rects = [];
        void Body()
        {
            rects.Clear();
            foreach (var label in new[] { "one", "two", "three" })
            {
                if (rects.Count > 0) ImGui.SameLine();
                ImGui.Button(label);
                rects.Add((ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
            }
        }
        var window = UiFrame.NextId("runs");
        UiFrame.Run(window, Body);
        var y = (rects[0].Min.Y + rects[0].Max.Y) / 2;

        var runs = UiFrame.ItemRuns(window, Body, new Vector2(0, y), new Vector2(400, y));

        Assert.Equal(3, runs.Count);
        for (var i = 0; i < 3; i++)
        {
            var centre = (rects[i].Min.X + rects[i].Max.X) / 2;
            Assert.InRange(runs[i].Centre.X, centre - 2f, centre + 2f);
        }
        Assert.True(runs[1].Min.X > runs[0].Max.X);
    }

    [Fact]
    public void ItemRunsFindsNothingOverTheEmptySpaceBesideTheButtons()
    {
        var window = UiFrame.NextId("runs empty");
        void Body() => ImGui.Button("only");
        UiFrame.Run(window, Body);
        Assert.Empty(UiFrame.ItemRuns(window, Body, new Vector2(300, 500), new Vector2(700, 500)));
        Assert.False(ImGui.GetIO().MouseDown[0]);
    }

    [Fact]
    public void ItemRunsIgnoresTheHostWindowsOwnBordersWhenTheSegmentRunsPastTheEdge()
    {
        var window = UiFrame.NextId("runs edge");
        var y = 0f;
        void Body()
        {
            ImGui.Button("only");
            y = (ImGui.GetItemRectMin().Y + ImGui.GetItemRectMax().Y) / 2;
        }
        UiFrame.Run(window, Body);
        Assert.Single(UiFrame.ItemRuns(window, Body, new Vector2(0, y), new Vector2(2000, y)));
    }
}
