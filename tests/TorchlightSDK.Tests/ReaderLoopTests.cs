namespace TorchlightSDK.Tests;

public class ReaderLoopTests
{
    private sealed class Scripted(params Func<GameState>[] steps) : IGameSource
    {
        private int _i;
        public bool TryRead(out GameState state, out string? failure)
        {
            var step = steps[Math.Min(_i++, steps.Length - 1)];
            state = step();
            failure = null;
            return true;
        }
        public void Dispose() { }
    }

    private sealed class Failing(GameState good, string? failure) : IGameSource
    {
        private int _i;
        public bool TryRead(out GameState state, out string? failure1)
        {
            state = good;
            failure1 = failure;
            return _i++ == 0;
        }
        public void Dispose() { }
    }

    [Fact]
    public void ThrowingSourceKeepsTheLastStateAndReportsTheFailure()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => throw new InvalidOperationException("boom")), _ => { });
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status.Contains("boom"), 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
    }

    [Fact]
    public void LoopKeepsRunningAfterAFailure()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => throw new InvalidOperationException("boom"), () => good), _ => { });
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "ok", 2000));
    }

    [Fact]
    public void ReportedFailureKeepsTheLastStateAndShowsTheFailureText()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Failing(good, "game not running"), _ => { });
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "game not running", 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
    }

    [Fact]
    public void ReportedFailureWithoutTextFallsBackToReadFailed()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Failing(good, null), _ => { });
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "read failed", 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
    }

    [Fact]
    public void SourceCreationFailureIsReportedThenRecovers()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var recovered = new ManualResetEventSlim();
        var logged = new List<string>();
        using var loop = new ReaderLoop(
            () => recovered.IsSet ? new Scripted(() => good) : throw new InvalidOperationException("no process"),
            message => { lock (logged) logged.Add(message); });
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status.Contains("no process"), 2000));
        recovered.Set();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "ok", 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
        lock (logged) Assert.Single(logged);
    }

    private sealed class Counting : IGameSource
    {
        public int Reads;
        public bool TryRead(out GameState state, out string? failure)
        {
            Interlocked.Increment(ref Reads);
            state = GameState.Empty;
            failure = null;
            return true;
        }
        public void Dispose() { }
    }

    private static int ReadsOver300Milliseconds(Func<Counting, ReaderLoop> create)
    {
        var source = new Counting();
        using var loop = create(source);
        loop.Start();
        Thread.Sleep(300);
        return Volatile.Read(ref source.Reads);
    }

    [Fact]
    public void TickMillisecondsSetsHowOftenTheSourceIsRead()
    {
        var fast = ReadsOver300Milliseconds(source => new ReaderLoop(() => source, _ => { }));
        var slow = ReadsOver300Milliseconds(source => new ReaderLoop(() => source, _ => { }, 50));
        Assert.True(slow <= 10, $"slow loop read {slow} times");
        Assert.True(fast > slow, $"fast {fast} slow {slow}");
    }
}
