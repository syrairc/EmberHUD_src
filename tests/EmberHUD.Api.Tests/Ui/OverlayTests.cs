using System.Drawing;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class OverlayTests : IDisposable
{
    static readonly Vector2 Origin = Vector2.Zero;
    static readonly Vector2 Size = new(100, 100);

    sealed class Panel(Vector2 origin, Vector2 size)
    {
        public Vector2 Origin = origin;
        public Vector2 Size = size;
        public int X = (int)origin.X;
        public int Y = (int)origin.Y;
        public int W = (int)size.X;
        public int H = (int)size.Y;
        public (bool hovered, Overlay.ResizeEdge edge, bool active) Last;
        public (bool hovered, bool onEdge, bool active) LastCenter;
        public ImGuiMouseCursor Cursor;
    }

    static Panel NewPanel() => new(new Vector2(100, 100), new Vector2(200, 80));

    static Panel NewBanner() => new(new Vector2(350, 40), new Vector2(300, 30)) { X = 500, Y = 40, W = 300 };

    static Action Frame(Overlay overlay, Panel p, string id = "panel", Overlay.ResizeEdge edges = Overlay.ResizeEdge.Corner, int minWidth = 60, int maxWidth = 4000, int minHeight = 40, int maxHeight = 2000) => () =>
    {
        p.Last = overlay.Handle(id, ref p.Origin, p.Size, ref p.X, ref p.Y, ref p.W, minWidth, maxWidth, ref p.H, minHeight, maxHeight, edges);
        p.Cursor = ImGui.GetMouseCursor();
    };

    static Action ShortFrame(Overlay overlay, Panel p, string id = "panel") => () =>
    {
        p.Last = overlay.Handle(id, ref p.Origin, p.Size, ref p.X, ref p.Y, ref p.W);
    };

    static Action CenterFrame(Overlay overlay, Panel p, string id = "banner", int minWidth = 60, int maxWidth = 4000) => () =>
    {
        p.LastCenter = overlay.HandleCenter(id, ref p.Origin, p.Size, ref p.X, ref p.Y, ref p.W, minWidth, maxWidth);
        p.Cursor = ImGui.GetMouseCursor();
    };

    static Vector2 Inside(Panel p) => p.Origin + new Vector2(20, 20);

    static Vector2 RightGrip(Panel p) => p.Origin + new Vector2(p.Size.X - 2, p.Size.Y / 2);

    static Vector2 BottomGrip(Panel p) => p.Origin + new Vector2(p.Size.X / 2, p.Size.Y - 2);

    static Vector2 CornerGrip(Panel p) => p.Origin + p.Size - new Vector2(2, 2);

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void HitTestInsideOffTheEdge() => Assert.Equal((true, false), Overlay.HitTest(new Vector2(50, 50), Origin, Size));

    [Fact]
    public void HitTestRightEdgeBand() => Assert.True(Overlay.HitTest(new Vector2(98, 50), Origin, Size).onEdge);

    [Fact]
    public void HitTestOutside() => Assert.False(Overlay.HitTest(new Vector2(200, 50), Origin, Size).hovered);

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(100f, 100f, true)]
    [InlineData(-1f, 50f, false)]
    [InlineData(50f, -1f, false)]
    [InlineData(101f, 50f, false)]
    [InlineData(50f, 101f, false)]
    public void HitTestBordersAreInclusive(float x, float y, bool expected) => Assert.Equal(expected, Overlay.HitTest(new Vector2(x, y), Origin, Size).hovered);

    [Fact]
    public void HitTestEdgeBandWidthIsTheCallersWhenGiven()
    {
        var mouse = new Vector2(80, 50);
        Assert.False(Overlay.HitTest(mouse, Origin, Size).onEdge);
        Assert.True(Overlay.HitTest(mouse, Origin, Size, 30f).onEdge);
        Assert.False(Overlay.HitTest(new Vector2(80, 150), Origin, Size, 30f).onEdge);
    }

    [Fact]
    public void HitEdgeBandWidthIsTheCallersWhenGiven()
    {
        var mouse = new Vector2(80, 50);
        Assert.Equal(Overlay.ResizeEdge.None, Overlay.HitEdge(mouse, Origin, Size, Overlay.ResizeEdge.Corner));
        Assert.Equal(Overlay.ResizeEdge.Right, Overlay.HitEdge(mouse, Origin, Size, Overlay.ResizeEdge.Corner, 30f));
        var below = new Vector2(50, 80);
        Assert.Equal(Overlay.ResizeEdge.None, Overlay.HitEdge(below, Origin, Size, Overlay.ResizeEdge.Corner));
        Assert.Equal(Overlay.ResizeEdge.Bottom, Overlay.HitEdge(below, Origin, Size, Overlay.ResizeEdge.Corner, 30f));
    }

    [Theory]
    [InlineData(98f, 50f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Right)]
    [InlineData(50f, 98f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Bottom)]
    [InlineData(98f, 98f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Corner)]
    [InlineData(98f, 98f, Overlay.ResizeEdge.Right, Overlay.ResizeEdge.Right)]
    [InlineData(50f, 98f, Overlay.ResizeEdge.Right, Overlay.ResizeEdge.None)]
    [InlineData(50f, 50f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.None)]
    [InlineData(200f, 50f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.None)]
    [InlineData(50f, 98f, Overlay.ResizeEdge.Bottom, Overlay.ResizeEdge.Bottom)]
    [InlineData(98f, 98f, Overlay.ResizeEdge.Bottom, Overlay.ResizeEdge.Bottom)]
    [InlineData(98f, 98f, Overlay.ResizeEdge.None, Overlay.ResizeEdge.None)]
    [InlineData(100f, 50f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Right)]
    [InlineData(50f, 100f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Bottom)]
    [InlineData(100f, 100f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.Corner)]
    [InlineData(101f, 98f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.None)]
    [InlineData(98f, 101f, Overlay.ResizeEdge.Corner, Overlay.ResizeEdge.None)]
    public void HitEdgeMasksToTheEnabledEdges(float x, float y, Overlay.ResizeEdge enabled, Overlay.ResizeEdge expected) =>
        Assert.Equal(expected, Overlay.HitEdge(new Vector2(x, y), Origin, Size, enabled));

    [Fact]
    public void EveryOverlayCallInOneUntouchedFrameLeavesTheImGuiStacksBalanced()
    {
        var overlay = new Overlay();
        var origin = new Vector2(100, 100);
        int x = 100, y = 100, width = 200, height = 80, cx = 500, cy = 40, cw = 300;
        var min = new Vector2(350, 40);

        TestFrame.Run(() =>
        {
            var (hovered, edge, active) = overlay.Handle("panel", ref origin, new Vector2(width, height), ref x, ref y, ref width);
            overlay.Handle("tall", ref origin, new Vector2(width, height), ref x, ref y, ref width, 60, 4000, ref height, 40, 2000);
            overlay.HandleCenter("banner", ref min, new Vector2(cw, 30), ref cx, ref cy, ref cw);
            overlay.Release("gone");
            Overlay.ClickBlocker("panel", origin, new Vector2(width, height));
            Overlay.DragHint(origin, origin + new Vector2(width, height), active, hovered, edge);
        });

        Assert.Equal((100, 100, 200, 80), (x, y, width, height));
        Assert.True(overlay.Movable);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    public void MovableIsNotLockedOrMoveHeld(bool locked, bool moveHeld, bool expected) => Assert.Equal(expected, new Overlay { Locked = locked, MoveHeld = moveHeld }.Movable);

    [Fact]
    public void DraggingMovesThePanelByTheMouseDeltaAndRoundsTheSettings()
    {
        var overlay = new Overlay();
        var p = new Panel(new Vector2(100.6f, 100.6f), new Vector2(200, 80));
        var grab = Inside(p);
        var delta = new Vector2(30, 20);
        UiFrame.Drag(UiFrame.NextId("drag"), grab, grab + delta, Frame(overlay, p));
        Assert.Equal(130.6f, p.Origin.X, 0.01f);
        Assert.Equal(120.6f, p.Origin.Y, 0.01f);
        Assert.Equal((131, 121), (p.X, p.Y));
        Assert.Equal((200, 80), (p.W, p.H));
    }

    [Fact]
    public void ADragIsActiveOnlyWhileTheMouseIsHeldAndOnlyForThePanelGrabbed()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var other = new Panel(new Vector2(500, 300), new Vector2(100, 100));
        var win = UiFrame.NextId("active");
        var otherFrame = Frame(overlay, other, "other");
        var frame = Frame(overlay, p);
        void Both()
        {
            frame();
            otherFrame();
        }
        UiFrame.Hover(win, Inside(p), Both);
        Assert.Equal((true, false), (p.Last.hovered, p.Last.active));
        UiFrame.Press(win, Inside(p), Both);
        Assert.Equal((true, true), (p.Last.hovered, p.Last.active));
        Assert.False(other.Last.active);
        UiFrame.MoveTo(win, Inside(p) + new Vector2(10, 10), Both);
        Assert.True(p.Last.active);
        UiFrame.Release(win, Both);
        Assert.False(p.Last.active);
    }

    [Fact]
    public void TwoOverlappingPanelsOnOneOverlayOnlyMoveTheFirstToGrabIt()
    {
        var overlay = new Overlay();
        var a = NewPanel();
        var b = NewPanel();
        var grab = Inside(a);
        var frameA = Frame(overlay, a, "a");
        var frameB = Frame(overlay, b, "b");
        UiFrame.Drag(UiFrame.NextId("overlap"), grab, grab + new Vector2(40, 25), () =>
        {
            frameA();
            frameB();
        });
        Assert.Equal((140, 125), (a.X, a.Y));
        Assert.Equal((100, 100), (b.X, b.Y));
        Assert.Equal(new Vector2(100, 100), b.Origin);
    }

    [Fact]
    public void APressOutsideThePanelStartsNothing()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("outside");
        var frame = Frame(overlay, p);
        var outside = p.Origin - new Vector2(30, 30);
        UiFrame.Press(win, outside, frame);
        UiFrame.MoveTo(win, outside + new Vector2(40, 40), frame);
        Assert.False(p.Last.active);
        UiFrame.Release(win, frame);
        Assert.Equal((100, 100), (p.X, p.Y));
        Assert.Equal((200, 80), (p.W, p.H));
    }

    [Fact]
    public void AfterAResizeEndsTheNextGrabStartsFresh()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("fresh");
        var frame = Frame(overlay, p);
        var grip = CornerGrip(p);
        UiFrame.Drag(win, grip, grip + new Vector2(20, 10), frame);
        Assert.Equal((218, 88), (p.W, p.H));
        Assert.False(p.Last.active);
        var grab = Inside(p);
        UiFrame.Drag(win, grab, grab + new Vector2(30, 15), frame);
        Assert.Equal((130, 115), (p.X, p.Y));
        Assert.Equal((218, 88), (p.W, p.H));
    }

    [Fact]
    public void DraggingTheCornerResizesBothAndMovesNothing()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = CornerGrip(p);
        UiFrame.Drag(UiFrame.NextId("corner"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        Assert.Equal((248, 108), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
        Assert.Equal(new Vector2(100, 100), p.Origin);
    }

    [Fact]
    public void DraggingTheRightEdgeResizesOnlyTheWidth()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = RightGrip(p);
        UiFrame.Drag(UiFrame.NextId("right"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        Assert.Equal((248, 80), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void DraggingTheBottomEdgeResizesOnlyTheHeight()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = BottomGrip(p);
        UiFrame.Drag(UiFrame.NextId("bottom"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        Assert.Equal((200, 108), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void AnEdgeMaskedOffMovesThePanelInstead()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = BottomGrip(p);
        UiFrame.Drag(UiFrame.NextId("masked"), grip, grip + new Vector2(0, 30), Frame(overlay, p, edges: Overlay.ResizeEdge.Right));
        Assert.Equal((200, 80), (p.W, p.H));
        Assert.Equal((100, 130), (p.X, p.Y));
    }

    [Fact]
    public void TheShortOverloadResizesOnlyTheWidthEvenFromTheCorner()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = CornerGrip(p);
        var win = UiFrame.NextId("short");
        var frame = ShortFrame(overlay, p);
        UiFrame.Hover(win, grip, frame);
        Assert.Equal(Overlay.ResizeEdge.Right, p.Last.edge);
        UiFrame.Drag(win, grip, grip + new Vector2(50, 30), frame);
        Assert.Equal((248, 80), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void ResizingIsClampedToTheCallersMinAndMax()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("clamp");
        var frame = Frame(overlay, p, minWidth: 150, maxWidth: 250, minHeight: 50, maxHeight: 90);
        var grip = CornerGrip(p);
        UiFrame.Drag(win, grip, grip + new Vector2(500, 500), frame);
        Assert.Equal((250, 90), (p.W, p.H));
        p.Size = new Vector2(p.W, p.H);
        grip = CornerGrip(p);
        UiFrame.Drag(win, grip, grip - new Vector2(400, 400), frame);
        Assert.Equal((150, 50), (p.W, p.H));
    }

    [Theory]
    [InlineData(0, ImGuiMouseCursor.ResizeEw)]
    [InlineData(1, ImGuiMouseCursor.ResizeNs)]
    [InlineData(2, ImGuiMouseCursor.ResizeNwse)]
    [InlineData(3, ImGuiMouseCursor.Arrow)]
    public void TheCursorFollowsTheEdgeUnderTheMouse(int where, ImGuiMouseCursor expected)
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var at = where switch { 0 => RightGrip(p), 1 => BottomGrip(p), 2 => CornerGrip(p), _ => Inside(p) };
        UiFrame.Hover(UiFrame.NextId("cursor"), at, Frame(overlay, p));
        Assert.Equal(expected, p.Cursor);
    }

    [Fact]
    public void ALiveResizeKeepsItsEdgeAndCursorEvenWhenTheMouseLeavesTheBand()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("sticky");
        var frame = Frame(overlay, p);
        UiFrame.Press(win, RightGrip(p), frame);
        UiFrame.MoveTo(win, p.Origin + new Vector2(60, 40), frame);
        Assert.Equal(Overlay.ResizeEdge.Right, p.Last.edge);
        Assert.Equal(ImGuiMouseCursor.ResizeEw, p.Cursor);
        Assert.True(p.Last.active);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ALockedPanelIgnoresADragAndReportsNothing()
    {
        var overlay = new Overlay { Locked = true };
        var p = NewPanel();
        var grab = Inside(p);
        UiFrame.Drag(UiFrame.NextId("locked"), grab, grab + new Vector2(40, 40), Frame(overlay, p));
        Assert.Equal((100, 100), (p.X, p.Y));
        Assert.Equal(new Vector2(100, 100), p.Origin);
        Assert.Equal((false, Overlay.ResizeEdge.None, false), p.Last);
    }

    [Fact]
    public void HoldingTheMoveKeyMakesALockedPanelGrabbable()
    {
        var overlay = new Overlay { Locked = true, MoveHeld = true };
        var p = NewPanel();
        var grab = Inside(p);
        UiFrame.Drag(UiFrame.NextId("moveheld"), grab, grab + new Vector2(40, 25), Frame(overlay, p));
        Assert.Equal((140, 125), (p.X, p.Y));
    }

    [Fact]
    public void LockingMidDragLetsTheLiveDragFinish()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("midlock");
        var frame = Frame(overlay, p);
        UiFrame.Press(win, Inside(p), frame);
        overlay.Locked = true;
        UiFrame.MoveTo(win, Inside(p) + new Vector2(30, 10), frame);
        Assert.Equal((130, 110), (p.X, p.Y));
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ReleaseDropsTheNamedPanelsDragButNotAnotherPanelsId()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("release");
        var frame = Frame(overlay, p);
        var grab = Inside(p);
        UiFrame.Press(win, grab, frame);
        UiFrame.MoveTo(win, grab + new Vector2(10, 0), frame);
        overlay.Release("elsewhere");
        UiFrame.MoveTo(win, grab + new Vector2(20, 0), frame);
        Assert.Equal(120, p.X);
        overlay.Release("panel");
        UiFrame.MoveTo(win, grab + new Vector2(60, 0), frame);
        Assert.Equal(120, p.X);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ReleaseDropsALiveResizeToo()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = UiFrame.NextId("releaseresize");
        var frame = Frame(overlay, p);
        var grip = RightGrip(p);
        UiFrame.Press(win, grip, frame);
        UiFrame.MoveTo(win, grip + new Vector2(20, 0), frame);
        Assert.Equal(218, p.W);
        overlay.Release("panel");
        UiFrame.MoveTo(win, grip + new Vector2(90, 0), frame);
        Assert.Equal(218, p.W);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ACenteredPanelMovesAroundItsCentreAndKeepsTheGrabOffset()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var grab = new Vector2(b.X, b.Y + 10);
        UiFrame.Drag(UiFrame.NextId("center"), grab, grab + new Vector2(40, 20), CenterFrame(overlay, b));
        Assert.Equal((540, 60), (b.X, b.Y));
        Assert.Equal(new Vector2(540 - b.Size.X / 2f, 60), b.Origin);
        Assert.Equal(300, b.W);
    }

    [Fact]
    public void TwoOverlappingCenteredPanelsOnlyMoveTheFirstToGrabIt()
    {
        var overlay = new Overlay();
        var a = NewBanner();
        var b = NewBanner();
        var frameA = CenterFrame(overlay, a, "a");
        var frameB = CenterFrame(overlay, b, "b");
        var grab = new Vector2(a.X, a.Y + 10);
        UiFrame.Drag(UiFrame.NextId("centeroverlap"), grab, grab + new Vector2(40, 20), () =>
        {
            frameA();
            frameB();
        });
        Assert.Equal((540, 60), (a.X, a.Y));
        Assert.Equal((500, 40), (b.X, b.Y));
        Assert.Equal(new Vector2(350, 40), b.Origin);
    }

    [Fact]
    public void AfterACenteredResizeAndDragEndTheNextGrabStartsFresh()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = UiFrame.NextId("centerfresh");
        var frame = CenterFrame(overlay, b);
        var grip = new Vector2(b.Origin.X + b.Size.X - 2, b.Origin.Y + 10);
        UiFrame.Drag(win, grip, grip + new Vector2(10, 0), frame);
        Assert.Equal(316, b.W);
        Assert.False(b.LastCenter.active);
        var grab = new Vector2(b.X, b.Y + 10);
        UiFrame.Drag(win, grab, grab + new Vector2(30, 0), frame);
        Assert.Equal(530, b.X);
        Assert.False(b.LastCenter.active);
        grab = new Vector2(b.X, b.Y + 10);
        UiFrame.Drag(win, grab, grab + new Vector2(-50, 0), frame);
        Assert.Equal(480, b.X);
    }

    [Fact]
    public void ACenteredPanelResizesSymmetricallyAndClamps()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = UiFrame.NextId("centerresize");
        var frame = CenterFrame(overlay, b, minWidth: 100, maxWidth: 500);
        var grip = new Vector2(b.Origin.X + b.Size.X - 2, b.Origin.Y + 10);
        UiFrame.Press(win, grip, frame);
        UiFrame.MoveTo(win, new Vector2(b.X + 120, grip.Y), frame);
        Assert.Equal(240, b.W);
        Assert.Equal(ImGuiMouseCursor.ResizeEw, b.Cursor);
        Assert.True(b.LastCenter.active);
        Assert.Equal((500, 40), (b.X, b.Y));
        UiFrame.MoveTo(win, new Vector2(b.X + 400, grip.Y), frame);
        Assert.Equal(500, b.W);
        UiFrame.MoveTo(win, new Vector2(b.X + 5, grip.Y), frame);
        Assert.Equal(100, b.W);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ACenteredPanelReportsHoverEdgeAndActive()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = UiFrame.NextId("centerreport");
        var frame = CenterFrame(overlay, b);
        UiFrame.Hover(win, new Vector2(b.X, b.Y + 10), frame);
        Assert.Equal((true, false, false), b.LastCenter);
        Assert.Equal(ImGuiMouseCursor.Arrow, b.Cursor);
        UiFrame.Hover(win, new Vector2(b.Origin.X + b.Size.X - 2, b.Y + 10), frame);
        Assert.Equal((true, true, false), b.LastCenter);
        Assert.Equal(ImGuiMouseCursor.ResizeEw, b.Cursor);
        UiFrame.Press(win, new Vector2(b.X, b.Y + 10), frame);
        Assert.Equal((true, false, true), b.LastCenter);
        UiFrame.Release(win, frame);
    }

    [Fact]
    public void ALockedCenteredPanelIgnoresDragEvenWithTheMoveKeyHeld()
    {
        var overlay = new Overlay { Locked = true, MoveHeld = true };
        var b = NewBanner();
        var grab = new Vector2(b.X, b.Y + 10);
        UiFrame.Drag(UiFrame.NextId("centerlocked"), grab, grab + new Vector2(40, 20), CenterFrame(overlay, b));
        Assert.Equal((500, 40), (b.X, b.Y));
        Assert.Equal((false, false, false), b.LastCenter);
    }

    [Fact]
    public void LockingACenteredPanelMidDragDropsTheDragForGood()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = UiFrame.NextId("centermidlock");
        var frame = CenterFrame(overlay, b);
        var grab = new Vector2(b.X, b.Y + 10);
        UiFrame.Press(win, grab, frame);
        UiFrame.MoveTo(win, grab + new Vector2(20, 0), frame);
        Assert.Equal(520, b.X);
        overlay.Locked = true;
        UiFrame.MoveTo(win, grab + new Vector2(50, 0), frame);
        overlay.Locked = false;
        UiFrame.MoveTo(win, grab + new Vector2(90, 0), frame);
        Assert.Equal(520, b.X);
        Assert.False(b.LastCenter.active);
        UiFrame.Release(win, frame);
    }


    static readonly Vector2 HintMin = new(100, 100);
    static readonly Vector2 HintMax = new(300, 220);
    static readonly uint DimHint = EColor.U32(Color.FromArgb(120, 120, 220, 255));
    static readonly uint BrightHint = EColor.U32(Color.FromArgb(220, 120, 220, 255));
    static readonly uint Grip = EColor.U32(Color.FromArgb(255, 120, 220, 255));

    static List<ImDrawVert> HintVerts(bool active, bool hovered, Overlay.ResizeEdge edge)
    {
        List<ImDrawVert> verts = [];
        UiFrame.Run(UiFrame.NextId("hint"), () =>
        {
            verts = UiFrame.Drawn(ImGui.GetForegroundDrawList(), () => Overlay.DragHint(HintMin, HintMax, active, hovered, edge));
        });
        return verts;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void DragHintOutlineBrightensWhenActiveOrHovered(bool active, bool hovered, bool bright)
    {
        var verts = HintVerts(active, hovered, Overlay.ResizeEdge.None);
        Assert.Equal(bright, verts.Any(v => v.Col == BrightHint));
        Assert.Equal(!bright, verts.Any(v => v.Col == DimHint));
        Assert.DoesNotContain(verts, v => v.Col == Grip);
        var outline = verts.Where(v => v.Col == BrightHint || v.Col == DimHint).ToList();
        Assert.InRange(outline.Min(v => v.Pos.X), HintMin.X - 2f, HintMin.X + 2f);
        Assert.InRange(outline.Max(v => v.Pos.X), HintMax.X - 2f, HintMax.X + 2f);
        Assert.InRange(outline.Min(v => v.Pos.Y), HintMin.Y - 2f, HintMin.Y + 2f);
        Assert.InRange(outline.Max(v => v.Pos.Y), HintMax.Y - 2f, HintMax.Y + 2f);
    }

    static (float width, float height) GripExtent(List<ImDrawVert> verts)
    {
        var grip = verts.Where(v => v.Col == Grip).ToList();
        return (grip.Max(v => v.Pos.X) - grip.Min(v => v.Pos.X), grip.Max(v => v.Pos.Y) - grip.Min(v => v.Pos.Y));
    }

    [Fact]
    public void DragHintRightEdgeDrawsAVerticalGripOnTheRightSide()
    {
        var verts = HintVerts(false, false, Overlay.ResizeEdge.Right);
        var (width, height) = GripExtent(verts);
        Assert.InRange(width, 0f, 8f);
        Assert.InRange(height, HintMax.Y - HintMin.Y, HintMax.Y - HintMin.Y + 8f);
        Assert.All(verts.Where(v => v.Col == Grip), v => Assert.InRange(v.Pos.X, HintMax.X - 4f, HintMax.X + 4f));
    }

    [Fact]
    public void DragHintBottomEdgeDrawsAHorizontalGripOnTheBottomSide()
    {
        var verts = HintVerts(false, false, Overlay.ResizeEdge.Bottom);
        var (width, height) = GripExtent(verts);
        Assert.InRange(height, 0f, 8f);
        Assert.InRange(width, HintMax.X - HintMin.X, HintMax.X - HintMin.X + 8f);
        Assert.All(verts.Where(v => v.Col == Grip), v => Assert.InRange(v.Pos.Y, HintMax.Y - 4f, HintMax.Y + 4f));
    }

    [Fact]
    public void DragHintCornerDrawsBothGrips()
    {
        var verts = HintVerts(false, false, Overlay.ResizeEdge.Corner);
        var (width, height) = GripExtent(verts);
        Assert.True(width > 100f);
        Assert.True(height > 100f);
    }

    static ImGuiWindowPtr BlockerWindow(string id) => ImGuiP.FindWindowByName($"##eidrag_{ImGui.GetID(id):X8}");

    [Fact]
    public void ClickBlockerIsAWindowAtTheGivenRect()
    {
        var pos = new Vector2(120, 80);
        var size = new Vector2(220, 90);
        var id = UiFrame.NextId("blocker");
        var found = false;
        Vector2 windowPos = default, windowSize = default;
        TestFrame.Run(() =>
        {
            Overlay.ClickBlocker(id, pos, size);
            var window = BlockerWindow(id);
            found = !window.IsNull;
            if (found)
            {
                windowPos = window.Pos;
                windowSize = window.Size;
            }
        });
        Assert.True(found);
        Assert.Equal(pos, windowPos);
        Assert.Equal(size, windowSize);
    }

    [Fact]
    public void ClickBlockerCapturesTheMouseOnlyOverItsRect()
    {
        var pos = new Vector2(120, 80);
        var size = new Vector2(220, 90);
        var id = UiFrame.NextId("capture");
        var io = UiFrame.Io();
        var capture = false;
        void Frame() => TestFrame.Run(() =>
        {
            Overlay.ClickBlocker(id, pos, size);
            capture = io.WantCaptureMouse;
        });
        io.AddMousePosEvent(pos.X + size.X / 2, pos.Y + size.Y / 2);
        Frame();
        Frame();
        Assert.True(capture);
        io.AddMousePosEvent(pos.X + size.X + 40, pos.Y + size.Y + 40);
        Frame();
        Frame();
        Assert.False(capture);
    }

    [Fact]
    public void ClickBlockerDrawsNothingEvenWithAWindowBorder()
    {
        UiFrame.Io();
        var id = UiFrame.NextId("invisible");
        var style = ImGui.GetStyle();
        var border = style.WindowBorderSize;
        List<ImDrawVert> verts = [];
        try
        {
            style.WindowBorderSize = 1f;
            TestFrame.Run(() =>
            {
                Overlay.ClickBlocker(id, new Vector2(120, 80), new Vector2(220, 90));
                verts = UiFrame.Verts(BlockerWindow(id).DrawList);
            });
        }
        finally
        {
            style.WindowBorderSize = border;
        }
        Assert.Empty(verts);
    }

    [Fact]
    public void ClickBlockerNamesAreScopedByTheIdStackNotJustTheString()
    {
        var id = UiFrame.NextId("scoped");
        Vector2 a = default, b = default, other = default;
        uint idA = 0, idB = 0;
        TestFrame.Run(() =>
        {
            ImGui.PushID("scopeA");
            Overlay.ClickBlocker(id, new Vector2(10, 10), new Vector2(50, 50));
            ImGui.PopID();
            ImGui.PushID("scopeB");
            Overlay.ClickBlocker(id, new Vector2(300, 300), new Vector2(60, 60));
            ImGui.PopID();
            Overlay.ClickBlocker(id + "x", new Vector2(500, 100), new Vector2(60, 60));
            other = BlockerWindow(id + "x").Pos;
            ImGui.PushID("scopeA");
            var windowA = BlockerWindow(id);
            (a, idA) = (windowA.Pos, windowA.ID);
            ImGui.PopID();
            ImGui.PushID("scopeB");
            var windowB = BlockerWindow(id);
            (b, idB) = (windowB.Pos, windowB.ID);
            ImGui.PopID();
        });
        Assert.NotEqual(idA, idB);
        Assert.Equal(new Vector2(10, 10), a);
        Assert.Equal(new Vector2(300, 300), b);
        Assert.Equal(new Vector2(500, 100), other);
    }

    [Fact]
    public void ClickBlockerWindowIsUndecoratedUnmovableAndNeverTakesFocus()
    {
        var id = UiFrame.NextId("blockerflags");
        ImGuiWindowFlags flags = default;
        TestFrame.Run(() =>
        {
            Overlay.ClickBlocker(id, new Vector2(120, 80), new Vector2(220, 90));
            flags = BlockerWindow(id).Flags;
        });
        const ImGuiWindowFlags expected = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse;
        Assert.Equal(expected, flags & expected);
    }

    [Fact]
    public void ClickBlockerDrawsInsideAWindowWithoutLeakingAnId() =>
        UiFrame.Run(UiFrame.NextId("balancehost"), () => Overlay.ClickBlocker(UiFrame.NextId("balance"), new Vector2(10, 10), new Vector2(50, 50)));

    [Fact]
    public void ClickBlockerReturnsTrueOnAClickInsideItsRectOnly()
    {
        var pos = new Vector2(120, 80);
        var size = new Vector2(220, 90);
        var id = UiFrame.NextId("click");
        var io = UiFrame.Io();
        var clicks = 0;
        void Frame() => TestFrame.Run(() => { if (Overlay.ClickBlocker(id, pos, size)) clicks++; });
        io.AddMousePosEvent(pos.X + 10, pos.Y + 10);
        Frame();
        Frame();
        Assert.Equal(0, clicks);
        io.AddMouseButtonEvent(0, true);
        Frame();
        Assert.Equal(1, clicks);
        io.AddMouseButtonEvent(0, false);
        Frame();
        Frame();
        Assert.Equal(1, clicks);
        io.AddMousePosEvent(pos.X + size.X + 40, pos.Y + 10);
        Frame();
        Frame();
        io.AddMouseButtonEvent(0, true);
        Frame();
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ClickBlockerSitsBehindAWindowAtTheSameSpot()
    {
        var pos = new Vector2(120, 80);
        var size = new Vector2(220, 90);
        var id = UiFrame.NextId("behind");
        var io = UiFrame.Io();
        var clicks = 0;
        void Frame() => TestFrame.Run(() =>
        {
            ImGui.SetNextWindowPos(pos);
            ImGui.SetNextWindowSize(size);
            ImGui.Begin("##front-" + id, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);
            ImGui.End();
            if (Overlay.ClickBlocker(id, pos, size)) clicks++;
        });
        io.AddMousePosEvent(pos.X + 10, pos.Y + 10);
        Frame();
        Frame();
        io.AddMouseButtonEvent(0, true);
        Frame();
        Assert.Equal(0, clicks);
    }
}
