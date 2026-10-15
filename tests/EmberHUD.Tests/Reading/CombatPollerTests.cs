using EmberHUD.Host;

namespace EmberHUD.Tests;

public class CombatPollerTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(5);

    [Fact]
    public void PollsWhileWanted()
    {
        var count = 0;
        using var poller = new CombatPoller(() => Interlocked.Increment(ref count), () => true, _ => { });
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref count) > 3, Generous));
    }

    [Fact]
    public void DoesNotPollWhenNotWanted()
    {
        var count = 0;
        using var poller = new CombatPoller(() => Interlocked.Increment(ref count), () => false, _ => { });
        Thread.Sleep(250);
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public void LogsAThrowingPollOnce()
    {
        var logged = 0;
        var polls = 0;
        using var poller = new CombatPoller(() =>
        {
            Interlocked.Increment(ref polls);
            throw new InvalidOperationException("boom");
        }, () => true, _ => Interlocked.Increment(ref logged));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref polls) > 3, Generous));
        Assert.Equal(1, Volatile.Read(ref logged));
    }

    [Fact]
    public void DisposeStopsPolling()
    {
        var count = 0;
        var poller = new CombatPoller(() => Interlocked.Increment(ref count), () => true, _ => { });
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref count) > 0, Generous));
        poller.Dispose();
        var after = Volatile.Read(ref count);
        Thread.Sleep(250);
        Assert.Equal(after, Volatile.Read(ref count));
    }
}
