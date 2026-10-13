using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public sealed class LogBufferTests
{
    [Fact]
    public void KeepsTheNewestLinesInOrder()
    {
        var log = new LogBuffer(50);
        for (var i = 0; i < 60; i++) log.Add($"line {i}");

        var lines = log.Snapshot();

        Assert.Equal(50, lines.Length);
        Assert.Equal("line 10", lines[0]);
        Assert.Equal("line 59", lines[^1]);
    }
}
