using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmberHUD;
using EmberHUD.Testing;

namespace Logging;

public sealed class LoggingTests
{
    static readonly DateTime Noon = new(2026, 1, 1, 12, 0, 0);
    static readonly IReadOnlySet<string> NoSources = new HashSet<string>();

    static LogEntry Entry(LogLevel level, string source, string message, int second = 0) => new(Noon.AddSeconds(second), level, source, message);

    static LogEntry[] Sample() =>
    [
        Entry(LogLevel.Debug, "Host", "starting up", 1),
        Entry(LogLevel.Info, "Tartarus", "pool ready", 2),
        Entry(LogLevel.Warn, "Host", "slow frame\nsecond line mentions NeedLe", 3),
        Entry(LogLevel.Error, "Pickit", "failed", 4),
    ];

    [Test]
    public void LevelFilterIsThisLevelAndUp()
    {
        var shown = LogFilter.Apply(Sample(), LogLevel.Warn, NoSources, "");
        Check.Equal(2, shown.Length);
        Check.That(shown.All(entry => entry.Level >= LogLevel.Warn), "only warn and error");
        Check.Equal(4, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "").Length);
    }

    [Test]
    public void EmptySourcesMeansAllAndASetNarrows()
    {
        Check.Equal(4, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "").Length);
        var host = LogFilter.Apply(Sample(), LogLevel.Trace, new HashSet<string> { "Host" }, "");
        Check.Equal(2, host.Length);
        Check.That(host.All(entry => entry.Source == "Host"), "host only");
        Check.Equal(3, LogFilter.Apply(Sample(), LogLevel.Trace, new HashSet<string> { "Host", "Pickit" }, "").Length);
    }

    [Test]
    public void SearchIsCaseInsensitiveAndMatchesContinuationLines()
    {
        Check.Equal(1, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "POOL").Length);
        Check.Equal(1, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "needle").Length);
        Check.Equal(2, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "host").Length);
        Check.Equal(0, LogFilter.Apply(Sample(), LogLevel.Trace, NoSources, "absent").Length);
    }

    [Test]
    public void CopyTextIsExactlyTheFilteredEntriesInFileFormat()
    {
        var all = Sample();
        var shown = LogFilter.Apply(all, LogLevel.Info, NoSources, "");
        var expected = string.Join('\n', new[] { all[1], all[2], all[3] }.Select(LogFormat.Format));
        Check.Equal(expected, LogFilter.Text(shown));
        Check.That(!LogFilter.Text(shown).Contains("starting up"), "hidden entry is absent");
    }

    [Test]
    public void SourceWidthIsTheLongestSourceCappedAtSixteen()
    {
        Check.Equal(8, LogFilter.SourceWidth(Sample()));
        Check.Equal(16, LogFilter.SourceWidth([Entry(LogLevel.Info, "AVeryLongSourceNameIndeed", "x")]));
        Check.Equal(0, LogFilter.SourceWidth([]));
    }

    [Test]
    public void PadTruncatesOverlongSources()
    {
        Check.Equal("Host    ", LogFilter.Pad("Host", 8));
        Check.Equal("AVeryLongSourceN~", LogFilter.Pad("AVeryLongSourceNameIndeed", 17));
        Check.Equal(16, LogFilter.Pad("AVeryLongSourceNameIndeed", 16).Length);
    }

    [Test]
    public void TheListIsNotRefilteredWhileTheStoreVersionIsUnchanged()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            TestFrame.Vertices(plugin.Render);
            Check.Equal(1, plugin.FilterRuns);
            TestLog.Use(Sample());
            TestFrame.Vertices(plugin.Render);
            Check.Equal(2, plugin.FilterRuns);
            plugin.Search = "host";
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.FilterRuns);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void PausedKeepsTheShownListWhileNewEntriesArrive()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.Shown.Length);
            plugin.Paused = true;
            TestLog.Use(Sample().Append(Entry(LogLevel.Error, "Host", "new", 5)).ToArray());
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.Shown.Length);
            plugin.Paused = false;
            TestFrame.Vertices(plugin.Render);
            Check.Equal(4, plugin.Shown.Length);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void ClearHidesEntriesUpToNowButNotLaterOnes()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            plugin.Clear();
            TestFrame.Vertices(plugin.Render);
            Check.Equal(0, plugin.Shown.Length);
            TestLog.Use(Sample().Append(Entry(LogLevel.Error, "Host", "later", 0) with { Time = DateTime.Now.AddMinutes(1) }).ToArray());
            TestFrame.Vertices(plugin.Render);
            Check.Equal(1, plugin.Shown.Length);
            Check.Equal("later", plugin.Shown[0].Message);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void RenderDrawsOnlyWhileThePanelIsShown()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            Check.Equal(1, plugin.FramesDrawn);
            plugin.Settings.ShowPanel = false;
            TestFrame.Vertices(plugin.Render);
            Check.Equal(1, plugin.FramesDrawn);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void RegistersTheToggleHotkeyOnF9()
    {
        var plugin = new Main();
        plugin.Initialize();
        var binding = plugin.Hotkeys.Single();
        Check.Equal("Toggle", binding.Id);
        Check.Equal(Key.F9, binding.Default.Key);
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings page draws");
    }

    [Test]
    public void ThrottleIsDueFirstThenOnlyAfterTheInterval()
    {
        long now = 0;
        var throttle = new Throttle(5000, () => now);
        Check.That(throttle.Due(), "first call is due even at clock zero");
        now = 4999;
        Check.That(!throttle.Due(), "not due inside the interval");
        now = 5000;
        Check.That(throttle.Due(), "due after the interval");
        Check.That(!throttle.Due(), "and then waits again");
        var low = new Throttle(5000, () => long.MinValue);
        Check.That(low.Due(), "a very low clock still loads the first time");
    }

    [Test]
    public void BlockLinesUpTheFourColumnsAndIndentsContinuationLines()
    {
        var block = LogFilter.Block([Entry(LogLevel.Warn, "Host", "head\r\nsecond\nthird", 3), Entry(LogLevel.Info, "AVeryLongSourceNameIndeed", "one")], 16);
        var lines = block.Split('\n');
        Check.Equal(4, lines.Length);
        Check.Equal("12:00:03.000 WARN  " + "Host".PadRight(16) + " head", lines[0]);
        Check.Equal(new string(' ', 36) + "second", lines[1]);
        Check.Equal(new string(' ', 36) + "third", lines[2]);
        Check.Equal("12:00:00.000 INFO  AVeryLongSource~ one", lines[3]);
        Check.Equal(36, lines[0].IndexOf("head"));
        Check.Equal(36, lines[1].IndexOf("second"));
        Check.Equal(36, lines[3].IndexOf("one"));
        Check.That(!block.Contains('\r'), "no carriage returns");
    }

    [Test]
    public void BlockOfNothingIsEmpty()
    {
        Check.Equal("", LogFilter.Block([], 0));
    }

    [Test]
    public void TheTextBlockIsRebuiltOnlyWhenTheFilterOutputIsRebuilt()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            var block = plugin.Block;
            Check.Equal(4, block.Split('\n').Length);
            TestFrame.Vertices(plugin.Render);
            Check.That(ReferenceEquals(block, plugin.Block), "same block while nothing changed");
            plugin.Search = "host";
            TestFrame.Vertices(plugin.Render);
            Check.That(!ReferenceEquals(block, plugin.Block), "block rebuilt after a filter change");
            Check.Equal(2, plugin.Block.Split('\n').Length);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    static string Utf8Text(Main plugin) => Encoding.UTF8.GetString(plugin.Utf8Bytes[..plugin.Utf8Length]);

    [Test]
    public void TheCachedUtf8BytesAreTheBlockWithNonAsciiTextAndANulTerminator()
    {
        try
        {
            TestLog.Use(Entry(LogLevel.Info, "Café", "crème brûlée"), Entry(LogLevel.Warn, "Host", "日本語のログ\nsecond ü"));
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            Check.That(plugin.Block.Any(c => c > 127), "the block has non-ascii text");
            Check.Equal(Encoding.UTF8.GetByteCount(plugin.Block), plugin.Utf8Length);
            Check.Equal(plugin.Block, Utf8Text(plugin));
            Check.Equal((byte)0, plugin.Utf8Bytes[plugin.Utf8Length]);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void ShrinkingTheTextKeepsTheBytesAndTerminatorCorrect()
    {
        try
        {
            TestLog.Use(Enumerable.Range(0, 300).Select(i => Entry(LogLevel.Info, i == 7 ? "Tiny" : "Host", "a fairly long message with some padding " + i, i)).ToArray());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            var longLength = plugin.Utf8Length;
            Check.Equal(plugin.Block, Utf8Text(plugin));
            plugin.ToggleSource("Tiny");
            TestFrame.Vertices(plugin.Render);
            Check.That(plugin.Utf8Length < longLength / 10, "the text shrank a lot");
            Check.Equal(plugin.Block, Utf8Text(plugin));
            Check.Equal((byte)0, plugin.Utf8Bytes[plugin.Utf8Length]);
            plugin.ClearSources();
            plugin.Search = "message with some padding 29";
            TestFrame.Vertices(plugin.Render);
            Check.Equal(plugin.Block, Utf8Text(plugin));
            Check.Equal((byte)0, plugin.Utf8Bytes[plugin.Utf8Length]);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void TheTextAreaFindsItsInnerScrollWindow()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            Check.That(plugin.ScrollWindowFound, "follow-bottom can reach the text area's scroll window");
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void TogglingSourcesNarrowsAndAllClears()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            TestFrame.Vertices(plugin.Render);
            plugin.ToggleSource("Pickit");
            TestFrame.Vertices(plugin.Render);
            Check.Equal(1, plugin.Shown.Length);
            plugin.ToggleSource("Pickit");
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.Shown.Length);
            plugin.ToggleSource("Host");
            plugin.ClearSources();
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.Shown.Length);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void CopyIsExactlyTheFilteredView()
    {
        try
        {
            var all = Sample();
            TestLog.Use(all);
            var plugin = new Main();
            plugin.Settings.DefaultLevel = LogLevel.Trace;
            plugin.ToggleSource("Host");
            TestFrame.Vertices(plugin.Render);
            Check.Equal(string.Join('\n', new[] { all[0], all[2] }.Select(LogFormat.Format)), plugin.CopyText);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void MaxLinesIsClampedTo50To20000()
    {
        var plugin = new Main();
        plugin.Settings.MaxLines = 1;
        Check.Equal(50, plugin.MaxLines);
        plugin.Settings.MaxLines = 999999;
        Check.Equal(20000, plugin.MaxLines);
        plugin.Settings.MaxLines = 300;
        Check.Equal(300, plugin.MaxLines);
    }

    [Test]
    public void AnOlderDayReadsTheStoreAndAMissingDayFallsBackToToday()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main();
            plugin.Day = new DateOnly(2020, 1, 1);
            TestFrame.Vertices(plugin.Render);
            Check.Equal(0, plugin.Shown.Length);
            Check.That(plugin.Day is null, "a day missing from the day list resets to today");
            TestFrame.Vertices(plugin.Render);
            Check.Equal(3, plugin.Shown.Length);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void AnOlderDayIsNotReReadWhenTheStoreVersionBumps()
    {
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main { Day = new DateOnly(2020, 1, 1) };
            TestFrame.Vertices(plugin.Render);
            plugin.Day = new DateOnly(2020, 1, 1);
            TestFrame.Vertices(plugin.Render);
            var loads = plugin.Loads;
            TestLog.Use(Sample());
            TestFrame.Vertices(plugin.Render);
            TestLog.Use(Sample());
            TestFrame.Vertices(plugin.Render);
            Check.Equal(loads, plugin.Loads);
            plugin.Day = new DateOnly(2020, 1, 2);
            TestFrame.Vertices(plugin.Render);
            Check.Equal(loads + 1, plugin.Loads);
            plugin.Settings.MaxLines = 300;
            TestFrame.Vertices(plugin.Render);
            Check.Equal(loads + 2, plugin.Loads);
            var date = DateTime.Now.Date;
            plugin.Now = () => date.AddDays(1);
            TestFrame.Vertices(plugin.Render);
            Check.Equal(loads + 3, plugin.Loads);
        }
        finally
        {
            TestLog.Reset();
        }
    }

    [Test]
    public void TodayIsNeverAFileDayAndASelectedTodayIsTheLiveView()
    {
        var today = new DateOnly(2026, 10, 21);
        var days = LogFilter.FileDays([new(2026, 10, 19), today, new(2026, 10, 20)], today);
        Check.Equal(2, days.Length);
        Check.Equal(new DateOnly(2026, 10, 20), days[0]);
        try
        {
            TestLog.Use(Sample());
            var plugin = new Main { Day = DateOnly.FromDateTime(DateTime.Now) };
            TestFrame.Vertices(plugin.Render);
            Check.That(plugin.Day is null, "today picked as a day becomes the live view");
            Check.Equal(3, plugin.Shown.Length);
        }
        finally
        {
            TestLog.Reset();
        }
    }
}
