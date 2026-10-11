namespace Aember.Api.Tests;

public class BackgroundJobTests
{
    [Fact]
    public async Task AnInlineJobIsTakenOnceWithItsResult()
    {
        var job = new BackgroundJob<int>(inline: true);

        job.Start(() => 42);

        Assert.True(job.Running);
        Assert.Equal(42, await job.TakeFinished()!);
        Assert.False(job.Running);
        Assert.Null(job.TakeFinished());
    }

    [Fact]
    public void AFaultedJobIsTakenWithItsException()
    {
        var job = new BackgroundJob<int>(inline: true);

        job.Start(() => throw new InvalidOperationException("boom"));

        var finished = job.TakeFinished()!;
        Assert.False(finished.IsCompletedSuccessfully);
        Assert.Equal("boom", finished.Exception!.GetBaseException().Message);
    }

    [Fact]
    public void StartingAgainBeforeTheResultIsTakenThrows()
    {
        var job = new BackgroundJob<int>(inline: true);
        job.Start(() => 1);

        Assert.Throws<InvalidOperationException>(() => job.Start(() => 2));
    }

    [Fact]
    public async Task ARealJobRunsOffTheCallingThread()
    {
        var job = new BackgroundJob<int>();
        var caller = Environment.CurrentManagedThreadId;
        Task<int>? finished = null;

        job.Start(() => Environment.CurrentManagedThreadId);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((finished = job.TakeFinished()) is null && DateTime.UtcNow < deadline) await Task.Delay(1, TestContext.Current.CancellationToken);
        Assert.NotNull(finished);
        Assert.NotEqual(caller, await finished!);
    }
}
