namespace EmberHUD.Assets.Tests;

public class JobRunnerTests
{
    sealed class FakeJob(Func<Action<string>, CancellationToken, AssetResult> run) : IAssetJob
    {
        public AssetType Type => AssetType.Items;
        public string Title => "Fake";
        public string Folder => "Items";
        public AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks) => new(AssetState.NotExtracted, 0, 0);
        public AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel) => run(progress, cancel);
    }

    static void WaitIdle(JobRunner runner) => SpinWait.SpinUntil(() => !runner.Busy, TimeSpan.FromSeconds(10));

    [Fact]
    public void Runner_SecondStartWhileBusy_ReturnsFalse()
    {
        var release = new ManualResetEventSlim();
        var runner = new JobRunner();
        var job = new FakeJob((_, _) =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return new AssetResult(1, 0, 0);
        });
        Assert.True(runner.TryStart(job, [], "", []));
        Assert.True(runner.Busy);
        Assert.False(runner.TryStart(job, [], "", []));
        release.Set();
        WaitIdle(runner);
        Assert.True(runner.TryStart(new FakeJob((_, _) => new AssetResult(2, 0, 0)), [], "", []));
        WaitIdle(runner);
        Assert.Equal(new AssetResult(2, 0, 0), runner.Last);
    }

    [Fact]
    public void Runner_ReportsProgressAndLastResult()
    {
        var seen = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var runner = new JobRunner();
        runner.TryStart(new FakeJob((progress, _) =>
        {
            progress("50 / 100 icons");
            seen.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return new AssetResult(7, 1, 0);
        }), [], "", []);
        Assert.True(seen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("50 / 100 icons", runner.Progress);
        release.Set();
        WaitIdle(runner);
        Assert.Equal(new AssetResult(7, 1, 0), runner.Last);
        Assert.Null(runner.Failure);
    }

    [Fact]
    public void Runner_FailureAndCancel_AreReported()
    {
        var runner = new JobRunner();
        runner.TryStart(new FakeJob((_, _) => throw new InvalidDataException("bad pak")), [], "", []);
        WaitIdle(runner);
        Assert.Equal("bad pak", runner.Failure);
        Assert.Null(runner.Last);

        var started = new ManualResetEventSlim();
        runner.TryStart(new FakeJob((_, cancel) =>
        {
            started.Set();
            cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancel.ThrowIfCancellationRequested();
            return new AssetResult(0, 0, 0);
        }), [], "", []);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        runner.Cancel();
        WaitIdle(runner);
        Assert.Equal("cancelled", runner.Failure);
    }

    [Fact]
    public void Runner_NewStart_ClearsThePreviousResultAndFailure()
    {
        var runner = new JobRunner();
        runner.TryStart(new FakeJob((_, _) => new AssetResult(5, 0, 0)), [], "", []);
        WaitIdle(runner);
        var started = new ManualResetEventSlim();
        runner.TryStart(new FakeJob((_, cancel) =>
        {
            started.Set();
            cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancel.ThrowIfCancellationRequested();
            return new AssetResult(0, 0, 0);
        }), [], "", []);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Null(runner.Last);
        runner.Cancel();
        WaitIdle(runner);
        Assert.Null(runner.Last);
        Assert.Equal("cancelled", runner.Failure);
        runner.TryStart(new FakeJob((_, _) => new AssetResult(1, 0, 0)), [], "", []);
        WaitIdle(runner);
        Assert.Null(runner.Failure);
    }

    [Fact]
    public void Runner_LogsTheFullExceptionOnFailureButNotOnCancel()
    {
        List<string> logged = [];
        var runner = new JobRunner(logged.Add);
        runner.TryStart(new FakeJob((_, _) => throw new InvalidDataException("boom")), [], "", []);
        WaitIdle(runner);
        Assert.Equal("boom", runner.Failure);
        var text = Assert.Single(logged);
        Assert.Contains("InvalidDataException", text);
        Assert.Contains("boom", text);

        runner.TryStart(new FakeJob((_, _) => throw new OperationCanceledException()), [], "", []);
        WaitIdle(runner);
        Assert.Equal("cancelled", runner.Failure);
        Assert.Single(logged);
    }
}
