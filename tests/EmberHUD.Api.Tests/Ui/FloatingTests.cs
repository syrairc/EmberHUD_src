using System.Drawing;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class FloatingTests : IDisposable
{
    sealed class Button
    {
        public Vector2 Pos;
        public int Clicks;
        public ImGuiMouseCursor Cursor;
        public List<ImDrawVert> Verts = [];
    }

    static string FloatName(string id) => $"##float_{ImGui.GetID(id):X8}";

    static Action ButtonFrame(string id, Button b, string label = "Toast me", Vector2 size = default) => () =>
    {
        if (Floating.Button(id, label, ref b.Pos, size)) b.Clicks++;
        b.Cursor = ImGui.GetMouseCursor();
        var window = ImGuiP.FindWindowByName(FloatName(id));
        b.Verts = window.IsNull ? [] : UiFrame.Verts(window.DrawList);
    };

    static Vector2 WindowMinSize()
    {
        Vector2 min = default;
        UiFrame.Run(UiFrame.NextId("minsize"), () => min = ImGui.GetStyle().WindowMinSize);
        return min;
    }

    static Vector2 Measure(string label)
    {
        Vector2 size = default;
        UiFrame.Run(UiFrame.NextId("measure"), () => size = ImGui.CalcTextSize(label) + ImGui.GetStyle().FramePadding * 2f);
        return size;
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void AFloatingButtonDraggedOffScreenIsClampedBack()
    {
        var pos = new Vector2(5000, 5000);
        var clicked = true;
        Vector2 expected = default;
        TestFrame.Run(() =>
        {
            var vp = ImGui.GetMainViewport();
            var size = ImGui.CalcTextSize("Toast me") + ImGui.GetStyle().FramePadding * 2f;
            expected = vp.Pos + vp.Size - size;
            clicked = Floating.Button(UiFrame.NextId("offscreen"), "Toast me", ref pos);
        });
        Assert.False(clicked);
        Assert.Equal(expected, pos);
        Assert.Equal(UiFrame.Io().DisplaySize, pos + Measure("Toast me"));
    }

    [Fact]
    public void AFloatingButtonDraggedAboveAndLeftIsClampedToTheViewportOrigin()
    {
        var pos = new Vector2(-300, -300);
        Vector2 viewport = default;
        TestFrame.Run(() =>
        {
            Floating.Button(UiFrame.NextId("topleft"), "Toast me", ref pos);
            viewport = ImGui.GetMainViewport().Pos;
        });
        Assert.Equal(viewport, pos);
    }

    [Fact]
    public void AFloatingButtonWiderThanTheViewportPinsToTheLeftInsteadOfThrowing()
    {
        var display = UiFrame.Io().DisplaySize;
        var pos = new Vector2(400, 400);
        TestFrame.Run(() => Floating.Button(UiFrame.NextId("wide"), "Toast me", ref pos, new Vector2(display.X + 100, 30)));
        Assert.Equal(0f, pos.X);
        Assert.Equal(400f, pos.Y);
    }

    [Fact]
    public void AFloatingButtonTallerThanTheViewportPinsToTheTop()
    {
        var display = UiFrame.Io().DisplaySize;
        var pos = new Vector2(400, 400);
        TestFrame.Run(() => Floating.Button(UiFrame.NextId("tall"), "Toast me", ref pos, new Vector2(30, display.Y + 100)));
        Assert.Equal(400f, pos.X);
        Assert.Equal(0f, pos.Y);
    }

    [Fact]
    public void AFloatingButtonKeepsAnInsideSpotUntouched()
    {
        var pos = new Vector2(200, 150);
        TestFrame.Run(() => Floating.Button(UiFrame.NextId("inside"), "Toast me", ref pos));
        Assert.Equal(new Vector2(200, 150), pos);
    }

    [Fact]
    public void AFloatingButtonSizesToTheLabelPlusFramePaddingUnlessAGivenSizeIsPositiveOnBothAxes()
    {
        var measured = Vector2.Max(Measure("Toast me"), WindowMinSize());
        var wanted = new Vector2(150, 40);
        Vector2 measuredWindow = default, givenWindow = default;
        TestFrame.Run(() =>
        {
            var pos = new Vector2(200, 150);
            var id = UiFrame.NextId("sized");
            Floating.Button(id, "Toast me", ref pos);
            measuredWindow = ImGuiP.FindWindowByName(FloatName(id)).Size;
            id = UiFrame.NextId("given");
            Floating.Button(id, "Toast me", ref pos, wanted);
            givenWindow = ImGuiP.FindWindowByName(FloatName(id)).Size;
        });
        Assert.Equal(measured, measuredWindow);
        Assert.Equal(wanted, givenWindow);
    }

    [Theory]
    [InlineData(0f, 40f)]
    [InlineData(150f, 0f)]
    [InlineData(-1f, -1f)]
    [InlineData(0f, 0f)]
    public void ButtonSizeFallsBackToTheLabelPlusPaddingWhenAnAxisIsNotPositive(float x, float y)
    {
        var label = new Vector2(50, 13);
        var padding = new Vector2(4, 3);
        Assert.Equal(new Vector2(58, 19), Floating.ButtonSize(new Vector2(x, y), label, padding));
    }

    [Fact]
    public void ButtonSizeKeepsAPositiveSize() => Assert.Equal(new Vector2(150, 40), Floating.ButtonSize(new Vector2(150, 40), new Vector2(50, 13), new Vector2(4, 3)));

    [Fact]
    public void AFloatingButtonWindowFollowsASizeChange()
    {
        var id = UiFrame.NextId("resize");
        var host = UiFrame.NextId("resizehost");
        var pos = new Vector2(200, 150);
        Vector2 first = default, second = default;
        UiFrame.Run(host, () =>
        {
            Floating.Button(id, "Toast me", ref pos, new Vector2(150, 40));
            first = ImGuiP.FindWindowByName(FloatName(id)).Size;
        });
        UiFrame.Run(host, () =>
        {
            Floating.Button(id, "Toast me", ref pos, new Vector2(220, 60));
            second = ImGuiP.FindWindowByName(FloatName(id)).Size;
        });
        Assert.Equal(new Vector2(150, 40), first);
        Assert.Equal(new Vector2(220, 60), second);
    }

    [Fact]
    public void AFloatingButtonWindowSitsAtPosWithNoPadding()
    {
        var id = UiFrame.NextId("atpos");
        var b = new Button { Pos = new Vector2(220, 160) };
        Vector2 windowPos = default;
        TestFrame.Run(() =>
        {
            Floating.Button(id, "Toast me", ref b.Pos);
            windowPos = ImGuiP.FindWindowByName(FloatName(id)).Pos;
        });
        Assert.Equal(new Vector2(220, 160), windowPos);
    }

    [Fact]
    public void AFloatingButtonClickReturnsTrueOnceAndDoesNotMoveIt()
    {
        var win = UiFrame.NextId("clickwin");
        var id = UiFrame.NextId("click");
        var b = new Button { Pos = new Vector2(200, 150) };
        var center = b.Pos + Measure("Toast me") / 2f;
        UiFrame.Click(win, center, ButtonFrame(id, b));
        Assert.Equal(1, b.Clicks);
        Assert.Equal(new Vector2(200, 150), b.Pos);
    }

    [Fact]
    public void AFloatingButtonDragMovesItByTheMouseDeltaAndIsNotAClick()
    {
        var win = UiFrame.NextId("dragwin");
        var id = UiFrame.NextId("drag");
        var b = new Button { Pos = new Vector2(200, 150) };
        var frame = ButtonFrame(id, b);
        var size = Measure("Toast me");
        var center = b.Pos + size / 2f;
        var delta = new Vector2(60, 40);
        UiFrame.Drag(win, center, center + delta, frame);
        Assert.Equal(0, b.Clicks);
        Assert.Equal(new Vector2(200, 150) + delta, b.Pos);
        UiFrame.Click(win, b.Pos + size / 2f, frame);
        Assert.Equal(1, b.Clicks);
    }

    [Fact]
    public void AFloatingButtonPressThatNeverPassesTheDragThresholdStaysPutAndClicks()
    {
        var win = UiFrame.NextId("tinywin");
        var id = UiFrame.NextId("tiny");
        var b = new Button { Pos = new Vector2(200, 150) };
        var center = b.Pos + Measure("Toast me") / 2f;
        var threshold = UiFrame.Io().MouseDragThreshold;
        UiFrame.Drag(win, center, center + new Vector2(threshold / 3f, 0), ButtonFrame(id, b));
        Assert.Equal(1, b.Clicks);
        Assert.Equal(new Vector2(200, 150), b.Pos);
    }

    [Fact]
    public void AFloatingButtonCursorIsAHandWhenHoveredAndMoveWhenHeld()
    {
        var win = UiFrame.NextId("cursorwin");
        var id = UiFrame.NextId("cursor");
        var b = new Button { Pos = new Vector2(200, 150) };
        var frame = ButtonFrame(id, b);
        var center = b.Pos + Measure("Toast me") / 2f;
        UiFrame.Unhover();
        UiFrame.Run(win, frame);
        Assert.Equal(ImGuiMouseCursor.Arrow, b.Cursor);
        UiFrame.Hover(win, center, frame);
        Assert.Equal(ImGuiMouseCursor.Hand, b.Cursor);
        UiFrame.Press(win, center, frame);
        Assert.Equal(ImGuiMouseCursor.ResizeAll, b.Cursor);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void AFloatingButtonFillsWithTheButtonColourForIdleHoveredAndHeld()
    {
        var win = UiFrame.NextId("colourwin");
        var id = UiFrame.NextId("colour");
        var b = new Button { Pos = new Vector2(200, 150) };
        var frame = ButtonFrame(id, b);
        var center = b.Pos + Measure("Toast me") / 2f;
        uint idle = 0, hovered = 0, held = 0;
        UiFrame.Run(win, () =>
        {
            idle = ImGui.GetColorU32(ImGuiCol.Button);
            hovered = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            held = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        });
        Assert.Equal(3, new[] { idle, hovered, held }.Distinct().Count());
        UiFrame.Unhover();
        UiFrame.Run(win, frame);
        Assert.Equal((true, false, false), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
        UiFrame.Hover(win, center, frame);
        Assert.Equal((false, true, false), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
        UiFrame.Press(win, center, frame);
        Assert.Equal((false, false, true), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void AFloatingButtonDrawsItsLabelAndABorder()
    {
        var win = UiFrame.NextId("labelwin");
        var size = new Vector2(150, 40);
        var plain = new Button { Pos = new Vector2(200, 150) };
        var labelled = new Button { Pos = new Vector2(200, 250) };
        uint border = 0;
        UiFrame.Unhover();
        UiFrame.Run(win, () => border = ImGui.GetColorU32(ImGuiCol.Border));
        UiFrame.Run(win, ButtonFrame(UiFrame.NextId("empty"), plain, "", size));
        UiFrame.Run(win, ButtonFrame(UiFrame.NextId("text"), labelled, "Toast me", size));
        Assert.True(labelled.Verts.Count > plain.Verts.Count);
        uint windowBg = 0;
        UiFrame.Run(win, () => windowBg = ImGui.GetColorU32(ImGuiCol.WindowBg));
        Assert.DoesNotContain(plain.Verts, v => v.Col == windowBg);
        Assert.Contains(plain.Verts, v => v.Col == border);
        var tolerance = 0f;
        UiFrame.Run(win, () => tolerance = ImGui.GetFontSize() / 2f);
        var shapeUvs = plain.Verts.Select(v => v.Uv).ToHashSet();
        var glyphs = labelled.Verts.Where(v => !shapeUvs.Contains(v.Uv)).ToList();
        Assert.NotEmpty(glyphs);
        Assert.InRange(glyphs.Min(v => v.Pos.X), labelled.Pos.X, labelled.Pos.X + size.X);
        Assert.InRange(glyphs.Max(v => v.Pos.X), labelled.Pos.X, labelled.Pos.X + size.X);
        Assert.InRange(glyphs.Min(v => v.Pos.Y), labelled.Pos.Y, labelled.Pos.Y + size.Y);
        Assert.InRange(glyphs.Max(v => v.Pos.Y), labelled.Pos.Y, labelled.Pos.Y + size.Y);
        Assert.InRange((glyphs.Min(v => v.Pos.Y) + glyphs.Max(v => v.Pos.Y)) / 2f, labelled.Pos.Y + size.Y / 2f - tolerance, labelled.Pos.Y + size.Y / 2f + tolerance);
        Assert.InRange((glyphs.Min(v => v.Pos.X) + glyphs.Max(v => v.Pos.X)) / 2f, labelled.Pos.X + size.X / 2f - tolerance, labelled.Pos.X + size.X / 2f + tolerance);
    }

    [Fact]
    public void AFloatingButtonWindowIsBorderlessUnmovableAndNeverTakesFocus()
    {
        var id = UiFrame.NextId("flags");
        var pos = new Vector2(200, 150);
        ImGuiWindowFlags flags = default;
        float padding = -1f;
        TestFrame.Run(() =>
        {
            Floating.Button(id, "Toast me", ref pos);
            var window = ImGuiP.FindWindowByName(FloatName(id));
            flags = window.Flags;
            padding = window.WindowPadding.X;
        });
        const ImGuiWindowFlags expected = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        Assert.Equal(expected, flags & expected);
        Assert.Equal(0f, padding);
    }

    [Fact]
    public void AFloatingButtonClampsAgainstAMovedViewport()
    {
        var viewportPos = new Vector2(50, 60);
        var viewportSize = new Vector2(800, 600);
        var far = new Vector2(5000, 5000);
        var near = new Vector2(-100, -100);
        var size = Measure("Toast me");
        TestFrame.Run(() =>
        {
            var vp = ImGui.GetMainViewport();
            var (oldPos, oldSize) = (vp.Pos, vp.Size);
            try
            {
                vp.Pos = viewportPos;
                vp.Size = viewportSize;
                Floating.Button(UiFrame.NextId("moved"), "Toast me", ref far);
                Floating.Button(UiFrame.NextId("movednear"), "Toast me", ref near);
            }
            finally
            {
                vp.Pos = oldPos;
                vp.Size = oldSize;
            }
        });
        Assert.Equal(viewportPos + viewportSize - size, far);
        Assert.Equal(viewportPos, near);
    }

    [Fact]
    public void TheSameFloatingIdUnderTwoIdScopesIsTwoWindowsAtTwoPositions()
    {
        var win = UiFrame.NextId("scopewin");
        Vector2 a = default, b = default, bareWindowPos = default;
        uint idA = 0, idB = 0;
        var posA = new Vector2(100, 100);
        var posB = new Vector2(400, 300);
        UiFrame.Run(win, () =>
        {
            ImGui.PushID("scopeA");
            Floating.Button("btn", "A", ref posA);
            ImGui.PopID();
            ImGui.PushID("scopeB");
            Floating.Button("btn", "B", ref posB);
            ImGui.PopID();
            ImGui.PushID("scopeA");
            var windowA = ImGuiP.FindWindowByName(FloatName("btn"));
            (a, idA) = (windowA.Pos, windowA.ID);
            ImGui.PopID();
            ImGui.PushID("scopeB");
            var windowB = ImGuiP.FindWindowByName(FloatName("btn"));
            (b, idB) = (windowB.Pos, windowB.ID);
            ImGui.PopID();
            var bare = ImGuiP.FindWindowByName(FloatName("btn"));
            bareWindowPos = bare.IsNull ? new Vector2(-1, -1) : bare.Pos;
        });
        Assert.NotEqual(idA, idB);
        Assert.Equal(new Vector2(100, 100), a);
        Assert.Equal(new Vector2(400, 300), b);
        Assert.Equal(new Vector2(-1, -1), bareWindowPos);
    }

    [Fact]
    public void ADragThatEndsUnseenInOneScopeDoesNotMakeTheNextScopesClickADrag()
    {
        var win = UiFrame.NextId("leakwin");
        var a = new Button { Pos = new Vector2(100, 100) };
        var b = new Button { Pos = new Vector2(400, 300) };
        Action Scoped(string scope, Button button) => () =>
        {
            ImGui.PushID(scope);
            ButtonFrame("btn", button)();
            ImGui.PopID();
        };
        var frameA = Scoped("scopeA", a);
        var frameB = Scoped("scopeB", b);
        var size = Measure("Toast me");
        var from = a.Pos + size / 2f;
        UiFrame.Press(win, from, frameA);
        UiFrame.MoveTo(win, from + new Vector2(20, 0), frameA);
        UiFrame.MoveTo(win, from + new Vector2(40, 0), frameA);
        UiFrame.Release(win, () => { });
        UiFrame.Click(win, b.Pos + size / 2f, frameB);
        Assert.Equal(1, b.Clicks);
    }
}
