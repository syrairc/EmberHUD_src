using System;
using System.IO;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class RunTrackerTests
{
    const int Size = 8;

    static TartarusState Board(int day, int[]? tools = null, int open = 0, TartarusChoice? choice = null, int size = Size) =>
        new(0, size, day, 20, [.. Enumerable.Range(0, size).Select(index => new TartarusCell(index, index < open, index < open ? 1 : -1, TartarusBeast.Unknown, false, false))], choice, tools ?? [], 0);

    static TartarusChoice Operation(bool swap) =>
        new(1, TartarusChoiceKind.Operation, [.. (swap ? new[] { new TartarusOption(1, TartarusOptionKind.Replace, -1, 0) } : []), new TartarusOption(2, TartarusOptionKind.End, -1, 0)], true);

    static void InTemp(Action<string> test)
    {
        var dir = Path.Combine(Path.GetTempPath(), "runtracker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { test(dir); }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void NewRunStartsATrackedRunFromDayOne()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            var run = tracker.Update(Board(1));
            Check.Equal(0, run.Assumed.Count);
            Check.Equal(1, run.Day);
        });
    }

    [Test]
    public void ToolsAreRecordedWhenPicked()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            tracker.Update(Board(1));
            tracker.Update(Board(1, [102]));
            var run = tracker.Update(Board(2, [102, 104]));
            Check.Equal("102,104", string.Join(",", run.ToolsHeld));
        });
    }

    [Test]
    public void SwapUsedResetsEachDay()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            Check.That(!(tracker.Update(Board(1, choice: Operation(true))).SwapUsedToday));
            Check.That(tracker.Update(Board(1, choice: Operation(false))).SwapUsedToday);
            Check.That(tracker.Update(Board(1)).SwapUsedToday);
            Check.That(!(tracker.Update(Board(2)).SwapUsedToday));
        });
    }

    [Test]
    public void RestartMidRunReloadsTheSavedFile()
    {
        InTemp(dir =>
        {
            using (var first = new RunTracker(dir, () => 0))
            {
                first.Update(Board(1));
                first.Update(Board(3, [102], open: 3, choice: Operation(false)));
            }
            using var second = new RunTracker(dir, () => 0);
            var run = second.Update(Board(3, [102], open: 3));
            Check.Equal(0, run.Assumed.Count);
            Check.Equal("102", string.Join(",", run.ToolsHeld));
            Check.That(run.SwapUsedToday);
            Check.Equal(3, run.SeenCells.Count);
        });
    }

    [Test]
    public void StaleFileFromAnEarlierRunIsDropped()
    {
        InTemp(dir =>
        {
            using (var first = new RunTracker(dir, () => 0))
            {
                first.Update(Board(1));
                first.Update(Board(15, [102], open: 4));
            }
            using var second = new RunTracker(dir, () => 0);
            var run = second.Update(Board(2, [103]));
            Check.Equal("103", string.Join(",", run.ToolsHeld));
            Check.Equal(0, run.SeenCells.Count);
            Check.That(run.Assumed.Count > 0);
        });
    }

    [Test]
    public void JoiningLateMarksTheRunPartlyTracked()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            var run = tracker.Update(Board(6, [102]));
            Check.That(run.Assumed.Count > 0);
            Check.That(run.Assumed.All(sentence => sentence.EndsWith('.')));
        });
    }

    [Test]
    public void NewRunIsDetectedWhenTheDayDropsOrTheBoardResets()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            tracker.Update(Board(1));
            tracker.Update(Board(5, [102], open: 3));
            Check.Equal(0, tracker.Update(Board(2)).ToolsHeld.Length);
            tracker.Update(Board(1, open: 2));
            var run = tracker.Update(Board(1));
            Check.Equal(0, run.Assumed.Count);
            Check.Equal(0, run.SeenCells.Count);
        });
    }

    [Test]
    public void NullStateKeepsTheLastRunAndDoesNotThrow()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            var before = tracker.Update(Board(4, [102], open: 2));
            var after = tracker.Update(null);
            Check.Equal(4, after.Day);
            Check.Equal("102", string.Join(",", after.ToolsHeld));
            Check.That(ReferenceEquals(before, after));
        });
    }

    [Test]
    public void GarbageStateIsIgnored()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            tracker.Update(Board(3, [102]));
            tracker.Update(Board(3) with { Size = -4 });
            tracker.Update(Board(3) with { Size = Size + 2 });
            tracker.Update(Board(3) with { Cells = null! });
            tracker.Update(Board(3) with { Day = 5000 });
            tracker.Update(Board(3) with { Day = -1 });
            tracker.Update(Board(3, size: 100000));
            tracker.Update(Board(3, choice: new TartarusChoice(1, TartarusChoiceKind.Operation, null!, true)));
            var run = tracker.Update(Board(3, [102]));
            Check.Equal(3, run.Day);
            Check.Equal(Size, run.Size);
        });
    }

    [Test]
    public void SavesAtMostEveryFiveSecondsAndOnDispose()
    {
        InTemp(dir =>
        {
            var now = 0.0;
            var file = Path.Combine(dir, "run.json");
            var tracker = new RunTracker(dir, () => now);
            tracker.Update(Board(1));
            Check.That(File.Exists(file));
            now = 2;
            tracker.Update(Board(2));
            Check.That(File.ReadAllText(file).Contains("\"Day\":1"));
            now = 6;
            tracker.Update(Board(2));
            Check.That(File.ReadAllText(file).Contains("\"Day\":2"));
            now = 7;
            tracker.Update(Board(3));
            Check.That(File.ReadAllText(file).Contains("\"Day\":2"));
            tracker.Dispose();
            Check.That(File.ReadAllText(file).Contains("\"Day\":3"));
            Check.That(!(File.Exists(file + ".tmp")));
        });
    }

    [Test]
    public void BadFileIsIgnored()
    {
        InTemp(dir =>
        {
            File.WriteAllText(Path.Combine(dir, "run.json"), "{ nope");
            using var tracker = new RunTracker(dir, () => 0);
            Check.Equal(0, tracker.Update(Board(1)).Assumed.Count);
        });
    }

    [Test]
    public void TierZeroPreviewMarksTheCellSeen()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            var state = Board(3);
            var cells = state.Cells.ToArray();
            cells[4] = cells[4] with { Tier = 0 };
            Check.That(tracker.Update(state with { Cells = cells }).SeenCells.Contains(4));
        });
    }

    [Test]
    public void ChainReactionStaysArmedUntilTheWardenMoves()
    {
        InTemp(dir =>
        {
            using var tracker = new RunTracker(dir, () => 0);
            var chain = new TartarusChoice(1, TartarusChoiceKind.Event, [new TartarusOption(1, TartarusOptionKind.Confirm, -1, 242)], true);
            Check.That(tracker.Update(Board(2, choice: chain) with { Position = 3 }).ChainArmedAt == 3);
            Check.That(tracker.Update(Board(2, choice: Operation(true)) with { Position = 3 }).ChainArmedAt == 3);
            Check.That(tracker.Update(Board(2, choice: Operation(true)) with { Position = 5 }).ChainArmedAt == -1);
        });
    }
}
