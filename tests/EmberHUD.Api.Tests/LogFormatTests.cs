namespace EmberHUD.Api.Tests;

public class LogFormatTests
{
    static readonly DateOnly Day = new(2026, 10, 21);

    static LogEntry Entry(string message, LogLevel level = LogLevel.Warn, string source = "Pickit") =>
        new(new DateTime(2026, 10, 21, 10, 42, 17, 512), level, source, message);

    [Fact]
    public void FormatWritesTheDocumentedLine()
    {
        Assert.Equal("10:42:17.512 WARN  [Pickit] hi", LogFormat.Format(Entry("hi")));
    }

    [Fact]
    public void ContinuationLinesAreIndented()
    {
        Assert.Equal("10:42:17.512 WARN  [Pickit] boom\n    at A\n    at B", LogFormat.Format(Entry("boom\nat A\r\nat B")));
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("boom\nat A\nat B")]
    [InlineData("")]
    public void ParseRoundTripsFormat(string message)
    {
        var entry = Entry(message);

        var parsed = LogFormat.Parse(LogFormat.Format(entry).Split('\n'), Day);

        Assert.Equal([entry], parsed);
    }

    [Fact]
    public void ParseRoundTripsCrLf()
    {
        var parsed = LogFormat.Parse(LogFormat.Format(Entry("boom\r\nat A")).Split('\n'), Day);

        Assert.Equal([Entry("boom\nat A")], parsed);
    }

    [Fact]
    public void AContinuationDoesNotNeedATimestampLookalike()
    {
        var entry = Entry("x\n10:00:00.000 INFO  [A] fake");

        var parsed = LogFormat.Parse(LogFormat.Format(entry).Split('\n'), Day);

        Assert.Equal([entry], parsed);
    }

    [Fact]
    public void GarbageNeverThrows()
    {
        var parsed = LogFormat.Parse(["", "   ", "not a log line", "10:42:17.512 WARN [x", "99:99:99.999 INFO  [x] y", "24:00:00.000 WARN  [x] y"], Day);

        Assert.Equal(4, parsed.Count);
        Assert.All(parsed, e => Assert.Equal("", e.Source));
        Assert.Equal("not a log line", parsed[0].Message);
        Assert.Equal(LogLevel.Info, parsed[0].Level);
        Assert.Equal("", parsed[0].Source);
    }

    [Fact]
    public void UnicodeDigitsInAHeaderMakeAStrayEntry()
    {
        var parsed = LogFormat.Parse(["١٠:٤٢:١٧.٥١٢ INFO  [x] y", "1٠:42:17.512 INFO  [x] y", "10:42:17.٥١٢ INFO  [x] y"], Day);

        Assert.Equal(3, parsed.Count);
        Assert.All(parsed, e => Assert.Equal("", e.Source));
        Assert.All(parsed, e => Assert.Equal(new DateTime(2026, 10, 21), e.Time));
    }

    [Fact]
    public void LevelNamesArePaddedToFive()
    {
        foreach (var level in Enum.GetValues<LogLevel>()) Assert.Equal(5, LogFormat.Level(level).Length);
    }
}
