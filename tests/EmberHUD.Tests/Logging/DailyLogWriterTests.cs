using EmberHUD.Host.Logging;

namespace EmberHUD.Tests;

public class DailyLogWriterTests
{
    private static readonly DateTime Noon = new(2026, 10, 21, 12, 0, 0);

    private static string[] Lines(string dir, DateTime day) =>
        File.ReadAllLines(Path.Combine(dir, DailyLogWriter.FileName(DateOnly.FromDateTime(day))));

    [Fact]
    public void FileNameIsEmberDashDate()
    {
        Assert.Equal("ember-2026-10-21.log", DailyLogWriter.FileName(new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public void WritesToTheDayFileNamedAfterTheEntryTime()
    {
        using var temp = new TempDir();
        using (var writer = new DailyLogWriter(temp.Path, () => Noon))
        {
            writer.Write(Noon, "one");
            writer.Write(Noon.AddHours(1), "two");
        }

        Assert.Equal(["one", "two"], Lines(temp.Path, Noon));
    }

    [Fact]
    public void AppendsAcrossRestarts()
    {
        using var temp = new TempDir();
        using (var writer = new DailyLogWriter(temp.Path, () => Noon))
            writer.Write(Noon, "first");
        using (var writer = new DailyLogWriter(temp.Path, () => Noon))
            writer.Write(Noon, "second");

        Assert.Equal(["first", "second"], Lines(temp.Path, Noon));
    }

    [Fact]
    public void RollsOverAtMidnight()
    {
        using var temp = new TempDir();
        var before = new DateTime(2026, 10, 21, 23, 59, 59, 900);
        var after = new DateTime(2026, 10, 22, 0, 0, 0, 100);
        using (var writer = new DailyLogWriter(temp.Path, () => before))
        {
            writer.Write(before, "late");
            writer.Write(after, "early");
        }

        Assert.Equal(["late"], Lines(temp.Path, before));
        Assert.Equal(["early"], Lines(temp.Path, after));
    }

    [Fact]
    public void DeletesOnlyOldDayFiles()
    {
        using var temp = new TempDir();
        var old = DailyLogWriter.FileName(DateOnly.FromDateTime(Noon.AddDays(-15)));
        var recent = DailyLogWriter.FileName(DateOnly.FromDateTime(Noon.AddDays(-13)));
        foreach (var name in new[] { old, recent, "ember.log", "notes.txt", "ember-bad.log" })
            temp.File(name, "x");

        using (new DailyLogWriter(temp.Path, () => Noon))
        {
        }

        Assert.False(File.Exists(Path.Combine(temp.Path, old)));
        foreach (var name in new[] { recent, "ember.log", "notes.txt", "ember-bad.log" })
            Assert.True(File.Exists(Path.Combine(temp.Path, name)), name);
    }

    [Fact]
    public void ALockedFileDoesNotThrowOrBlock()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, DailyLogWriter.FileName(DateOnly.FromDateTime(Noon)));
        using var writer = new DailyLogWriter(temp.Path, () => Noon);
        using (new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            writer.Write(Noon, "dropped");
            Assert.True(watch.ElapsedMilliseconds < 500);
        }

        var deadline = DateTime.UtcNow.AddSeconds(10);
        bool Reached()
        {
            if (!File.Exists(path))
                return false;
            using var read = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            return read.ReadToEnd().Contains("later");
        }
        while (!Reached() && DateTime.UtcNow < deadline)
        {
            writer.Write(Noon, "later");
            Thread.Sleep(20);
        }

        Assert.True(Reached());
    }

    [Fact]
    public void AMissingDirectoryIsCreated()
    {
        using var temp = new TempDir();
        var dir = Path.Combine(temp.Path, "sub", "logs");
        using (var writer = new DailyLogWriter(dir, () => Noon))
            writer.Write(Noon, "hi");

        Assert.Equal(["hi"], Lines(dir, Noon));
    }

    [Fact]
    public void DisposeDrainsQueuedLines()
    {
        using var temp = new TempDir();
        using (var writer = new DailyLogWriter(temp.Path, () => Noon))
            for (var i = 0; i < 500; i++)
                writer.Write(Noon, $"line {i}");

        var lines = Lines(temp.Path, Noon);
        Assert.Equal(500, lines.Length);
        Assert.Equal("line 0", lines[0]);
        Assert.Equal("line 499", lines[^1]);
    }

    [Fact]
    public void MultiLineTextKeepsItsLines()
    {
        using var temp = new TempDir();
        using (var writer = new DailyLogWriter(temp.Path, () => Noon))
            writer.Write(Noon, "a\nb\nc");

        Assert.Equal(["a", "b", "c"], Lines(temp.Path, Noon));
    }

    [Fact]
    public void ADeletedDirectoryIsRecreatedOnTheNextLine()
    {
        using var temp = new TempDir();
        var dir = Path.Combine(temp.Path, "logs");
        using var writer = new DailyLogWriter(dir, () => Noon);
        Directory.Delete(dir);
        writer.Write(Noon, "after");
        writer.Dispose();

        Assert.Equal(["after"], Lines(dir, Noon));
    }

    [Fact]
    public void ANonIoFailureDoesNotThrow()
    {
        using var temp = new TempDir();
        var dir = Path.Combine(temp.Path, "bad:name<>|?");
        var writer = new DailyLogWriter(dir, () => Noon);
        writer.Write(Noon, "x");
        writer.Dispose();
    }

    [Fact]
    public void WriteAfterDisposeAndDoubleDisposeDoNotThrow()
    {
        using var temp = new TempDir();
        var writer = new DailyLogWriter(temp.Path, () => Noon);
        writer.Dispose();
        writer.Write(Noon, "late");
        writer.Dispose();
    }
}
