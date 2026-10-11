using System.Drawing;

namespace Aember.Tests;

public class TargetWindowTests
{
    sealed class FakeTarget : ITargetSource
    {
        public nint Handle;
        public Rectangle Bounds = new(0, 0, 100, 100);
        public bool Foreground = true;
        public bool Minimized;
        public bool BoundsReadable = true;

        public nint Find() => Handle;
        public bool TryGetBounds(nint hwnd, out Rectangle bounds)
        {
            bounds = Bounds;
            return hwnd != 0 && BoundsReadable;
        }
        public bool IsForeground(nint hwnd) => Foreground;
        public bool IsMinimized(nint hwnd) => Minimized;
    }

    [Fact]
    public void ReportsNothingWhileTheClientIsAbsent()
    {
        var src = new FakeTarget();
        var t = new TargetWindow(src);

        Assert.Equal(TargetChange.None, t.Poll());
        Assert.False(t.IsVisibleTarget);
    }

    [Fact]
    public void ReportsFoundOnceAndThenNothing()
    {
        var src = new FakeTarget();
        var t = new TargetWindow(src);
        t.Poll();

        src.Handle = 42;

        Assert.Equal(TargetChange.Found, t.Poll());
        Assert.Equal(TargetChange.None, t.Poll());
        Assert.True(t.IsVisibleTarget);
    }

    [Fact]
    public void ReportsResizeWhenTheRectMoves()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = new TargetWindow(src);
        t.Poll();

        src.Bounds = new Rectangle(0, 0, 3440, 1440);

        Assert.Equal(TargetChange.Resized, t.Poll());
        Assert.Equal(new Rectangle(0, 0, 3440, 1440), t.Bounds);
    }

    [Fact]
    public void ReportsLostWhenTheClientGoesAway()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = new TargetWindow(src);
        t.Poll();

        src.Handle = 0;

        Assert.Equal(TargetChange.Lost, t.Poll());
        Assert.False(t.IsVisibleTarget);
    }

    [Fact]
    public void FindsTheClientAgainAfterALoss()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = new TargetWindow(src);
        t.Poll();
        src.Handle = 0;
        t.Poll();

        src.Handle = 43;

        Assert.Equal(TargetChange.Found, t.Poll());
    }

    [Fact]
    public void IsNotAVisibleTargetWhileBackgroundedOrMinimized()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = new TargetWindow(src);
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
        var t = new TargetWindow(src);

        Assert.Equal(TargetChange.None, t.Poll());
        Assert.Equal(0, t.Handle);
        Assert.Equal(Rectangle.Empty, t.Bounds);
    }

    [Fact]
    public void ReportsFoundWhenBoundsBecomesReadable()
    {
        var src = new FakeTarget { Handle = 42, BoundsReadable = false };
        var t = new TargetWindow(src);
        t.Poll();

        src.BoundsReadable = true;

        Assert.Equal(TargetChange.Found, t.Poll());
        Assert.Equal(42, t.Handle);
        Assert.Equal(new Rectangle(0, 0, 100, 100), t.Bounds);
    }

    [Fact]
    public void BoundsFailureDoesNotLeaveStaleRect()
    {
        var src = new FakeTarget { Handle = 42 };
        var t = new TargetWindow(src);
        t.Poll();

        src.Handle = 43;
        src.Bounds = new Rectangle(200, 200, 300, 300);
        src.BoundsReadable = false;

        Assert.Equal(TargetChange.None, t.Poll());
        Assert.Equal(new Rectangle(0, 0, 100, 100), t.Bounds);
    }
}

public class TitleMatchTests
{
    [Theory]
    [InlineData("Torchlight: Infinite", "Torchlight: Infinite", true)]
    [InlineData("Torchlight: Infinite", "Torchlight: Infinite  ", true)]
    [InlineData("Torchlight", "Torchlight: Infinite  ", true)]
    [InlineData("Torchlight: Infinite", "Torchlight", false)]
    [InlineData("Torchlight: Infinite", "Notepad", false)]
    [InlineData("Torchlight: Infinite", " Torchlight: Infinite", false)]
    [InlineData("Torchlight: Infinite", "torchlight: infinite", false)]
    [InlineData(null, "anything", true)]
    [InlineData("", "anything", true)]
    [InlineData(null, "", true)]
    [InlineData("x", "", false)]
    public void MatchesByOrdinalPrefix(string? wanted, string actual, bool expected)
    {
        Assert.Equal(expected, Win32TargetSource.TitleMatches(wanted, actual));
    }
}
