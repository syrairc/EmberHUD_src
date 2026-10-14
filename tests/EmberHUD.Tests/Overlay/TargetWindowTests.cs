using System.Drawing;

namespace EmberHUD.Tests;

public class TargetWindowTests
{
    sealed class FakeTarget : ITargetSource
    {
        public nint Handle;
        public HashSet<nint> StillAlive = [];
        public Rectangle Bounds = new(0, 0, 100, 100);
        public bool Foreground = true;
        public bool Minimized;
        public bool BoundsReadable = true;
        public int Finds;

        public nint Find()
        {
            Finds++;
            return Handle;
        }

        public bool Exists(nint hwnd) => hwnd != 0 && (hwnd == Handle || StillAlive.Contains(hwnd));

        public bool TryGetBounds(nint hwnd, out Rectangle bounds)
        {
            bounds = Bounds;
            return hwnd != 0 && BoundsReadable;
        }

        public bool IsForeground(nint hwnd) => Foreground;

        public bool IsMinimized(nint hwnd) => Minimized;
    }

    long _now;

    TargetWindow Target(FakeTarget source) => new(source, () => _now);

    [Fact]
    public void ReportsNothingWhileTheClientIsAbsent()
    {
        var t = Target(new FakeTarget());

        Assert.False(t.Poll());
        Assert.False(t.IsVisibleTarget);
    }

    [Fact]
    public void ReportsFoundOnceAndThenNothing()
    {
        var src = new FakeTarget();
        var t = Target(src);
        t.Poll();

        src.Handle = 42;

        Assert.True(t.Poll());
        Assert.Equal(42, t.Handle);
        Assert.False(t.Poll());
        Assert.True(t.IsVisibleTarget);
    }

    [Fact]
    public void ReportsResizeWhenTheRectMoves()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();

        src.Bounds = new Rectangle(0, 0, 3440, 1440);

        Assert.True(t.Poll());
        Assert.Equal(new Rectangle(0, 0, 3440, 1440), t.Bounds);
    }

    [Fact]
    public void ReportsLostWhenTheClientGoesAway()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();

        src.Handle = 0;

        Assert.True(t.Poll());
        Assert.Equal(0, t.Handle);
        Assert.False(t.IsVisibleTarget);
        Assert.False(t.Poll());
    }

    [Fact]
    public void FindsTheClientAgainAfterALoss()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();
        src.Handle = 0;
        t.Poll();

        src.Handle = 43;

        Assert.True(t.Poll());
        Assert.Equal(43, t.Handle);
    }

    [Fact]
    public void IsNotAVisibleTargetWhileBackgroundedOrMinimized()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();

        src.Foreground = false;
        t.Poll();
        Assert.False(t.IsVisibleTarget);

        src.Foreground = true;
        src.Minimized = true;
        t.Poll();
        Assert.False(t.IsVisibleTarget);
    }

    [Fact]
    public void BoundsFailureDoesNotReportFound()
    {
        var src = new FakeTarget { Handle = 42, BoundsReadable = false };
        var t = Target(src);

        Assert.False(t.Poll());
        Assert.Equal(0, t.Handle);
        Assert.Equal(Rectangle.Empty, t.Bounds);
    }

    [Fact]
    public void ReportsFoundWhenBoundsBecomesReadable()
    {
        var src = new FakeTarget { Handle = 42, BoundsReadable = false };
        var t = Target(src);
        t.Poll();

        src.BoundsReadable = true;

        Assert.True(t.Poll());
        Assert.Equal(42, t.Handle);
        Assert.Equal(new Rectangle(0, 0, 100, 100), t.Bounds);
    }

    [Fact]
    public void BoundsFailureDoesNotLeaveStaleRect()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();

        src.Handle = 43;
        src.Bounds = new Rectangle(200, 200, 300, 300);
        src.BoundsReadable = false;

        Assert.False(t.Poll());
        Assert.Equal(42, t.Handle);
        Assert.Equal(new Rectangle(0, 0, 100, 100), t.Bounds);
    }

    [Fact]
    public void TheHandleIsKeptWithoutSearchingWhileItStaysValid()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();
        Assert.Equal(1, src.Finds);

        _now += 500;
        t.Poll();
        t.Poll();

        Assert.Equal(1, src.Finds);
    }

    [Fact]
    public void ADestroyedWindowIsReplacedOnTheNextPoll()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();

        src.Handle = 43;

        Assert.True(t.Poll());
        Assert.Equal(43, t.Handle);
        Assert.Equal(2, src.Finds);
    }

    [Fact]
    public void TheWindowIsSearchedForAgainOnceTheIntervalHasPassed()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = Target(src);
        t.Poll();
        src.Handle = 43;
        src.StillAlive.Add(42);

        _now += 999;
        Assert.False(t.Poll());
        Assert.Equal(42, t.Handle);

        _now += 1;
        Assert.True(t.Poll());
        Assert.Equal(43, t.Handle);
    }
}
