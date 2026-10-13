using System.Drawing;
using System.Numerics;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class OverlayTests
{
    static readonly Vector2 Origin = Vector2.Zero;
    static readonly Vector2 Size = new(100, 100);

    static int _next;

    static string NextId(string prefix) => prefix + ++_next;

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
            Overlay.Chrome(ImGui.GetForegroundDrawList(), origin, origin + new Vector2(width, height), Color.FromArgb(200, 10, 10, 10), Color.White, 1f, 4f);
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
        try
        {
            UiFrame.Drag(NextId("drag"), grab, grab + delta, Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        var win = NextId("active");
        var otherFrame = Frame(overlay, other, "other");
        var frame = Frame(overlay, p);
        void Both()
        {
            frame();
            otherFrame();
        }
        try
        {
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
        finally
        {
            UiFrame.Unhover();
        }
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
        try
        {
            UiFrame.Drag(NextId("overlap"), grab, grab + new Vector2(40, 25), () =>
            {
                frameA();
                frameB();
            });
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((140, 125), (a.X, a.Y));
        Assert.Equal((100, 100), (b.X, b.Y));
        Assert.Equal(new Vector2(100, 100), b.Origin);
    }

    [Fact]
    public void APressOutsideThePanelStartsNothing()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("outside");
        var frame = Frame(overlay, p);
        var outside = p.Origin - new Vector2(30, 30);
        try
        {
            UiFrame.Press(win, outside, frame);
            UiFrame.MoveTo(win, outside + new Vector2(40, 40), frame);
            Assert.False(p.Last.active);
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((100, 100), (p.X, p.Y));
        Assert.Equal((200, 80), (p.W, p.H));
    }

    [Fact]
    public void AfterAResizeEndsTheNextGrabStartsFresh()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("fresh");
        var frame = Frame(overlay, p);
        try
        {
            var grip = CornerGrip(p);
            UiFrame.Drag(win, grip, grip + new Vector2(20, 10), frame);
            Assert.Equal((218, 88), (p.W, p.H));
            Assert.False(p.Last.active);
            var grab = Inside(p);
            UiFrame.Drag(win, grab, grab + new Vector2(30, 15), frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((130, 115), (p.X, p.Y));
        Assert.Equal((218, 88), (p.W, p.H));
    }

    [Fact]
    public void DraggingTheCornerResizesBothAndMovesNothing()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = CornerGrip(p);
        try
        {
            UiFrame.Drag(NextId("corner"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        try
        {
            UiFrame.Drag(NextId("right"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((248, 80), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void DraggingTheBottomEdgeResizesOnlyTheHeight()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = BottomGrip(p);
        try
        {
            UiFrame.Drag(NextId("bottom"), grip, grip + new Vector2(50, 30), Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((200, 108), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void AnEdgeMaskedOffMovesThePanelInstead()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = BottomGrip(p);
        try
        {
            UiFrame.Drag(NextId("masked"), grip, grip + new Vector2(0, 30), Frame(overlay, p, edges: Overlay.ResizeEdge.Right));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((200, 80), (p.W, p.H));
        Assert.Equal((100, 130), (p.X, p.Y));
    }

    [Fact]
    public void TheShortOverloadResizesOnlyTheWidthEvenFromTheCorner()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var grip = CornerGrip(p);
        var win = NextId("short");
        var frame = ShortFrame(overlay, p);
        try
        {
            UiFrame.Hover(win, grip, frame);
            Assert.Equal(Overlay.ResizeEdge.Right, p.Last.edge);
            UiFrame.Drag(win, grip, grip + new Vector2(50, 30), frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((248, 80), (p.W, p.H));
        Assert.Equal((100, 100), (p.X, p.Y));
    }

    [Fact]
    public void ResizingIsClampedToTheCallersMinAndMax()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("clamp");
        var frame = Frame(overlay, p, minWidth: 150, maxWidth: 250, minHeight: 50, maxHeight: 90);
        try
        {
            var grip = CornerGrip(p);
            UiFrame.Drag(win, grip, grip + new Vector2(500, 500), frame);
            Assert.Equal((250, 90), (p.W, p.H));
            p.Size = new Vector2(p.W, p.H);
            grip = CornerGrip(p);
            UiFrame.Drag(win, grip, grip - new Vector2(400, 400), frame);
            Assert.Equal((150, 50), (p.W, p.H));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        try
        {
            UiFrame.Hover(NextId("cursor"), at, Frame(overlay, p));
            Assert.Equal(expected, p.Cursor);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ALiveResizeKeepsItsEdgeAndCursorEvenWhenTheMouseLeavesTheBand()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("sticky");
        var frame = Frame(overlay, p);
        try
        {
            UiFrame.Press(win, RightGrip(p), frame);
            UiFrame.MoveTo(win, p.Origin + new Vector2(60, 40), frame);
            Assert.Equal(Overlay.ResizeEdge.Right, p.Last.edge);
            Assert.Equal(ImGuiMouseCursor.ResizeEw, p.Cursor);
            Assert.True(p.Last.active);
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ALockedPanelIgnoresADragAndReportsNothing()
    {
        var overlay = new Overlay { Locked = true };
        var p = NewPanel();
        var grab = Inside(p);
        try
        {
            UiFrame.Drag(NextId("locked"), grab, grab + new Vector2(40, 40), Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        try
        {
            UiFrame.Drag(NextId("moveheld"), grab, grab + new Vector2(40, 25), Frame(overlay, p));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((140, 125), (p.X, p.Y));
    }

    [Fact]
    public void LockingMidDragLetsTheLiveDragFinish()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("midlock");
        var frame = Frame(overlay, p);
        try
        {
            UiFrame.Press(win, Inside(p), frame);
            overlay.Locked = true;
            UiFrame.MoveTo(win, Inside(p) + new Vector2(30, 10), frame);
            Assert.Equal((130, 110), (p.X, p.Y));
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ReleaseDropsTheNamedPanelsDragButNotAnotherPanelsId()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("release");
        var frame = Frame(overlay, p);
        try
        {
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
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ReleaseDropsALiveResizeToo()
    {
        var overlay = new Overlay();
        var p = NewPanel();
        var win = NextId("releaseresize");
        var frame = Frame(overlay, p);
        try
        {
            var grip = RightGrip(p);
            UiFrame.Press(win, grip, frame);
            UiFrame.MoveTo(win, grip + new Vector2(20, 0), frame);
            Assert.Equal(218, p.W);
            overlay.Release("panel");
            UiFrame.MoveTo(win, grip + new Vector2(90, 0), frame);
            Assert.Equal(218, p.W);
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ACenteredPanelMovesAroundItsCentreAndKeepsTheGrabOffset()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var grab = new Vector2(b.X, b.Y + 10);
        try
        {
            UiFrame.Drag(NextId("center"), grab, grab + new Vector2(40, 20), CenterFrame(overlay, b));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        try
        {
            UiFrame.Drag(NextId("centeroverlap"), grab, grab + new Vector2(40, 20), () =>
            {
                frameA();
                frameB();
            });
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((540, 60), (a.X, a.Y));
        Assert.Equal((500, 40), (b.X, b.Y));
        Assert.Equal(new Vector2(350, 40), b.Origin);
    }

    [Fact]
    public void AfterACenteredResizeAndDragEndTheNextGrabStartsFresh()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = NextId("centerfresh");
        var frame = CenterFrame(overlay, b);
        try
        {
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
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal(480, b.X);
    }

    [Fact]
    public void ACenteredPanelResizesSymmetricallyAndClamps()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = NextId("centerresize");
        var frame = CenterFrame(overlay, b, minWidth: 100, maxWidth: 500);
        try
        {
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
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ACenteredPanelReportsHoverEdgeAndActive()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = NextId("centerreport");
        var frame = CenterFrame(overlay, b);
        try
        {
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
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ALockedCenteredPanelIgnoresDragEvenWithTheMoveKeyHeld()
    {
        var overlay = new Overlay { Locked = true, MoveHeld = true };
        var b = NewBanner();
        var grab = new Vector2(b.X, b.Y + 10);
        try
        {
            UiFrame.Drag(NextId("centerlocked"), grab, grab + new Vector2(40, 20), CenterFrame(overlay, b));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal((500, 40), (b.X, b.Y));
        Assert.Equal((false, false, false), b.LastCenter);
    }

    [Fact]
    public void LockingACenteredPanelMidDragDropsTheDragForGood()
    {
        var overlay = new Overlay();
        var b = NewBanner();
        var win = NextId("centermidlock");
        var frame = CenterFrame(overlay, b);
        var grab = new Vector2(b.X, b.Y + 10);
        try
        {
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
        finally
        {
            UiFrame.Unhover();
        }
    }

    static List<ImDrawVert> NewVerts(ImDrawListPtr list, int from) => UiFrame.Verts(list).Skip(from).ToList();

    static uint Rgb(uint col) => col & 0x00FFFFFFu;

    static List<ImDrawVert> ChromeVerts(Color background, Color border, float thickness, float radius)
    {
        List<ImDrawVert> verts = [];
        UiFrame.Run(NextId("chrome"), () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            Overlay.Chrome(list, new Vector2(100, 100), new Vector2(300, 200), background, border, thickness, radius);
            verts = NewVerts(list, from);
        });
        return verts;
    }

    static readonly Color Fill = Color.FromArgb(200, 10, 220, 30);
    static readonly Color Line = Color.FromArgb(255, 240, 20, 60);

    [Fact]
    public void ChromeFillsWithTheBackgroundColourOverTheRect()
    {
        var verts = ChromeVerts(Fill, Line, 0f, 0f);
        Assert.NotEmpty(verts);
        Assert.All(verts, v => Assert.Equal(Rgb(EColor.U32(Fill)), Rgb(v.Col)));
        Assert.Contains(verts, v => v.Col == EColor.U32(Fill));
        Assert.InRange(verts.Min(v => v.Pos.X), 99f, 100f);
        Assert.InRange(verts.Max(v => v.Pos.X), 300f, 301f);
        Assert.InRange(verts.Min(v => v.Pos.Y), 99f, 100f);
        Assert.InRange(verts.Max(v => v.Pos.Y), 200f, 201f);
    }

    [Fact]
    public void ChromeOutlinesInTheBorderColourWhenThereIsNoFill()
    {
        var verts = ChromeVerts(Color.FromArgb(0, 10, 220, 30), Line, 1f, 0f);
        Assert.NotEmpty(verts);
        Assert.All(verts, v => Assert.Equal(Rgb(EColor.U32(Line)), Rgb(v.Col)));
    }

    [Fact]
    public void ChromeDrawsBothWhenBothAreSet()
    {
        var verts = ChromeVerts(Fill, Line, 1f, 0f);
        Assert.Contains(verts, v => v.Col == EColor.U32(Fill));
        Assert.Contains(verts, v => v.Col == EColor.U32(Line));
    }

    [Fact]
    public void ChromeWithZeroThicknessDrawsNoOutline() => Assert.DoesNotContain(ChromeVerts(Fill, Line, 0f, 0f), v => Rgb(v.Col) == Rgb(EColor.U32(Line)));

    [Fact]
    public void ChromeRadiusRoundsTheFillAndTheOutline()
    {
        var clear = Color.FromArgb(0, 0, 0, 0);
        Assert.True(ChromeVerts(Fill, Line, 0f, 12f).Count > ChromeVerts(Fill, Line, 0f, 0f).Count);
        Assert.True(ChromeVerts(clear, Line, 1f, 12f).Count > ChromeVerts(clear, Line, 1f, 0f).Count);
    }

    [Fact]
    public void ChromeThicknessWidensTheOutline()
    {
        var thin = ChromeVerts(Color.FromArgb(0, 0, 0, 0), Line, 1f, 0f);
        var thick = ChromeVerts(Color.FromArgb(0, 0, 0, 0), Line, 6f, 0f);
        Assert.True(thick.Max(v => v.Pos.X) - thick.Min(v => v.Pos.X) > thin.Max(v => v.Pos.X) - thin.Min(v => v.Pos.X));
    }

    static readonly Vector2 HintMin = new(100, 100);
    static readonly Vector2 HintMax = new(300, 220);
    static readonly uint DimHint = EColor.U32(Color.FromArgb(120, 120, 220, 255));
    static readonly uint BrightHint = EColor.U32(Color.FromArgb(220, 120, 220, 255));
    static readonly uint Grip = EColor.U32(Color.FromArgb(255, 120, 220, 255));

    static List<ImDrawVert> HintVerts(bool active, bool hovered, Overlay.ResizeEdge edge)
    {
        List<ImDrawVert> verts = [];
        UiFrame.Run(NextId("hint"), () =>
        {
            var list = ImGui.GetForegroundDrawList();
            var from = list.VtxBuffer.Size;
            Overlay.DragHint(HintMin, HintMax, active, hovered, edge);
            verts = NewVerts(list, from);
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
        var id = NextId("blocker");
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
        var id = NextId("capture");
        var io = UiFrame.Io();
        var capture = false;
        void Frame() => TestFrame.Run(() =>
        {
            Overlay.ClickBlocker(id, pos, size);
            capture = io.WantCaptureMouse;
        });
        try
        {
            io.AddMousePosEvent(pos.X + size.X / 2, pos.Y + size.Y / 2);
            Frame();
            Frame();
            Assert.True(capture);
            io.AddMousePosEvent(pos.X + size.X + 40, pos.Y + size.Y + 40);
            Frame();
            Frame();
            Assert.False(capture);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void ClickBlockerDrawsNothingEvenWithAWindowBorder()
    {
        UiFrame.Io();
        var id = NextId("invisible");
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
        var id = NextId("scoped");
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
        var id = NextId("blockerflags");
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
    public void ClickBlockerKeepsTheIdStackBalanced()
    {
        var id = NextId("balance");
        uint before = 0, after = 0;
        UiFrame.Run(NextId("balancehost"), () =>
        {
            before = ImGui.GetID("probe");
            Overlay.ClickBlocker(id, new Vector2(10, 10), new Vector2(50, 50));
            after = ImGui.GetID("probe");
        });
        Assert.Equal(before, after);
    }

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
        UiFrame.Run(NextId("minsize"), () => min = ImGui.GetStyle().WindowMinSize);
        return min;
    }

    static Vector2 Measure(string label)
    {
        Vector2 size = default;
        UiFrame.Run(NextId("measure"), () => size = ImGui.CalcTextSize(label) + ImGui.GetStyle().FramePadding * 2f);
        return size;
    }

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
            clicked = Floating.Button(NextId("offscreen"), "Toast me", ref pos);
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
            Floating.Button(NextId("topleft"), "Toast me", ref pos);
            viewport = ImGui.GetMainViewport().Pos;
        });
        Assert.Equal(viewport, pos);
    }

    [Fact]
    public void AFloatingButtonWiderThanTheViewportPinsToTheLeftInsteadOfThrowing()
    {
        var display = UiFrame.Io().DisplaySize;
        var pos = new Vector2(400, 400);
        TestFrame.Run(() => Floating.Button(NextId("wide"), "Toast me", ref pos, new Vector2(display.X + 100, 30)));
        Assert.Equal(0f, pos.X);
        Assert.Equal(400f, pos.Y);
    }

    [Fact]
    public void AFloatingButtonTallerThanTheViewportPinsToTheTop()
    {
        var display = UiFrame.Io().DisplaySize;
        var pos = new Vector2(400, 400);
        TestFrame.Run(() => Floating.Button(NextId("tall"), "Toast me", ref pos, new Vector2(30, display.Y + 100)));
        Assert.Equal(400f, pos.X);
        Assert.Equal(0f, pos.Y);
    }

    [Fact]
    public void AFloatingButtonKeepsAnInsideSpotUntouched()
    {
        var pos = new Vector2(200, 150);
        TestFrame.Run(() => Floating.Button(NextId("inside"), "Toast me", ref pos));
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
            var id = NextId("sized");
            Floating.Button(id, "Toast me", ref pos);
            measuredWindow = ImGuiP.FindWindowByName(FloatName(id)).Size;
            id = NextId("given");
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
    public void ButtonSizeOfDefaultIsTheLabelPlusPadding() => Assert.Equal(new Vector2(58, 19), Floating.ButtonSize(default, new Vector2(50, 13), new Vector2(4, 3)));

    [Fact]
    public void AFloatingButtonWindowFollowsASizeChange()
    {
        var id = NextId("resize");
        var host = NextId("resizehost");
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
        var id = NextId("atpos");
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
        var win = NextId("clickwin");
        var id = NextId("click");
        var b = new Button { Pos = new Vector2(200, 150) };
        var center = b.Pos + Measure("Toast me") / 2f;
        try
        {
            UiFrame.Click(win, center, ButtonFrame(id, b));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal(1, b.Clicks);
        Assert.Equal(new Vector2(200, 150), b.Pos);
    }

    [Fact]
    public void AFloatingButtonDragMovesItByTheMouseDeltaAndIsNotAClick()
    {
        var win = NextId("dragwin");
        var id = NextId("drag");
        var b = new Button { Pos = new Vector2(200, 150) };
        var frame = ButtonFrame(id, b);
        var size = Measure("Toast me");
        var center = b.Pos + size / 2f;
        var delta = new Vector2(60, 40);
        try
        {
            UiFrame.Drag(win, center, center + delta, frame);
            Assert.Equal(0, b.Clicks);
            Assert.Equal(new Vector2(200, 150) + delta, b.Pos);
            UiFrame.Click(win, b.Pos + size / 2f, frame);
            Assert.Equal(1, b.Clicks);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void AFloatingButtonPressThatNeverPassesTheDragThresholdStaysPutAndClicks()
    {
        var win = NextId("tinywin");
        var id = NextId("tiny");
        var b = new Button { Pos = new Vector2(200, 150) };
        var center = b.Pos + Measure("Toast me") / 2f;
        var threshold = UiFrame.Io().MouseDragThreshold;
        try
        {
            UiFrame.Drag(win, center, center + new Vector2(threshold / 3f, 0), ButtonFrame(id, b));
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal(1, b.Clicks);
        Assert.Equal(new Vector2(200, 150), b.Pos);
    }

    [Fact]
    public void AFloatingButtonCursorIsAHandWhenHoveredAndMoveWhenHeld()
    {
        var win = NextId("cursorwin");
        var id = NextId("cursor");
        var b = new Button { Pos = new Vector2(200, 150) };
        var frame = ButtonFrame(id, b);
        var center = b.Pos + Measure("Toast me") / 2f;
        try
        {
            UiFrame.Unhover();
            UiFrame.Run(win, frame);
            Assert.Equal(ImGuiMouseCursor.Arrow, b.Cursor);
            UiFrame.Hover(win, center, frame);
            Assert.Equal(ImGuiMouseCursor.Hand, b.Cursor);
            UiFrame.Press(win, center, frame);
            Assert.Equal(ImGuiMouseCursor.ResizeAll, b.Cursor);
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void AFloatingButtonFillsWithTheButtonColourForIdleHoveredAndHeld()
    {
        var win = NextId("colourwin");
        var id = NextId("colour");
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
        try
        {
            UiFrame.Unhover();
            UiFrame.Run(win, frame);
            Assert.Equal((true, false, false), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
            UiFrame.Hover(win, center, frame);
            Assert.Equal((false, true, false), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
            UiFrame.Press(win, center, frame);
            Assert.Equal((false, false, true), (b.Verts.Any(v => v.Col == idle), b.Verts.Any(v => v.Col == hovered), b.Verts.Any(v => v.Col == held)));
            UiFrame.Release(win, frame);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void AFloatingButtonDrawsItsLabelAndABorder()
    {
        var win = NextId("labelwin");
        var size = new Vector2(150, 40);
        var plain = new Button { Pos = new Vector2(200, 150) };
        var labelled = new Button { Pos = new Vector2(200, 250) };
        uint border = 0;
        try
        {
            UiFrame.Unhover();
            UiFrame.Run(win, () => border = ImGui.GetColorU32(ImGuiCol.Border));
            UiFrame.Run(win, ButtonFrame(NextId("empty"), plain, "", size));
            UiFrame.Run(win, ButtonFrame(NextId("text"), labelled, "Toast me", size));
        }
        finally
        {
            UiFrame.Unhover();
        }
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
        var id = NextId("flags");
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
                Floating.Button(NextId("moved"), "Toast me", ref far);
                Floating.Button(NextId("movednear"), "Toast me", ref near);
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
        var win = NextId("scopewin");
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
        var win = NextId("leakwin");
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
        try
        {
            UiFrame.Press(win, from, frameA);
            UiFrame.MoveTo(win, from + new Vector2(20, 0), frameA);
            UiFrame.MoveTo(win, from + new Vector2(40, 0), frameA);
            UiFrame.Release(win, () => { });
            UiFrame.Click(win, b.Pos + size / 2f, frameB);
            Assert.Equal(1, b.Clicks);
        }
        finally
        {
            UiFrame.Click(win, a.Pos + size / 2f, frameA);
            UiFrame.Unhover();
        }
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 2)]
    [InlineData(true, true, 2)]
    public void FloatingButtonKeepsTheIdStackBalancedInEveryVariant(bool explicitSize, bool offscreen, int mouse)
    {
        var win = NextId("balancewin");
        var id = NextId("balancebtn");
        var b = new Button { Pos = offscreen ? new Vector2(5000, 5000) : new Vector2(200, 150) };
        var size = explicitSize ? new Vector2(150, 40) : default;
        var mismatches = 0;
        var inner = ButtonFrame(id, b, size: size);
        void Probe()
        {
            var before = ImGui.GetID("probe");
            inner();
            if (before != ImGui.GetID("probe")) mismatches++;
        }
        try
        {
            var at = (explicitSize ? size : Measure("Toast me")) / 2f;
            if (mouse == 0) UiFrame.Run(win, Probe);
            else if (mouse == 1) UiFrame.Hover(win, b.Pos + at, Probe);
            else
            {
                UiFrame.Press(win, b.Pos + at, Probe);
                UiFrame.Release(win, Probe);
            }
        }
        finally
        {
            UiFrame.Unhover();
        }
        Assert.Equal(0, mismatches);
    }
}
