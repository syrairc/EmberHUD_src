using System;
using System.IO;
using System.Linq;
using EmberHUD.Testing;

namespace InmateLog;

public sealed class InmateStoreTests
{
    static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    static Sighting Seen(InmateSpecies species, int step, SightingEnd end = SightingEnd.Killed, int cfgId = 5027023) =>
        new(cfgId, new(species, step), end, end == SightingEnd.Killed ? 0 : 55, 90, 12.5f, 3.5);

    static void InTemp(Action<string> test)
    {
        var dir = Path.Combine(Path.GetTempPath(), "inmatelog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { test(dir); }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void KilledAddsToTheGridCell()
    {
        var store = new InmateStore();
        store.Add(Seen(InmateSpecies.Wolf, 3), "Ward", 7, Now);
        store.Add(Seen(InmateSpecies.Wolf, 3), "Ward", 8, Now);
        store.Add(Seen(InmateSpecies.Wolf, 3), "Ward", 9, Now);
        store.Add(Seen(InmateSpecies.Rat, 1), "Ward", null, Now);
        Check.Equal(3, store.Grid[(int)InmateSpecies.Wolf, 2]);
        Check.Equal(1, store.Grid[(int)InmateSpecies.Rat, 0]);
        Check.Equal(0, store.Grid[(int)InmateSpecies.Lion, 3]);
        Check.Equal(4, store.Kills.Count);
        Check.That(store.Dirty);
    }

    [Test]
    public void UnclassifiedKillCountsSeparately()
    {
        var store = new InmateStore();
        store.Add(Seen(InmateSpecies.Lion, 0), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Lion, 0), "Ward", null, Now);
        Check.Equal(2, store.Unclassified);
        Check.Equal(0, store.Grid[(int)InmateSpecies.Lion, 0]);
        Check.Equal(2, store.Kills.Count);
    }

    [Test]
    public void UnsureNeverTouchesTheGrid()
    {
        var store = new InmateStore();
        store.Add(Seen(InmateSpecies.Mammoth, 2, SightingEnd.Unsure), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Mammoth, 0, SightingEnd.Unsure), "Ward", null, Now);
        Check.Equal(2, store.Unsure);
        Check.Equal(0, store.Unclassified);
        Check.Equal(0, store.Grid[(int)InmateSpecies.Mammoth, 1]);
        Check.Equal("unsure", store.Kills[0].Outcome);
        Check.Equal(2, store.Kills.Count);
    }

    [Test]
    public void KillLogIsCappedOldestFirst()
    {
        var store = new InmateStore();
        for (var i = 0; i < 2005; i++) store.Add(Seen(InmateSpecies.Rat, 2, cfgId: i), "Ward", null, Now);
        Check.Equal(2000, store.Kills.Count);
        Check.Equal(5, store.Kills[0].CfgId);
        Check.Equal(2004, store.Kills[^1].CfgId);
        Check.Equal(2005, store.Grid[(int)InmateSpecies.Rat, 1]);
    }

    [Test]
    public void RunLogIsCapped()
    {
        var store = new InmateStore();
        for (var i = 0; i < 53; i++) store.AddRun(new(Now.AddMinutes(i), null, i, null, []));
        Check.Equal(50, store.Runs.Count);
        Check.Equal(3, store.Runs[0].NameplatesBefore);
        Check.Equal(52, store.Runs[^1].NameplatesBefore);
        Check.That(store.Dirty);
    }

    [Test]
    public void SaveThenLoadRoundTrips()
    {
        InTemp(dir =>
        {
            var store = new InmateStore();
            store.Add(Seen(InmateSpecies.Lion, 4), "Ward Nine", 21, Now);
            store.Add(Seen(InmateSpecies.Rat, 0), "Ward", null, Now);
            store.Add(Seen(InmateSpecies.Wolf, 1, SightingEnd.Unsure), "Ward", null, Now);
            store.AddRun(new(Now, Now.AddMinutes(9), 30, 41, [new(5, 2, 3)]));
            store.Save(dir);
            Check.That(!store.Dirty);

            var loaded = InmateStore.Load(dir);
            Check.Equal(1, loaded.Grid[(int)InmateSpecies.Lion, 3]);
            Check.Equal(1, loaded.Unclassified);
            Check.Equal(1, loaded.Unsure);
            Check.Equal(3, loaded.Kills.Count);
            Check.Equal("Ward Nine", loaded.Kills[0].Area);
            Check.Equal(21, loaded.Kills[0].Nameplates);
            Check.Equal(InmateSpecies.Lion, loaded.Kills[0].Species);
            Check.Equal(Now, loaded.Kills[0].When);
            Check.Equal(1, loaded.Runs.Count);
            Check.Equal(41, loaded.Runs[0].NameplatesAfter);
            Check.Equal(new RunCell(5, 2, 3), loaded.Runs[0].Cells[0]);
            Check.That(!loaded.Dirty);
        });
    }

    [Test]
    public void CorruptFileLoadsEmptyAndKeepsTheBadFile()
    {
        InTemp(dir =>
        {
            File.WriteAllText(Path.Combine(dir, "inmates.json.bad"), "older");
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "{ not json");
            var loaded = InmateStore.Load(dir);
            Check.Equal(0, loaded.Kills.Count);
            Check.Equal(0, loaded.Runs.Count);
            Check.That(!File.Exists(Path.Combine(dir, "inmates.json")));
            Check.Equal("{ not json", File.ReadAllText(Path.Combine(dir, "inmates.json.bad")));
        });
    }

    [Test]
    public void MissingFileLoadsEmpty()
    {
        InTemp(dir =>
        {
            var loaded = InmateStore.Load(dir);
            Check.Equal(0, loaded.Kills.Count);
            Check.Equal(0, loaded.Unsure);
            Check.Equal(0, loaded.Grid[(int)InmateSpecies.Rat, 3]);
            Check.That(!loaded.Dirty);
        });
    }

    [Test]
    public void SaveLeavesNoTempFile()
    {
        InTemp(dir =>
        {
            var inner = Path.Combine(dir, "made");
            var store = new InmateStore();
            store.Add(Seen(InmateSpecies.Mammoth, 1), "Ward", null, Now);
            store.Save(inner);
            Check.That(File.Exists(Path.Combine(inner, "inmates.json")));
            Check.That(!File.Exists(Path.Combine(inner, "inmates.json.tmp")));
        });
    }

    [Test]
    public void ClearEmptiesEverythingAndMarksDirty()
    {
        var store = new InmateStore();
        store.Add(Seen(InmateSpecies.Wolf, 2), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Wolf, 0), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Wolf, 2, SightingEnd.Unsure), "Ward", null, Now);
        store.AddRun(new(Now, null, null, null, []));
        store.Clear();
        Check.Equal(0, store.Grid[(int)InmateSpecies.Wolf, 1]);
        Check.Equal(0, store.Unclassified);
        Check.Equal(0, store.Unsure);
        Check.Equal(0, store.Kills.Count);
        Check.Equal(0, store.Runs.Count);
        Check.That(store.Dirty);
    }

    [Test]
    public void OutOfRangeStepsGoToUnclassified()
    {
        var store = new InmateStore();
        store.Add(Seen(InmateSpecies.Lion, 5), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Lion, -1), "Ward", null, Now);
        store.Add(Seen(InmateSpecies.Lion, -1), "Ward", null, Now);
        Check.Equal(3, store.Unclassified);
        foreach (var count in store.Grid) Check.Equal(0, count);
    }

    [Test]
    public void NullListsInTheFileLoadEmptyAndAddWorks()
    {
        InTemp(dir =>
        {
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "{\"Grid\":[[0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]],\"Kills\":null}");
            var loaded = InmateStore.Load(dir);
            loaded.Add(Seen(InmateSpecies.Rat, 1), "Ward", null, Now);
            Check.Equal(1, loaded.Kills.Count);
            Check.Equal(0, loaded.Runs.Count);
            Check.That(File.Exists(Path.Combine(dir, "inmates.json.bad")));
        });
    }

    [Test]
    public void NullElementsAndNullCellsLoadEmpty()
    {
        InTemp(dir =>
        {
            const string grid = "\"Grid\":[[0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]]";
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "{" + grid + ",\"Kills\":[null],\"Runs\":[]}");
            Check.Equal(0, InmateStore.Load(dir).Kills.Count);
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "{" + grid + ",\"Kills\":[],\"Runs\":[{\"Cells\":null}]}");
            Check.Equal(0, InmateStore.Load(dir).Runs.Count);
        });
    }

    [Test]
    public void OverCapFileIsTrimmedOnLoad()
    {
        InTemp(dir =>
        {
            var big = new InmateStore();
            for (var i = 0; i < 2000; i++) big.Add(Seen(InmateSpecies.Rat, 1, cfgId: i), "Ward", null, Now);
            big.Save(dir);
            var text = File.ReadAllText(Path.Combine(dir, "inmates.json"));
            var kills = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<KillEntry>>(System.Text.Json.JsonDocument.Parse(text).RootElement.GetProperty("Kills").GetRawText())!;
            var runs = new System.Collections.Generic.List<RunEntry>();
            for (var i = 0; i < 60; i++) runs.Add(new(Now, null, i, null, []));
            kills.AddRange(kills.Take(10));
            File.WriteAllText(Path.Combine(dir, "inmates.json"), System.Text.Json.JsonSerializer.Serialize(new { Grid = new int[4][] { new int[4], new int[4], new int[4], new int[4] }, Unclassified = 0, Unsure = 0, Kills = kills, Runs = runs }));
            var loaded = InmateStore.Load(dir);
            Check.Equal(2000, loaded.Kills.Count);
            Check.Equal(50, loaded.Runs.Count);
            Check.Equal(10, loaded.Runs[0].NameplatesBefore);
        });
    }

    [Test]
    public void SaveToALockedFileDoesNotThrowAndStaysDirty()
    {
        InTemp(dir =>
        {
            var store = new InmateStore();
            store.Add(Seen(InmateSpecies.Wolf, 2), "Ward", null, Now);
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "old");
            using (new FileStream(Path.Combine(dir, "inmates.json"), FileMode.Open, FileAccess.Read, FileShare.None))
                store.Save(dir);
            Check.That(store.Dirty);
            Check.That(!File.Exists(Path.Combine(dir, "inmates.json.tmp")));
            Check.Equal("old", File.ReadAllText(Path.Combine(dir, "inmates.json")));
        });
    }

    [Test]
    public void NonFiniteNumbersAreStoredAsZeroAndSaveWorks()
    {
        InTemp(dir =>
        {
            var store = new InmateStore();
            store.Add(new(5027023, new(InmateSpecies.Wolf, 2), SightingEnd.Unsure, double.NaN, double.PositiveInfinity, float.NegativeInfinity, double.NaN), "Ward", null, Now);
            Check.Equal(0.0, store.Kills[0].LastLife);
            Check.Equal(0.0, store.Kills[0].MaxLife);
            Check.Equal(0f, store.Kills[0].Distance);
            Check.Equal(0.0, store.Kills[0].SecondsSeen);
            store.Save(dir);
            Check.That(!store.Dirty);
            Check.Equal(1, InmateStore.Load(dir).Kills.Count);
        });
    }

    [Test]
    public void UnreadableFileIsNotLoadedAndNeverOverwritten()
    {
        InTemp(dir =>
        {
            var seeded = new InmateStore();
            seeded.Add(Seen(InmateSpecies.Lion, 2), "Old", null, Now);
            seeded.Save(dir);
            var path = Path.Combine(dir, "inmates.json");
            var original = File.ReadAllText(path);

            InmateStore pending;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                pending = InmateStore.Load(dir);
                Check.That(!pending.Loaded);
                pending.Add(Seen(InmateSpecies.Wolf, 1), "New", null, Now);
                pending.Save(dir);
                Check.That(pending.Dirty);
            }

            Check.Equal(original, File.ReadAllText(path));
            Check.That(!File.Exists(path + ".bad"));
            var loaded = InmateStore.Load(dir);
            Check.That(loaded.Loaded);
            loaded.Absorb(pending);
            Check.Equal(1, loaded.Grid[(int)InmateSpecies.Lion, 1]);
            Check.Equal(1, loaded.Grid[(int)InmateSpecies.Wolf, 0]);
            Check.Equal(2, loaded.Kills.Count);
            Check.That(loaded.Dirty);
            loaded.Save(dir);
            Check.Equal(2, InmateStore.Load(dir).Kills.Count);
        });
    }

    [Test]
    public void NullJsonFileLoadsEmptyAndAddWorks()
    {
        InTemp(dir =>
        {
            File.WriteAllText(Path.Combine(dir, "inmates.json"), "null");
            var loaded = InmateStore.Load(dir);
            loaded.Add(Seen(InmateSpecies.Rat, 1), "Ward", null, Now);
            Check.Equal(1, loaded.Kills.Count);
            Check.That(File.Exists(Path.Combine(dir, "inmates.json.bad")));
        });
    }
}
