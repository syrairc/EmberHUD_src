using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace InmateLog;

public sealed class MainTests
{
    const int RareWolf = 5027023;
    static readonly string[][] Titles =
    [
        ["Assault", "Arson", "Violent Offender", "War Criminal"],
        ["Poaching", "Abuse", "Calamity Bringer", "Heretic Leader"],
        ["Bone Eater", "Impalement", "Grave Robber", "Cannibal Criminal"],
        ["Fencing", "Fraud", "Robber", "Underworld Boss"],
    ];
    static readonly string[] Species = ["Mammoth", "Berserk Lion", "Alpha Wolf", "Colossal Rat"];

    static string TempDir() => Path.Combine(Path.GetTempPath(), "inmatelog-" + Guid.NewGuid().ToString("N"));

    static void UseNames()
    {
        var names = new Dictionary<string, string>();
        for (var s = 0; s < 4; s++)
            for (var step = 1; step <= 4; step++)
                names[$"npc|name|{5027000 + s * 10 + step}"] = $"{Titles[s][step - 1]}: {Species[s]}";
        TestGame.UseLoc(key => names.GetValueOrDefault(key), null);
    }

    static Entity Wolf(int id, double life) => new(id, RareWolf, Vector3.Zero, EntityKind.Monster, Rarity.Rare) { Life = life, MaxLife = 90 };

    static void Set(int area, params Entity[] entities) =>
        TestGame.Set(new GameState { AreaId = area, Player = new Player(Vector3.Zero, Vector2.UnitX), Entities = entities, Area = new MapInfo("Prison", 1, null, null, false, [], []) });

    sealed class Rig : IDisposable
    {
        public string Directory = TempDir();
        public double Seconds;
        public Main Plugin;

        public Rig()
        {
            UseNames();
            Plugin = new Main { DataDirectory = Directory, Seconds = () => Seconds, Now = () => new DateTime(2026, 10, 17, 12, 0, 0) };
        }

        public string File => Path.Combine(Directory, "inmates.json");

        public void Tick(double seconds)
        {
            Seconds = seconds;
            Plugin.Tick();
        }

        public void Dispose()
        {
            TestGame.UseLoc(null, null);
            try { System.IO.Directory.Delete(Directory, true); } catch (IOException) { }
        }
    }

    static void Kill(Rig rig, int id, double seconds)
    {
        Set(7, Wolf(id, 0));
        rig.Tick(seconds);
        Set(7);
        rig.Tick(seconds + 0.6);
    }

    [Test]
    public void TickAsksForInventoryAndTartarus()
    {
        using var rig = new Rig();
        TestGame.Set(GameState.Empty);
        Check.That(TestGame.TartarusAskedDuring(() => rig.Tick(0)), "tartarus");
        Check.That(TestGame.InventoryAskedDuring(() => rig.Tick(1)), "inventory");
    }

    [Test]
    public void DeadInmateIsCountedAndSaved()
    {
        using var rig = new Rig();
        Kill(rig, 11, 10);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
        Check.Equal("Prison", rig.Plugin.Store.Kills[0].Area);
        rig.Tick(20);
        Check.That(File.Exists(rig.File), "saved");
    }

    [Test]
    public void SavesNoMoreThanEveryFiveSeconds()
    {
        using var rig = new Rig();
        Kill(rig, 11, 10);
        Check.That(File.Exists(rig.File), "first save");
        File.Delete(rig.File);
        Kill(rig, 12, 11);
        rig.Tick(14);
        Check.That(!File.Exists(rig.File), "too soon");
        rig.Tick(15.7);
        Check.That(File.Exists(rig.File), "after interval");
    }

    [Test]
    public void DisposeSavesDirtyData()
    {
        using var rig = new Rig();
        Kill(rig, 11, 10);
        File.Delete(rig.File);
        Kill(rig, 12, 10.7);
        Check.That(!File.Exists(rig.File), "not yet");
        rig.Plugin.Dispose();
        Check.That(File.Exists(rig.File), "saved on dispose");
    }

    [Test]
    public void LoadsTheFileOnStart()
    {
        using var rig = new Rig();
        var seeded = new InmateStore();
        seeded.Add(new(RareWolf, new(InmateSpecies.Lion, 2), SightingEnd.Killed, 0, 90, 5, 3), "Old", null, DateTime.Now);
        seeded.Save(rig.Directory);
        TestGame.Set(GameState.Empty);
        rig.Tick(1);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Lion, 1]);
    }

    [Test]
    public void UnsureRemovalIsLoggedNotCounted()
    {
        using var rig = new Rig();
        Set(7, Wolf(13, 60));
        rig.Tick(1);
        Set(7);
        rig.Tick(2);
        Check.Equal(1, rig.Plugin.Store.Unsure);
        Check.Equal(0, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
        Check.Equal("unsure", rig.Plugin.Store.Kills[0].Outcome);
    }

    [Test]
    public void RunIsRecordedWhenTartarusEnds()
    {
        using var rig = new Rig();
        var board = new TartarusState(0, 16, 1, 9, [new(4, true, 2, TartarusBeast.Wolf, false, false)], null, [], 0);
        TestGame.Set(new GameState { Tartarus = board });
        rig.Tick(1);
        TestGame.Set(GameState.Empty);
        rig.Tick(2);
        Check.Equal(0, rig.Plugin.Store.Runs.Count);
        rig.Tick(5);
        Check.Equal(1, rig.Plugin.Store.Runs.Count);
        Check.Equal(4, rig.Plugin.Store.Runs[0].Cells[0].Index);
    }

    [Test]
    public void SettingsPageDrawsInEveryState()
    {
        using var rig = new Rig();
        TestGame.Set(GameState.Empty);
        TestFrame.Run(rig.Plugin.DrawSettings);
        Kill(rig, 21, 1);
        Set(7, Wolf(22, 50));
        rig.Tick(3);
        Set(7);
        rig.Tick(4);
        rig.Plugin.Store.Kills.Add(new(DateTime.Now, "Odd", 5027099, null, 0, "killed", 0, 10, 1, 1, 12));
        rig.Plugin.Store.AddRun(new(DateTime.Now, null, null, 7, [new(1, 2, 3)]));
        TestFrame.Run(rig.Plugin.DrawSettings);
    }

    [Test]
    public void ClassificationIsCachedUntilTheAreaChanges()
    {
        using var rig = new Rig();
        var calls = 0;
        TestGame.UseLoc(key => { calls++; return "Plain Rock"; }, null);
        Entity Rock() => new(31, 4000001, Vector3.Zero, EntityKind.Monster, Rarity.Normal) { Life = 10, MaxLife = 10 };
        TestGame.Set(new GameState { AreaId = 7, Entities = [Rock()] });
        rig.Tick(1);
        rig.Tick(2);
        rig.Tick(3);
        Check.Equal(1, calls);
        TestGame.Set(new GameState { AreaId = 8, Entities = [Rock()] });
        rig.Tick(4);
        Check.Equal(2, calls);
    }

    [Test]
    public void UnclassifiedStepIsRetriedAfterAnAreaChange()
    {
        using var rig = new Rig();
        TestGame.UseLoc(key => key == $"npc|name|{RareWolf}" ? "Robber: Alpha Wolf" : null, null);
        TestGame.Set(new GameState { AreaId = 7, Entities = [Wolf(41, 0)] });
        rig.Tick(1);
        UseNames();
        TestGame.Set(new GameState { AreaId = 8, Entities = [Wolf(42, 0)] });
        rig.Tick(2);
        TestGame.Set(new GameState { AreaId = 8 });
        rig.Tick(3);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
    }

    static void Gone(Rig rig, int id, double life, SceneEntity[] scene)
    {
        TestGame.UseScene(() => scene);
        Set(7, Wolf(id, life));
        rig.Tick(1);
        Set(7);
        rig.Tick(2);
    }

    [Test]
    public void SceneDeadCountsAsKilledWhateverTheLastLife()
    {
        using var rig = new Rig();
        try
        {
            Gone(rig, 61, 34, [new(61, RareWolf, null, false, EntityKind.Monster, "")]);
            Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
            Check.Equal("scene-dead", rig.Plugin.Store.Kills[0].Source);
        }
        finally { TestGame.UseScene(null); }
    }

    [Test]
    public void EmptySceneIsUnknownNotDead()
    {
        using var rig = new Rig();
        try
        {
            Gone(rig, 62, 34, []);
            Check.Equal(1, rig.Plugin.Store.Unsure);
            Check.Equal("life", rig.Plugin.Store.Kills[0].Source);
        }
        finally { TestGame.UseScene(null); }
    }

    [Test]
    public void SceneEntryWithAnotherCfgIdIsUnknown()
    {
        using var rig = new Rig();
        try
        {
            Gone(rig, 63, 0, [new(63, 5027099, null, true, EntityKind.Monster, "")]);
            Check.Equal("life", rig.Plugin.Store.Kills[0].Source);
            Check.Equal("killed", rig.Plugin.Store.Kills[0].Outcome);
        }
        finally { TestGame.UseScene(null); }
    }

    [Test]
    public void SceneIsListedOncePerTickAndOnlyWhenNeeded()
    {
        using var rig = new Rig();
        var lists = 0;
        try
        {
            TestGame.UseScene(() => { lists++; return []; });
            Set(7, Wolf(64, 20), Wolf(65, 20));
            rig.Tick(1);
            Check.Equal(0, lists);
            Set(7);
            rig.Tick(2);
            Check.Equal(1, lists);
            Check.Equal(2, rig.Plugin.Store.Unsure);
        }
        finally { TestGame.UseScene(null); }
    }

    [Test]
    public void SettingsPageSurvivesCorruptKillEntries()
    {
        using var rig = new Rig();
        TestGame.Set(GameState.Empty);
        rig.Plugin.Store.Kills.Add(new(DateTime.Now, null!, 5027023, (InmateSpecies)99, 2, "killed", 0, 10, 1, 1, null));
        TestFrame.Run(rig.Plugin.DrawSettings);
    }

    [Test]
    public void FailedLookupIsRetriedAfterTheWindow()
    {
        using var rig = new Rig();
        TestGame.UseLoc(_ => null, null);
        TestGame.Set(new GameState { AreaId = 7, Entities = [Wolf(51, 0)] });
        rig.Tick(1);
        UseNames();
        rig.Tick(5);
        TestGame.Set(new GameState { AreaId = 7 });
        rig.Tick(6);
        Check.Equal(0, rig.Plugin.Store.Kills.Count);
        TestGame.Set(new GameState { AreaId = 7, Entities = [Wolf(51, 0)] });
        rig.Tick(12);
        TestGame.Set(new GameState { AreaId = 7 });
        rig.Tick(13);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
    }

    [Test]
    public void KillsDuringAnUnreadableFileAreKeptAfterTheRetry()
    {
        using var rig = new Rig();
        var seeded = new InmateStore();
        seeded.Add(new(RareWolf, new(InmateSpecies.Lion, 2), SightingEnd.Killed, 0, 90, 5, 3), "Old", null, DateTime.Now);
        seeded.Save(rig.Directory);
        var original = File.ReadAllText(rig.File);
        using (new FileStream(rig.File, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Kill(rig, 11, 10);
            rig.Tick(20);
            Check.That(!rig.Plugin.Store.Loaded);
        }

        Check.Equal(original, File.ReadAllText(rig.File));
        rig.Tick(30);
        Check.That(rig.Plugin.Store.Loaded);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Lion, 1]);
        Check.Equal(1, rig.Plugin.Store.Grid[(int)InmateSpecies.Wolf, 2]);
        rig.Tick(40);
        Check.Equal(2, InmateStore.Load(rig.Directory).Kills.Count);
    }
}
