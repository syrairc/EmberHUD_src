using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class LogStoreTests
{
    static LogEntry Entry(string message) => new(new DateTime(2026, 10, 21, 10, 0, 0), LogLevel.Info, "A", message);

    [Fact]
    public void LogStoreWithoutAProviderIsEmpty()
    {
        TestLog.Reset();

        Assert.Equal(0, LogStore.Version);
        Assert.Empty(LogStore.Today(10));
        Assert.Empty(LogStore.Days());
        Assert.Empty(LogStore.Read(new DateOnly(2026, 10, 21), 10));
    }

    [Fact]
    public void TestLogFeedsTheStore()
    {
        var a = Entry("a");
        var b = Entry("b");

        TestLog.Use(a, b);
        var version = LogStore.Version;

        Assert.Equal([a, b], LogStore.Today(10));
        Assert.Equal([b], LogStore.Today(1));

        TestLog.Use(a);
        Assert.NotEqual(version, LogStore.Version);

        TestLog.Reset();
        Assert.Empty(LogStore.Today(10));
    }
}
