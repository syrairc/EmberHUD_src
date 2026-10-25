using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class SendMouseTests : IDisposable
{
    static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    static readonly HumanizerConfig Distinct = new()
    {
        StepDelayMin = 1, StepDelayMax = 2,
        PreClickSettleMin = 100, PreClickSettleMax = 100,
        ClickDwellMin = 200, ClickDwellMax = 200,
        PostClickSettleMin = 300, PostClickSettleMax = 300,
    };

    public SendMouseTests()
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

    static Task<bool> MoveTo(int x, int y, MouseMode? mode, CancellationToken? cancel = null) =>
        Send.MoveTo(x, y, mode, cancel ?? TestContext.Current.CancellationToken);

    static Task<bool> MoveTo(int x, int y) => MoveTo(x, y, null);

    static Task<bool> ClickAt(int x, int y, MouseButton button, MouseMode? mode, CancellationToken? cancel = null) =>
        Send.ClickAt(x, y, button, mode, cancel ?? TestContext.Current.CancellationToken);

    static void WaitFor(ManualResetEventSlim signal) => Assert.True(signal.Wait(Limit, TestContext.Current.CancellationToken));

    static SentInput Move(int x, int y) => new(Key.None, null, false, x, y);

    static SentInput ButtonDown(MouseButton button) => new(Key.None, button, true, 0, 0);

    static SentInput ButtonUp(MouseButton button) => new(Key.None, button, false, 0, 0);

    [Fact]
    public async Task DirectMoveSendsOneMoveEvent()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Assert.True(await Done(MoveTo(10, 20, MouseMode.Direct)));
        Assert.Equal([Move(10, 20)], sent);
    }

    [Fact]
    public async Task HumanizedMoveSendsManyStepsEndingOnTheTarget()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        Assert.True(await Done(MoveTo(300, 200, MouseMode.Humanized)));
        Assert.True(sent.Count > 5);
        Assert.All(sent, input => Assert.True(input.Key == Key.None && input.Button is null));
        Assert.Equal(Move(300, 200), sent[^1]);
        Assert.Equal(sent.Count, waits.Count);
        var config = new HumanizerConfig();
        Assert.All(waits, wait => Assert.InRange(wait, config.StepDelayMin, config.StepDelayMax));
    }

    [Fact]
    public async Task ModeArgumentOverridesTheGlobalMode()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Mode = MouseMode.Humanized;
        Assert.True(await Done(MoveTo(300, 200, MouseMode.Direct)));
        Assert.Single(sent);
        sent.Clear();
        Send.Mode = MouseMode.Direct;
        Assert.True(await Done(MoveTo(300, 200, MouseMode.Humanized)));
        Assert.True(sent.Count > 1);
    }

    [Fact]
    public async Task GlobalModeAppliesWhenNoArgument()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Mode = MouseMode.Direct;
        Assert.True(await Done(MoveTo(300, 200)));
        Assert.Single(sent);
        sent.Clear();
        Send.Mode = MouseMode.Humanized;
        Assert.True(await Done(MoveTo(300, 200)));
        Assert.True(sent.Count > 1);
    }

    [Fact]
    public async Task HumanizedClickIsMovesThenDownDwellUp()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        Send.Config = Distinct;
        Assert.True(await Done(ClickAt(30, 0, MouseButton.Right, MouseMode.Humanized)));
        Assert.Equal([ButtonDown(MouseButton.Right), ButtonUp(MouseButton.Right)], sent[^2..]);
        var moves = sent[..^2];
        Assert.NotEmpty(moves);
        Assert.All(moves, input => Assert.True(input.Button is null && input.Key == Key.None));
        Assert.Equal(Move(30, 0), moves[^1]);
        Assert.Equal(moves.Count + 3, waits.Count);
        Assert.All(waits.Take(moves.Count), wait => Assert.InRange(wait, 1, 2));
        Assert.Equal([100, 200, 300], waits.Skip(moves.Count));
    }

    [Fact]
    public async Task DirectClickIsMoveDownUpWithNoWaits()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        Assert.True(await Done(ClickAt(5, 6, MouseButton.Right, MouseMode.Direct)));
        Assert.Equal([Move(5, 6), ButtonDown(MouseButton.Right), ButtonUp(MouseButton.Right)], sent);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task ClickRefusedByTheCapLeavesTheCursorOnTheTargetAndReturnsFalse()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Clock = () => 1_000_000;
        for (var i = 0; i < 30; i++) Assert.True(Send.Down(Key.W));
        Assert.False(await Done(ClickAt(5, 5, MouseButton.Left, MouseMode.Direct)));
        Assert.Equal(Move(5, 5), sent[^1]);
        Assert.DoesNotContain(sent, input => input.Button is not null);
    }

    [Fact]
    public async Task MovesDoNotCountTowardsTheCap()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Clock = () => 1_000_000;
        Assert.True(await Done(MoveTo(1900, 1000, MouseMode.Humanized)));
        Assert.True(await Done(MoveTo(1900, 1000, MouseMode.Humanized)));
        Assert.True(await Done(MoveTo(0, 0, MouseMode.Humanized)));
        Assert.True(sent.Count >= 200);
        Assert.True(Send.Down(Key.W));
    }

    [Fact]
    public async Task ClickRefusedByTheCapDoesNotReleaseAButtonTheCallerHolds()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Clock = () => 1_000_000;
        for (var i = 0; i < 29; i++) Assert.True(Send.Down(Key.W));
        Assert.True(Send.MouseDown(MouseButton.Left));
        sent.Clear();
        Assert.False(await Done(ClickAt(5, 5, MouseButton.Left, MouseMode.Direct)));
        Assert.DoesNotContain(sent, input => input.Button is not null);
        Send.ReleaseAll();
        Assert.Contains(ButtonUp(MouseButton.Left), sent);
    }

    [Fact]
    public async Task ClickWhileNotAllowedDoesNotReleaseAButtonTheCallerHolds()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Assert.True(Send.MouseDown(MouseButton.Left));
        sent.Clear();
        ChatIs(true);
        Assert.False(await Done(ClickAt(5, 5, MouseButton.Left, MouseMode.Direct)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task CallsRunInOrderAcrossCallers()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        Send.Wait = ms =>
        {
            waits.Add(ms);
            if (entered.IsSet) return;
            entered.Set();
            proceed.Wait(Limit);
        };
        var first = MoveTo(100, 0, MouseMode.Humanized);
        WaitFor(entered);
        var second = await Task.Factory.StartNew(() => MoveTo(1, 1, MouseMode.Direct), TestContext.Current.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
        var third = MoveTo(2, 2, MouseMode.Direct);
        proceed.Set();
        Assert.True(await Done(first));
        Assert.True(await Done(second));
        Assert.True(await Done(third));
        Assert.Equal([Move(1, 1), Move(2, 2)], sent[^2..]);
        Assert.Equal(Move(100, 0), sent[^3]);
        Assert.DoesNotContain(sent[..^2], input => input == Move(1, 1) || input == Move(2, 2));
    }

    [Fact]
    public async Task LosingFocusMidPathStopsTheMoveReleasesTheButtonAndFailsQueuedCalls()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        Send.Config = Distinct;
        using var atPreSettle = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        Send.Wait = ms =>
        {
            waits.Add(ms);
            if (ms == 100)
            {
                atPreSettle.Set();
                proceed.Wait(Limit);
            }
            else if (ms == 200) ChatIs(true);
        };
        var click = ClickAt(0, 0, MouseButton.Left, MouseMode.Humanized);
        WaitFor(atPreSettle);
        var queued = MoveTo(50, 50, MouseMode.Direct);
        proceed.Set();
        Assert.False(await Done(click));
        Assert.False(await Done(queued));
        Assert.Equal([Move(0, 0), ButtonDown(MouseButton.Left), ButtonUp(MouseButton.Left)], sent);
        Assert.DoesNotContain(300, waits);
    }

    [Fact]
    public async Task ReleaseAllCancelsTheInFlightCallAndNothingSendsAfterItReturns()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        Send.Wait = ms =>
        {
            waits.Add(ms);
            if (entered.IsSet) return;
            entered.Set();
            proceed.Wait(Limit);
        };
        var running = MoveTo(500, 500, MouseMode.Humanized);
        WaitFor(entered);
        var queued = MoveTo(1, 1, MouseMode.Direct);
        Send.ReleaseAll();
        var countAtReturn = sent.Count;
        proceed.Set();
        Assert.False(await Done(running));
        Assert.False(await Done(queued));
        Assert.Equal(countAtReturn, sent.Count);
    }

    [Fact]
    public async Task CancelledTokenBeforeRunFailsWithoutEvents()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.False(await Done(MoveTo(10, 10, MouseMode.Direct, cancelled.Token)));
        Assert.False(await Done(ClickAt(10, 10, MouseButton.Left, MouseMode.Humanized, cancelled.Token)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task CancellingAQueuedCallLeavesTheOthersRunning()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource();
        Send.Wait = ms =>
        {
            waits.Add(ms);
            if (entered.IsSet) return;
            entered.Set();
            proceed.Wait(Limit);
        };
        var running = MoveTo(40, 0, MouseMode.Humanized);
        WaitFor(entered);
        var doomed = MoveTo(1, 1, MouseMode.Direct, cancel.Token);
        var survivor = MoveTo(2, 2, MouseMode.Direct);
        cancel.Cancel();
        proceed.Set();
        Assert.True(await Done(running));
        Assert.False(await Done(doomed));
        Assert.True(await Done(survivor));
        Assert.DoesNotContain(Move(1, 1), sent);
        Assert.Equal(Move(2, 2), sent[^1]);
    }

    [Fact]
    public async Task ToScreenNullFailsWithoutEvents()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.ToScreen = (_, _) => null;
        Assert.False(await Done(MoveTo(10, 10, MouseMode.Direct)));
        Assert.False(await Done(ClickAt(10, 10, MouseButton.Left, MouseMode.Humanized)));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ThrowingSinkFailsTheCallButTheWorkerKeepsServing()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        var count = 0;
        Send.Sink = input =>
        {
            if (++count == 3) throw new InvalidOperationException("sink");
            sent.Add(input);
        };
        Assert.False(await Done(MoveTo(300, 200, MouseMode.Humanized)));
        Assert.Equal(2, sent.Count);
        Assert.True(await Done(MoveTo(7, 8, MouseMode.Direct)));
        Assert.Equal(Move(7, 8), sent[^1]);
    }

    [Fact]
    public async Task ThrowingSinkOnButtonDownStillReleasesTheButton()
    {
        var sent = TestSend.Record();
        TestSend.Instant();
        Send.Sink = input =>
        {
            sent.Add(input);
            if (input.Down && input.Button is not null) throw new InvalidOperationException("sink");
        };
        Assert.False(await Done(ClickAt(5, 5, MouseButton.Left, MouseMode.Direct)));
        Assert.Equal(ButtonUp(MouseButton.Left), sent[^1]);
    }

    [Fact]
    public async Task ThirtyThirdPendingCallReturnsFalseAtOnce()
    {
        var sent = TestSend.Record();
        var waits = TestSend.Instant();
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        Send.Wait = ms =>
        {
            waits.Add(ms);
            if (entered.IsSet) return;
            entered.Set();
            proceed.Wait(Limit);
        };
        var running = MoveTo(50, 50, MouseMode.Humanized);
        WaitFor(entered);
        var pending = Enumerable.Range(0, 32).Select(i => MoveTo(i, i, MouseMode.Direct)).ToArray();
        Assert.All(pending, call => Assert.False(call.IsCompleted));
        var overflow = MoveTo(99, 99, MouseMode.Direct);
        Assert.True(overflow.IsCompleted);
        Assert.False(await overflow);
        proceed.Set();
        Assert.True(await Done(running));
        foreach (var call in pending) Assert.True(await Done(call));
        Assert.DoesNotContain(Move(99, 99), sent);
    }

    [Fact]
    public async Task CursorAlreadyOnTargetSendsOneEvent()
    {
        var sent = TestSend.Record();
        TestSend.Instant(cursor: (7, 7));
        Assert.True(await Done(MoveTo(7, 7, MouseMode.Humanized)));
        Assert.Equal([Move(7, 7)], sent);
    }
}
