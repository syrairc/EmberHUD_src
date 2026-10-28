using System.Numerics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class SendElementTests : IDisposable
{
    static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    static readonly HumanizerConfig Distinct = new()
    {
        StepDelayMin = 1, StepDelayMax = 2,
        PreClickSettleMin = 100, PreClickSettleMax = 100,
        ClickDwellMin = 200, ClickDwellMax = 200,
        PostClickSettleMin = 300, PostClickSettleMax = 300,
    };

    public SendElementTests()
    {
        TestSend.Reset();
        Send.ToScreen = (x, y) => (x, y);
        ChatIs(false);
    }

    public void Dispose()
    {
        TestSend.Reset();
        TestGame.Set(GameState.Empty);
    }

    static void ChatIs(bool? open) => TestGame.Set(GameState.Empty with { ChatOpen = open });

    static Task<bool> Done(Task<bool> call) => call.WaitAsync(Limit, TestContext.Current.CancellationToken);

    static readonly ScreenRect Box = Rect(100, 200, 300, 260);

    static ElementOptions Direct => new() { Mode = MouseMode.Direct, Cancel = TestContext.Current.CancellationToken };

    [Fact]
    public async Task MoveToElementSendsOneMoveInsideTheRect()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Assert.True(await Done(Send.MoveToElement(Box, Direct)));
        var move = Assert.Single(sent);
        Assert.Null(move.Button);
        Assert.InRange(move.X, 100, 300);
        Assert.InRange(move.Y, 200, 260);
    }

    [Fact]
    public async Task ClickElementMovesThenPressesAndReleasesLeft()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Assert.True(await Done(Send.ClickElement(Box, Direct)));
        Assert.Equal(3, sent.Count);
        Assert.Null(sent[0].Button);
        Assert.Equal(new SentInput(Key.None, MouseButton.Left, true, 0, 0), sent[1]);
        Assert.Equal(new SentInput(Key.None, MouseButton.Left, false, 0, 0), sent[2]);
    }

    [Fact]
    public async Task ClickElementUsesTheGivenButtonAndMode()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Mode = MouseMode.Humanized;
        Assert.True(await Done(Send.ClickElement(Box, Direct with { Button = MouseButton.Right })));
        Assert.Equal(3, sent.Count);
        Assert.Equal(new SentInput(Key.None, MouseButton.Right, true, 0, 0), sent[1]);
        Assert.Equal(new SentInput(Key.None, MouseButton.Right, false, 0, 0), sent[2]);
    }

    [Fact]
    public async Task NullOrBadRectSendsNothing()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        foreach (var rect in new ScreenRect?[] { null, Rect(5, 5, 5, 20), Rect(float.NaN, 0, 10, 10) })
        {
            Assert.False(await Done(Send.MoveToElement(rect, Direct)));
            Assert.False(await Done(Send.ClickElement(rect, Direct)));
        }
        Assert.Empty(sent);
    }

    [Fact]
    public async Task CancelledTokenSendsNothing()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var options = Direct with { Cancel = source.Token };
        Assert.False(await Done(Send.MoveToElement(Box, options)));
        Assert.False(await Done(Send.ClickElement(Box, options)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task NoGameWindowSendsNothing()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.ToScreen = (_, _) => null;
        Assert.False(await Done(Send.MoveToElement(Box, Direct)));
        Assert.False(await Done(Send.ClickElement(Box, Direct)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ChatOpenSendsNothing()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        ChatIs(true);
        Assert.False(await Done(Send.MoveToElement(Box, Direct)));
        Assert.False(await Done(Send.ClickElement(Box, Direct)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ConfigOverrideReachesTheHumanizer()
    {
        TestSend.Record();
        var waits = TestSend.Instant();
        var options = new ElementOptions { Mode = MouseMode.Humanized, Config = Distinct, Cancel = TestContext.Current.CancellationToken };
        Assert.True(await Done(Send.ClickElement(Box, options)));
        Assert.Equal([100, 200, 300], waits[^3..]);
    }

    static ScreenRect Rect(float x1, float y1, float x2, float y2) => new(new Vector2(x1, y1), new Vector2(x2, y2));

    static List<(int X, int Y)> Draw(ScreenRect rect, int margin, int count = 5000)
    {
        var rng = new Random(1);
        return Enumerable.Range(0, count).Select(_ => Send.PickPoint(rect, margin, rng)!.Value).ToList();
    }

    [Fact]
    public void PickPointStaysInsideTheInsetRect()
    {
        foreach (var (x, y) in Draw(Rect(100, 200, 300, 260), 4))
        {
            Assert.InRange(x, 104, 296);
            Assert.InRange(y, 204, 256);
        }
    }

    [Fact]
    public void PickPointFavoursTheCentre()
    {
        var points = Draw(Rect(100, 200, 300, 260), 4);
        Assert.True(points.Count(p => p.X is >= 150 and <= 250) > points.Count * 0.6);
        Assert.InRange(points.Average(p => p.X), 197, 203);
    }

    [Fact]
    public void PickPointUsesCentreWhenRectIsSmallerThanTwiceTheMargin()
    {
        var points = Draw(Rect(10, 10, 16, 40), 4);
        Assert.All(points, p => Assert.Equal(13, p.X));
        Assert.All(points, p => Assert.InRange(p.Y, 14, 36));
        Assert.True(points.Select(p => p.Y).Distinct().Count() > 1);
    }

    [Fact]
    public void PickPointRejectsBadRects()
    {
        var rng = new Random(1);
        Assert.Null(Send.PickPoint(null, 4, rng));
        Assert.Null(Send.PickPoint(Rect(5, 5, 5, 20), 4, rng));
        Assert.Null(Send.PickPoint(Rect(20, 20, 10, 10), 4, rng));
        Assert.Null(Send.PickPoint(Rect(float.NaN, 0, 10, 10), 4, rng));
        Assert.Null(Send.PickPoint(Rect(0, 0, float.PositiveInfinity, 10), 4, rng));
    }

    [Fact]
    public void PickPointTreatsNegativeMarginAsZero()
    {
        foreach (var (x, y) in Draw(Rect(0, 0, 10, 10), -5))
        {
            Assert.InRange(x, 0, 10);
            Assert.InRange(y, 0, 10);
        }
    }
}
