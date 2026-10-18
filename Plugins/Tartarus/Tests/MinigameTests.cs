using System;
using System.IO;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class MinigameTests
{
    static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    static TartarusCell Cell(int index, int tier, int uid, TartarusBeast beast = TartarusBeast.Wolf, bool open = true) => new(index, open, tier, beast, false, false, uid);

    static TartarusState Board(int chromatics, params TartarusCell[] cells) => new(0, 16, 1, 9, cells, null, [], chromatics);

    static TartarusState Board(params TartarusCell[] cells) => Board(0, cells);

    static MinigameLogger Logger(Func<int, int, int>? collected = null) => new(collected ?? ((_, _) => 0));

    static System.Collections.Generic.IReadOnlyList<PullEntry> Step(MinigameLogger logger, TartarusState? state, double seconds) => logger.Update(state, T0.AddSeconds(seconds), seconds);

    static void InTemp(Action<string> test)
    {
        var dir = Path.Combine(Path.GetTempPath(), "minigame-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { test(dir); }
        finally { Directory.Delete(dir, true); }
    }

    static PullEntry Pull(int uid, int run = 1) => new(T0, run, 1, 3, uid, 2, 2, "door", 0);

    [Test]
    public void FirstStateLogsOpenCellsAsStart()
    {
        var logger = Logger();
        var pulls = Step(logger, Board(Cell(3, 1, 11, TartarusBeast.Lion), Cell(4, 0, 12, TartarusBeast.Rat), Cell(5, 2, 13, open: false)), 1);
        Check.Equal(2, pulls.Count);
        Check.Equal("start", pulls[0].Cause);
        Check.Equal(new PullEntry(T0.AddSeconds(1), 1, 1, 3, 11, 1, 1, "start", 0), pulls[0]);
        Check.Equal(3, pulls[1].Species);
    }

    [Test]
    public void LockedCellOpeningIsADoor()
    {
        var logger = Logger();
        Step(logger, Board(Cell(3, 1, 11, open: false)), 1);
        var pulls = Step(logger, Board(Cell(3, 2, 21)), 2);
        Check.Equal("door", pulls.Single().Cause);
        Step(logger, Board(Cell(3, 2, 21), Cell(7, 0, 31)), 3);
        Check.Equal("door", Step(logger, Board(Cell(3, 2, 21), Cell(7, 0, 31), Cell(9, 1, 41)), 4).Single().Cause);
    }

    [Test]
    public void ConsumeIsNotAPull()
    {
        var logger = Logger();
        Step(logger, Board(Cell(3, 1, 11), Cell(4, 1, 12)), 1);
        var pulls = Step(logger, Board(Cell(3, 2, 21), Cell(4, 1, 12, open: false)), 2);
        Check.Equal("consume", pulls.Single().Cause);
        Check.Equal(2, pulls.Single().Tier);
    }

    [Test]
    public void SameUidIsLoggedOnce()
    {
        var logger = Logger();
        Step(logger, Board(Cell(3, 1, 11)), 1);
        Check.Equal(0, Step(logger, Board(Cell(3, 1, 11)), 2).Count);
        Check.Equal(0, Step(logger, Board(Cell(3, 2, 11)), 3).Count);
        Check.Equal(0, Step(logger, Board(Cell(3, 2, 11)), 4).Count);
    }

    [Test]
    public void ChromaticRefillIsLabelledRefill()
    {
        var logger = Logger();
        Step(logger, Board(2, Cell(3, 5, 11)), 1);
        Check.Equal("refill", Step(logger, Board(2, Cell(3, 1, 21)), 2).Single().Cause);
        Check.Equal("refill", Step(logger, Board(3, Cell(3, 4, 31)), 3).Single().Cause);
    }

    [Test]
    public void ReplaceChangesTheUidWithoutSealing()
    {
        var logger = Logger();
        Step(logger, Board(Cell(3, 1, 11), Cell(4, 2, 12)), 1);
        var pulls = Step(logger, Board(Cell(3, 0, 21, TartarusBeast.Rat), Cell(4, 2, 12)), 2);
        Check.Equal("replace", pulls.Single().Cause);
        Check.Equal(3, pulls.Single().Species);
    }

    [Test]
    public void NullForThreeSecondsEndsTheRun()
    {
        var logger = Logger();
        Check.Equal(1, Step(logger, Board(Cell(3, 1, 11)), 1).Single().Run);
        Step(logger, null, 2);
        Step(logger, null, 3.9);
        Check.Equal(0, Step(logger, Board(Cell(3, 1, 11)), 4).Count);
        Step(logger, null, 5);
        Step(logger, null, 8);
        var again = Step(logger, Board(Cell(3, 1, 11)), 9);
        Check.Equal(2, again.Single().Run);
        Check.Equal("start", again.Single().Cause);
    }

    [Test]
    public void CollectedCountComesFromTheGrid()
    {
        var grid = new int[4, 4];
        var logger = Logger((species, tier) => grid[species, tier]);
        var zero = Step(logger, Board(Cell(3, 2, 11, TartarusBeast.Wolf, open: false)), 1);
        Check.Equal(0, zero.Count);
        Check.Equal(0, Step(logger, Board(Cell(3, 2, 11)), 2).Single().Collected);
        grid[2, 2] = 3;
        Step(logger, Board(Cell(3, 2, 11, open: false)), 3);
        Check.Equal(3, Step(logger, Board(Cell(3, 2, 21)), 4).Single().Collected);
        Step(logger, Board(Cell(3, 2, 21, open: false)), 5);
        Check.Equal(-1, Step(logger, Board(Cell(3, 4, 31)), 6).Single().Collected);
    }

    [Test]
    public void GarbageCellsAreSkipped()
    {
        var logger = Logger();
        Check.Equal(0, Step(logger, Board(Cell(1, 1, 0), Cell(2, 1, 5, TartarusBeast.Unknown), Cell(3, -1, 6)), 1).Count);
        Check.Equal(1, Step(logger, Board(Cell(1, 1, 7), Cell(2, 1, 5, TartarusBeast.Unknown), Cell(3, -1, 6)), 2).Count);
    }

    [Test]
    public void StoreRoundTrips()
    {
        InTemp(dir =>
        {
            var store = new MinigameStore();
            store.Add(Pull(11));
            store.Add(Pull(12, 2));
            store.Save(dir);
            Check.That(!store.Dirty);
            var loaded = MinigameStore.Load(dir);
            Check.That(loaded.Loaded);
            Check.Equal(2, loaded.Entries.Count);
            Check.Equal(Pull(12, 2), loaded.Entries[1]);
            Check.That(!loaded.Dirty);
        });
    }

    static TartarusState EventBoard(int choiceId, int eventId, int day, params TartarusCell[] cells) =>
        new(3, 16, day, 9, cells, new(choiceId, TartarusChoiceKind.Event, [new(1, TartarusOptionKind.Confirm, -1, eventId)], true), [], 0);

    [Test]
    public void EventScreenIsLoggedOncePerChoiceAndDay()
    {
        var logger = Logger();
        var marked = new TartarusCell(3, false, -1, TartarusBeast.Unknown, false, true);
        Step(logger, Board(marked), 1);
        var landed = logger.Event(EventBoard(7, 212, 4, Cell(3, 2, 11, TartarusBeast.Lion)), T0.AddSeconds(2))!;
        Check.Equal(new EventEntry(T0.AddSeconds(2), 1, 4, 3, 212, "Extra Portion", true, 1, 2, true), landed);
        Check.That(logger.Event(EventBoard(7, 212, 4, Cell(3, 2, 11)), T0.AddSeconds(3)) is null);
        Check.That(logger.Event(EventBoard(8, 212, 4, Cell(3, 2, 11)), T0.AddSeconds(4)) is not null);
        Check.That(logger.Event(Board(Cell(3, 2, 11)), T0.AddSeconds(5)) is null);
        var closed = Logger().Event(EventBoard(1, 999, 1, Cell(3, 2, 11, open: false)), T0)!;
        Check.That(!closed.Open && closed.Species == -1 && closed.Tier == -1 && !closed.Marked && closed.Name == "Event 999");
    }

    [Test]
    public void EventsRoundTripAndOldFilesStillLoad()
    {
        InTemp(dir =>
        {
            var store = new MinigameStore();
            store.Add(new EventEntry(T0, 1, 2, 3, 251, "Transfer of Convicts", true, 0, 1, false));
            store.Save(dir);
            Check.Equal(1, MinigameStore.Load(dir).Events.Count);
            File.WriteAllText(Path.Combine(dir, "minigame.json"), "{\"Entries\":[]}");
            var old = MinigameStore.Load(dir);
            Check.That(old.Loaded && old.Events.Count == 0);
        });
    }

    [Test]
    public void BrokenStoreLoadsEmptyAndKeepsBad()
    {
        InTemp(dir =>
        {
            var path = Path.Combine(dir, "minigame.json");
            File.WriteAllText(path, "{ not json");
            Check.Equal(0, MinigameStore.Load(dir).Entries.Count);
            Check.That(!File.Exists(path));
            Check.Equal("{ not json", File.ReadAllText(path + ".bad"));
            File.WriteAllText(path, "{\"Entries\":[null]}");
            Check.Equal(0, MinigameStore.Load(dir).Entries.Count);
            File.WriteAllText(path, "null");
            Check.Equal(0, MinigameStore.Load(dir).Entries.Count);
        });
    }

    [Test]
    public void ClearEmptiesAndMarksDirty()
    {
        InTemp(dir =>
        {
            var store = new MinigameStore();
            store.Add(Pull(1));
            store.Save(dir);
            store.Clear();
            Check.Equal(0, store.Entries.Count);
            Check.That(store.Dirty);
        });
    }

    [Test]
    public void EntriesAreCapped()
    {
        var store = new MinigameStore();
        for (var i = 0; i < 5005; i++) store.Add(Pull(i + 1));
        Check.Equal(5000, store.Entries.Count);
        Check.Equal(6, store.Entries[0].Uid);
    }

    [Test]
    public void LoggerTickSavesAndAbsorbs()
    {
        var directory = Path.Combine(Path.GetTempPath(), "minigame-" + Guid.NewGuid().ToString("N"));
        var seconds = 1.0;
        var logger = new InmateLogger { DataDirectory = directory, Seconds = () => seconds, Now = () => T0 };
        try
        {
            var seeded = new MinigameStore();
            seeded.Add(Pull(99, 4));
            seeded.Save(directory);
            var path = Path.Combine(directory, "minigame.json");
            var original = File.ReadAllText(path);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                TestGame.Set(new GameState { Tartarus = Board(Cell(3, 2, 11)) });
                logger.Tick();
                seconds = 10;
                logger.Tick();
                Check.That(!logger.Minigame.Loaded);
            }

            Check.Equal(original, File.ReadAllText(path));
            seconds = 20;
            logger.Tick();
            Check.That(logger.Minigame.Loaded);
            Check.Equal(2, logger.Minigame.Entries.Count);
            seconds = 30;
            logger.Tick();
            var saved = MinigameStore.Load(directory).Entries;
            Check.Equal(2, saved.Count);
            Check.Equal(4, saved[0].Run);
            Check.Equal(1, saved[1].Run);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    [Test]
    public void RunNumberContinuesAfterTheStoredOnes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "minigame-" + Guid.NewGuid().ToString("N"));
        var logger = new InmateLogger { DataDirectory = directory, Seconds = () => 1, Now = () => T0 };
        try
        {
            var seeded = new MinigameStore();
            seeded.Add(Pull(99, 7));
            seeded.Save(directory);
            TestGame.Set(new GameState { Tartarus = Board(Cell(3, 2, 11)) });
            logger.Tick();
            Check.Equal(8, logger.Minigame.Entries[^1].Run);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    [Test]
    public void LogPageDrawsAndClears()
    {
        var store = new MinigameStore();
        TestFrame.Run(() => MinigameLogUI.Draw(store, store.Clear));
        store.Add(Pull(1));
        store.Add(Pull(2) with { Cause = "consume" });
        store.Add(Pull(3) with { Tier = 5, Collected = -1, Species = 9 });
        TestFrame.Run(() => MinigameLogUI.Draw(store, store.Clear));
    }
}
