using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class SendTests : IDisposable
{
    public SendTests() => TestSend.Reset();

    public void Dispose()
    {
        TestSend.Reset();
        TestGame.Set(GameState.Empty);
    }

    static void ChatIs(bool? open) => TestGame.Set(GameState.Empty with { ChatOpen = open });

    [Fact]
    public void PressSendsDownThenUp()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Assert.True(Send.Press(Key.F5));
        Assert.Equal([new SentInput(Key.F5, null, true, 0, 0), new SentInput(Key.F5, null, false, 0, 0)], sent);
    }

    [Fact]
    public void NothingSendsWhenTheGameIsNotForeground()
    {
        ChatIs(false);
        var sent = TestSend.Record(allowed: false);
        Assert.False(Send.Press(Key.F5));
        Assert.False(Send.Down(Key.F5));
        Assert.False(Send.Click(MouseButton.Left));
        Assert.False(Send.MouseDown(MouseButton.Right));
        Assert.Empty(sent);
    }

    [Fact]
    public void NothingSendsWhenChatIsOpen()
    {
        ChatIs(true);
        var sent = TestSend.Record();
        Assert.False(Send.Allowed);
        Assert.False(Send.Press(Key.F5));
        Assert.Empty(sent);
    }

    [Fact]
    public void NothingSendsWhenChatStateIsUnknown()
    {
        ChatIs(null);
        var sent = TestSend.Record();
        Assert.False(Send.Press(Key.F5));
        Assert.Empty(sent);
    }

    [Fact]
    public void KeyNoneNeverSends()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Assert.False(Send.Press(Key.None));
        Assert.False(Send.Down(Key.None));
        Assert.False(Send.Up(Key.None));
        Assert.Empty(sent);
    }

    [Fact]
    public void KeyUpSendsEvenWhenTheGateFails()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Assert.True(Send.Down(Key.W));
        ChatIs(true);
        Assert.True(Send.Up(Key.W));
        Assert.Equal(new SentInput(Key.W, null, false, 0, 0), sent[^1]);
    }

    [Fact]
    public void TickReleasesHeldKeysAsSoonAsTheGateFails()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Send.Down(Key.W);
        Send.MouseDown(MouseButton.Left);
        Send.Tick();
        Assert.Equal(2, sent.Count);
        ChatIs(true);
        Send.Tick();
        Assert.Equal(4, sent.Count);
        Assert.Contains(new SentInput(Key.W, null, false, 0, 0), sent);
        Assert.Contains(new SentInput(Key.None, MouseButton.Left, false, 0, 0), sent);
        Send.Tick();
        Assert.Equal(4, sent.Count);
    }

    [Fact]
    public void ReleaseAllReleasesEveryHeldKeyOnce()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Send.Down(Key.W);
        Send.Down(Key.A);
        Send.ReleaseAll();
        Send.ReleaseAll();
        Assert.Equal(2, sent.Count(input => !input.Down));
        Assert.Equal(4, sent.Count);
    }

    [Fact]
    public void RateLimitRefusesPastThirtyEventsInOneSecond()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        long now = 1_000_000;
        Send.Clock = () => now;
        for (var i = 0; i < 30; i++) Assert.True(Send.Down(Key.W));
        Assert.False(Send.Down(Key.W));
        Assert.Equal(30, sent.Count);
        now += System.Diagnostics.Stopwatch.Frequency + 1;
        Assert.True(Send.Down(Key.W));
        Assert.Equal(31, sent.Count);
    }

    [Fact]
    public void UpBypassesTheRateLimit()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Send.Clock = () => 1_000_000;
        for (var i = 0; i < 30; i++) Send.Down(Key.W);
        Assert.True(Send.Up(Key.W));
        Assert.Equal(31, sent.Count);
    }

    [Fact]
    public void MouseMoveConvertsClientToScreen()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Send.ToScreen = (x, y) => (x + 100, y + 200);
        Assert.True(Send.MouseMove(5, 6));
        Assert.Equal(new SentInput(Key.None, null, false, 105, 206), Assert.Single(sent));
    }

    [Fact]
    public void MouseMoveWithoutAWindowDoesNotSend()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Send.ToScreen = null;
        Assert.False(Send.MouseMove(5, 6));
        Send.ToScreen = (_, _) => null;
        Assert.False(Send.MouseMove(5, 6));
        Assert.Empty(sent);
    }

    [Fact]
    public void ClickIsDownThenUp()
    {
        ChatIs(false);
        var sent = TestSend.Record();
        Assert.True(Send.Click(MouseButton.Right));
        Assert.Equal([new SentInput(Key.None, MouseButton.Right, true, 0, 0), new SentInput(Key.None, MouseButton.Right, false, 0, 0)], sent);
    }

    [Fact]
    public async Task ReleaseAllWaitsForADownMidSendSoTheKeyIsNeverStuck()
    {
        ChatIs(false);
        var sent = new List<SentInput>();
        using var inSink = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        TestSend.Record();
        Send.Sink = input =>
        {
            if (input.Down)
            {
                inSink.Set();
                proceed.Wait();
            }
            sent.Add(input);
        };
        var down = Task.Run(() => Send.Down(Key.W), TestContext.Current.CancellationToken);
        Assert.True(inSink.Wait(5000, TestContext.Current.CancellationToken));
        var release = Task.Run(Send.ReleaseAll, TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(release.IsCompleted);
        proceed.Set();
        await down;
        await release;
        Assert.Equal([new SentInput(Key.W, null, true, 0, 0), new SentInput(Key.W, null, false, 0, 0)], sent);
    }
}
