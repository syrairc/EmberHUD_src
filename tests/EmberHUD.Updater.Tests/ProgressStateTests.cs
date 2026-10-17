using EmberHUD.Updater.Ui;

namespace EmberHUD.Updater.Tests;

public class ProgressStateTests
{
    static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) Thread.Sleep(5);
        return condition();
    }

    [Fact]
    public void SnapshotReflectsTheLastReport()
    {
        var state = new ProgressState();
        state.Report(1, 10, "a.dll");
        state.Report(4, 10, "b.dll");
        Assert.Equal((4, 10, "b.dll"), state.Snapshot());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AskBlocksUntilAnsweredAndReturnsTheAnswer(bool answer)
    {
        var state = new ProgressState();
        var ask = Task.Run(() => state.Ask("close it"), TestContext.Current.CancellationToken);
        Assert.True(WaitFor(() => state.PendingPrompt == "close it"));
        Assert.False(ask.IsCompleted);
        state.Answer(answer);
        Assert.Equal(answer, await ask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PendingPromptIsNullAfterAnswer()
    {
        var state = new ProgressState();
        var ask = Task.Run(() => state.Ask("close it"), TestContext.Current.CancellationToken);
        Assert.True(WaitFor(() => state.PendingPrompt is not null));
        state.Answer(true);
        await ask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Null(state.PendingPrompt);
    }

    [Fact]
    public async Task AnswerFalseOnShutdownReleasesAWaitingAsk()
    {
        var state = new ProgressState();
        var ask = Task.Run(() => state.Ask("close it"), TestContext.Current.CancellationToken);
        Assert.True(WaitFor(() => state.PendingPrompt is not null));
        state.Close();
        Assert.False(await ask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.False(state.Ask("again"));
    }

    [Fact]
    public async Task SecondAnswerBeforeTheWorkerWakesIsIgnored()
    {
        var state = new ProgressState();
        var ask = Task.Run(() => state.Ask("close it"), TestContext.Current.CancellationToken);
        Assert.True(WaitFor(() => state.PendingPrompt is not null));
        state.Answer(true);
        state.Answer(false);
        Assert.True(await ask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnswerWithNoPendingPromptIsIgnored()
    {
        var state = new ProgressState();
        state.Answer(true);
        var ask = Task.Run(() => state.Ask("close it"), TestContext.Current.CancellationToken);
        Assert.True(WaitFor(() => state.PendingPrompt is not null));
        Assert.False(ask.IsCompleted);
        state.Close();
    }

    [Fact]
    public void FinishPublishesTheResult()
    {
        var state = new ProgressState();
        Assert.Null(state.Result);
        state.Finish(new UpdateResult { Outcome = "failed", Reason = "x" });
        Assert.Equal("failed", state.Result?.Outcome);
    }
}
