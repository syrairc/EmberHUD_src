namespace EmberHUD.Tests;

public class ReaderLoopTests
{
    private sealed class Scripted(params Func<GameState?>[] steps) : IGameSource
    {
        private int _i;
        public string? Failure { get; init; }
        public bool TryRead(out GameState state, out string? failure)
        {
            var next = steps[Math.Min(_i++, steps.Length - 1)]();
            state = next ?? GameState.Empty;
            failure = next is null ? Failure : null;
            return next is not null;
        }
        public void Dispose() { }
    }

    [Fact]
    public void ThrowingSourceKeepsTheLastStateAndReportsTheFailure()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => throw new InvalidOperationException("boom")), _ => { }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status.Contains("boom"), 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
    }

    [Fact]
    public void TypeInitializerFailureReportsAndLogsTheInnerCause()
    {
        var good = GameState.Empty with { Status = "ok" };
        var logged = new List<string>();
        using var loop = new ReaderLoop(
            () => new Scripted(() => good, () => throw new TypeInitializationException("Offsets", new InvalidDataException("resource t missing"))),
            message => { lock (logged) logged.Add(message); }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status.Contains("resource t missing"), 2000));
        lock (logged) Assert.Equal("reader - resource t missing", Assert.Single(logged));
    }

    [Fact]
    public void LoopKeepsRunningAfterAFailure()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => throw new InvalidOperationException("boom"), () => good), _ => { }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "ok", 2000));
    }

    [Fact]
    public void ReportedFailureKeepsTheLastStateAndShowsTheFailureText()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => null) { Failure = "game not running" }, _ => { }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "game not running", 2000));
        Assert.Equal(7, loop.Current.State.AreaId);
    }

    [Fact]
    public void ReportedFailureClearsChatAndPausedToUnknown()
    {
        var good = GameState.Empty with { Status = "ok", ChatOpen = false, Paused = false };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => null), _ => { }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "read failed", 2000));
        Assert.Null(loop.Current.State.ChatOpen);
        Assert.Null(loop.Current.State.Paused);
    }

    [Fact]
    public void ThrowingSourceClearsChatAndPausedToUnknown()
    {
        var good = GameState.Empty with { Status = "ok", ChatOpen = false, Paused = false };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => throw new InvalidOperationException("boom")), _ => { }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status.Contains("boom"), 2000));
        Assert.Null(loop.Current.State.ChatOpen);
        Assert.Null(loop.Current.State.Paused);
    }

    [Fact]
    public void ReportedFailureWithoutTextFallsBackToReadFailed()
    {
        var good = GameState.Empty with { Status = "ok", AreaId = 7 };
        using var loop = new ReaderLoop(() => new Scripted(() => good, () => null), _ => { }, 8);
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
            message => { lock (logged) logged.Add(message); }, 8);
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

    private static int ReadsOver200Milliseconds(Func<Counting, ReaderLoop> create)
    {
        var source = new Counting();
        using var loop = create(source);
        loop.Start();
        Thread.Sleep(200);
        return Volatile.Read(ref source.Reads);
    }

    [Fact]
    public void TickMillisecondsSetsHowOftenTheSourceIsRead()
    {
        var fast = ReadsOver200Milliseconds(source => new ReaderLoop(() => source, _ => { }, 8));
        var slow = ReadsOver200Milliseconds(source => new ReaderLoop(() => source, _ => { }, 50));
        Assert.True(slow <= 8, $"slow loop read {slow} times");
        Assert.True(fast > slow, $"fast {fast} slow {slow}");
    }

    [Fact]
    public void ErrorThatReturnsAfterARecoveryIsLoggedAgain()
    {
        var good = GameState.Empty with { Status = "ok" };
        var logged = new List<string>();
        using var loop = new ReaderLoop(
            () => new Scripted(() => throw new InvalidOperationException("boom"), () => good, () => throw new InvalidOperationException("boom"), () => good),
            message => { lock (logged) logged.Add(message); }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => { lock (logged) return logged.Count == 2; }, 2000));
    }

    [Fact]
    public void SameErrorInARowIsLoggedOnce()
    {
        var good = GameState.Empty with { Status = "ok" };
        var logged = new List<string>();
        using var loop = new ReaderLoop(
            () => new Scripted(() => throw new InvalidOperationException("boom"), () => throw new InvalidOperationException("boom"), () => good),
            message => { lock (logged) logged.Add(message); }, 8);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => loop.Current.State.Status == "ok", 2000));
        lock (logged) Assert.Single(logged);
    }

    [Fact]
    public void DisposingTwiceIsANoOp()
    {
        var loop = new ReaderLoop(() => new Counting(), _ => { }, 8);
        loop.Start();
        loop.Dispose();
        loop.Dispose();
    }

    private sealed class Hanging(ManualResetEventSlim entered, ManualResetEventSlim release) : IGameSource
    {
        public bool TryRead(out GameState state, out string? failure)
        {
            entered.Set();
            release.Wait();
            state = GameState.Empty;
            failure = null;
            return true;
        }
        public void Dispose() { }
    }

    [Fact]
    public async Task DisposeReturnsWhileTheSourceIsHungInARead()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var loop = new ReaderLoop(() => new Hanging(entered, release), _ => { }, 8);
        loop.Start();
        Assert.True(entered.Wait(2000, TestContext.Current.CancellationToken));
        var disposing = Task.Run(loop.Dispose, TestContext.Current.CancellationToken);
        Assert.Same(disposing, await Task.WhenAny(disposing, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        release.Set();
    }

    [Theory]
    [InlineData(33, 0, 33)]
    [InlineData(33, 10, 23)]
    [InlineData(33, 33, 1)]
    [InlineData(33, 500, 1)]
    public void TheWaitIsTheTickMinusTheWorkAndAtLeastOneMillisecond(int tick, int elapsedMilliseconds, int expected)
    {
        Assert.Equal(expected, ReaderLoop.WaitMilliseconds(tick, TimeSpan.FromMilliseconds(elapsedMilliseconds)));
    }

    private sealed class Stamped(int milliseconds) : IGameSource
    {
        private readonly List<long> _starts = [];

        public long[] Starts
        {
            get
            {
                lock (_starts) return [.. _starts];
            }
        }

        public bool TryRead(out GameState state, out string? failure)
        {
            lock (_starts) _starts.Add(System.Diagnostics.Stopwatch.GetTimestamp());
            Thread.Sleep(milliseconds);
            state = GameState.Empty;
            failure = null;
            return true;
        }

        public void Dispose() { }
    }

    [Fact]
    public void ThePeriodIncludesTheTimeTheReadTook()
    {
        var source = new Stamped(60);
        using var loop = new ReaderLoop(() => source, _ => { }, 100);
        loop.Start();

        SpinWait.SpinUntil(() => source.Starts.Length >= 8, TimeSpan.FromSeconds(15));

        var starts = source.Starts;
        Assert.True(starts.Length >= 8, $"{starts.Length} reads");
        var gaps = starts.Zip(starts.Skip(1), (a, b) => System.Diagnostics.Stopwatch.GetElapsedTime(a, b).TotalMilliseconds).Order().ToArray();
        var median = gaps[gaps.Length / 2];
        Assert.True(median < 130, $"median gap {median:0}ms");
    }
}
