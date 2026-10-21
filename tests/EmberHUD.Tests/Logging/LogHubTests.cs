using EmberHUD.Host.Logging;

namespace EmberHUD.Tests;

public class LogHubTests
{
    private static readonly DateTime Noon = new(2026, 10, 21, 12, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static LogEntry Entry(string message, DateTime? time = null, LogLevel level = LogLevel.Info) => new(time ?? Noon, level, "Host", message);

    private static string DayFile(string dir, DateOnly day) => Path.Combine(dir, DailyLogWriter.FileName(day));

    [Fact]
    public void KeepsTheNewestEntriesUpToCapacity()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon, 50);
        for (var i = 0; i < 60; i++) hub.Add(Entry($"line {i}"));

        var kept = hub.Today(1000);

        Assert.Equal(50, kept.Length);
        Assert.Equal("line 10", kept[0].Message);
        Assert.Equal("line 59", kept[^1].Message);
    }

    [Fact]
    public void VersionChangesOnEveryAdd()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon);
        var seen = new HashSet<int> { hub.Version };
        for (var i = 0; i < 5; i++)
        {
            hub.Add(Entry("x"));
            seen.Add(hub.Version);
        }

        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void SeedsFromTodaysFileTail()
    {
        using var temp = new TempDir();
        File.WriteAllLines(DayFile(temp.Path, Today), Enumerable.Range(0, 5).Select(i => LogFormat.Format(Entry($"old {i}", Noon.AddMinutes(i)))));

        var hub = new LogHub(temp.Path, () => Noon.AddHours(1), capacity: 3);

        Assert.Equal(["old 2", "old 3", "old 4"], hub.Today(10).Select(e => e.Message));
    }

    [Fact]
    public void TodayDropsYesterdaysEntriesAfterMidnight()
    {
        using var temp = new TempDir();
        var now = Noon;
        var hub = new LogHub(temp.Path, () => now);
        hub.Add(Entry("late"));
        Assert.Single(hub.Today(10));

        now = Noon.Date.AddDays(1).AddMinutes(1);

        Assert.Empty(hub.Today(10));
    }

    [Fact]
    public void TodayReturnsTheNewestMax()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon);
        for (var i = 0; i < 5; i++) hub.Add(Entry($"n{i}"));

        Assert.Equal(["n3", "n4"], hub.Today(2).Select(e => e.Message));
    }

    [Fact]
    public void DaysListsDayFilesNewestFirst()
    {
        using var temp = new TempDir();
        foreach (var name in new[] { "ember-2026-10-19.log", "ember-2026-10-21.log", "ember-2026-10-20.log", "ember.log", "ember-notadate.log", "other-2026-10-21.log", "ember-2026-10-22.txt" })
            temp.File(name);

        var hub = new LogHub(temp.Path, () => Noon);

        Assert.Equal([new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 19)], hub.Days());
    }

    [Fact]
    public void DaysIsEmptyWhenTheDirectoryIsMissing()
    {
        using var temp = new TempDir();
        var hub = new LogHub(Path.Combine(temp.Path, "nope"), () => Noon);

        Assert.Empty(hub.Days());
        Assert.Empty(hub.Read(Today, 10));
    }

    [Fact]
    public void ReadReturnsTheTailOfAnOldDay()
    {
        using var temp = new TempDir();
        var old = new DateOnly(2026, 10, 18);
        File.WriteAllLines(DayFile(temp.Path, old), Enumerable.Range(0, 5).Select(i => LogFormat.Format(Entry($"e{i}", new DateTime(2026, 10, 18, 9, i, 0)))));
        var hub = new LogHub(temp.Path, () => Noon);

        var tail = hub.Read(old, 2);

        Assert.Equal(["e3", "e4"], tail.Select(e => e.Message));
        Assert.Equal(old, DateOnly.FromDateTime(tail[0].Time));
    }

    [Fact]
    public void ReadSurvivesGarbageAndMissingFiles()
    {
        using var temp = new TempDir();
        var day = new DateOnly(2026, 10, 18);
        File.WriteAllText(DayFile(temp.Path, day), "garbage\n\u0000\u0001 junk\n09:00:00.000 INFO  [Host] fine\n09:00:01.000 WA");
        var hub = new LogHub(temp.Path, () => Noon);

        var entries = hub.Read(day, 100);

        Assert.Contains(entries, e => e.Message == "fine" && e.Source == "Host");
        Assert.Empty(hub.Read(new DateOnly(2020, 1, 1), 100));
    }

    [Fact]
    public void ReadWorksWhileTheWriterHasTheFileOpen()
    {
        using var temp = new TempDir();
        using var writer = new FileStream(DayFile(temp.Path, Today), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var text = new StreamWriter(writer) { AutoFlush = true };
        text.WriteLine(LogFormat.Format(Entry("held")));
        var hub = new LogHub(temp.Path, () => Noon);

        Assert.Equal(["held"], hub.Read(Today, 10).Select(e => e.Message));
    }

    [Fact]
    public void RecentTakesTheNewestAcrossDays()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon);
        hub.Add(Entry("yesterday", Noon.AddDays(-1)));
        hub.Add(Entry("today"));

        Assert.Equal(["yesterday", "today"], hub.Recent(5).Select(e => e.Message));
        Assert.Equal(["today"], hub.Recent(1).Select(e => e.Message));
    }

    [Fact]
    public void RecentLinesFormatsMultiLineEntries()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon);
        hub.Add(Entry("one"));
        hub.Add(Entry("boom\nat A", level: LogLevel.Error));

        Assert.Equal(["12:00:00.000 INFO  [Host] one", "12:00:00.000 ERROR [Host] boom", "    at A"], hub.RecentLines(2));
        Assert.Equal(["12:00:00.000 ERROR [Host] boom", "    at A"], hub.RecentLines(1));
    }

    [Fact]
    public void ReadOfAHugeFileReturnsTheNewestMaxInOrder()
    {
        using var temp = new TempDir();
        var day = new DateOnly(2026, 10, 18);
        File.WriteAllLines(DayFile(temp.Path, day), Enumerable.Range(0, 50_000).Select(i => LogFormat.Format(Entry($"e{i}", new DateTime(2026, 10, 18, 9, 0, 0)))));
        var hub = new LogHub(temp.Path, () => Noon);

        var tail = hub.Read(day, 100);

        Assert.Equal(Enumerable.Range(49_900, 100).Select(i => $"e{i}"), tail.Select(e => e.Message));
    }

    [Fact]
    public void ReadDropsAContinuationFragmentWhenTheCutFallsInsideAnEntry()
    {
        using var temp = new TempDir();
        var day = new DateOnly(2026, 10, 18);
        var big = Entry("big" + string.Concat(Enumerable.Range(0, 12).Select(n => $"{(char)10}frame {n}")), new DateTime(2026, 10, 18, 9, 0, 0));
        var lines = new List<string> { LogFormat.Format(big) };
        lines.AddRange(Enumerable.Range(0, 7).Select(i => LogFormat.Format(Entry($"after{i}", new DateTime(2026, 10, 18, 9, 1, 0)))));
        File.WriteAllLines(DayFile(temp.Path, day), lines);
        var hub = new LogHub(temp.Path, () => Noon);

        var tail = hub.Read(day, 1);

        Assert.Equal(["after6"], tail.Select(e => e.Message));
        Assert.All(hub.Read(day, 2), e => Assert.StartsWith("after", e.Message));
    }

    [Fact]
    public void NonPositiveCapacityDoesNotThrow()
    {
        using var temp = new TempDir();
        var hub = new LogHub(temp.Path, () => Noon, 0);
        hub.Add(Entry("a"));
        hub.Add(Entry("b"));

        Assert.Equal(["b"], hub.Today(5).Select(e => e.Message));
    }
}
