using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class PalettesAndPathsTests
{
    static readonly Terrain Open = new(100, 100, 10f, Vector2.Zero, new BitArray(10000, true));

    static QuestTarget Target(int id, float x, float y, string name = "") => new(id, new Vector2(x, y)) { Name = name };

    static GameState State(Terrain? terrain, Player? player, int area = 1, params QuestTarget[] targets) =>
        new() { AreaId = area, Terrain = terrain, Player = player, QuestTargets = targets };

    static Player At(float x, float y) => new(new Vector3(x, y, 0), Vector2.UnitX);

    static GameState Open1(Player player, params QuestTarget[] targets) => State(Open, player, 1, targets);

    static Paths Sync(Random? random = null, List<(Vector2 From, Vector2 To)>? calls = null) => new(random ?? new Random(1))
    {
        Runner = job => job(),
        Finder = (_, from, to) =>
        {
            calls?.Add((from, to));
            return [from, to];
        },
    };

    static float Distance(Color a, Color b) => MathF.Sqrt(MathF.Pow(a.R - b.R, 2) + MathF.Pow(a.G - b.G, 2) + MathF.Pow(a.B - b.B, 2));

    [Test]
    public void ThirtyDistinctPresets()
    {
        Check.Equal(30, Palette.Presets.Length);
        Check.Equal(30, Palette.Presets.Select(c => c.ToArgb()).Distinct().Count());
        var closest = float.MaxValue;
        for (var i = 0; i < 30; i++)
            for (var j = i + 1; j < 30; j++)
                closest = MathF.Min(closest, Distance(Palette.Presets[i], Palette.Presets[j]));
        Check.That(closest >= 50f, $"closest pair {closest}");
    }

    [Test]
    public void PickAvoidsUsedColours()
    {
        var used = Palette.Presets.Skip(1).ToHashSet();
        for (var seed = 0; seed < 20; seed++)
            Check.Equal(Palette.Presets[0], Palette.Pick(used, new Random(seed)));
    }

    [Test]
    public void PickReusesWhenAllUsed()
    {
        var picked = Palette.Pick(Palette.Presets.ToHashSet(), new Random(1));
        Check.That(Palette.Presets.Contains(picked), "still a preset");
    }

    [Test]
    public void ColourIsStableWhileTheTargetExists()
    {
        var paths = Sync();
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        var first = paths.Current[0].Colour;
        for (var step = 1; step < 20; step++)
        {
            paths.Update(Open1(At(50 + step * 200, 50), Target(1, 500, 500)), step * 100);
            Check.Equal(first, paths.Current[0].Colour);
        }
    }

    [Test]
    public void TwoTargetsNeverShareAColour()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var paths = Sync(new Random(seed));
            paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(2, 800, 800), Target(3, 200, 900)), 0);
            Check.Equal(3, paths.Current.Select(p => p.Colour).Distinct().Count());
        }
    }

    [Test]
    public void AreaChangeResetsColours()
    {
        var paths = Sync();
        paths.Update(State(Open, At(50, 50), 1, Target(1, 500, 500)), 0);
        Check.Equal(1, paths.Current.Count);
        paths.Update(State(Open, At(50, 50), 2), 100);
        Check.Equal(0, paths.Current.Count);
        paths.Update(State(Open, At(50, 50), 3, Target(1, 500, 500)), 200);
        Check.Equal(1, paths.Current.Count);
        Check.That(paths.Current[0].Points is not null, "pathed from scratch");
    }

    [Test]
    public void RemovedTargetDropsOutAndFreesItsColour()
    {
        var paths = Sync();
        paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(2, 800, 800)), 0);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 100);
        Check.Equal(1, paths.Current.Count);
        Check.Equal(1, paths.Current[0].Id);

        var crowd = Enumerable.Range(0, 29).Select(i => Target(100 + i, 3000 + i * 1000, 3000)).Prepend(Target(1, 500, 500)).ToArray();
        paths.Update(Open1(At(50, 50), crowd), 200);
        Check.Equal(30, paths.Current.Select(p => p.Colour).Distinct().Count());
    }

    [Test]
    public void UnnamedTargetIsObjectN()
    {
        var paths = Sync();
        paths.Update(Open1(At(50, 50), Target(9, 800, 800), Target(4, 500, 500), Target(7, 200, 900, "Boss")), 0);
        var byId = paths.Current.ToDictionary(p => p.Id);
        Check.Equal("Objective 1", byId[4].Name);
        Check.Equal("Boss", byId[7].Name);
        Check.Equal("Objective 3", byId[9].Name);
    }

    [Test]
    public void NoRecomputeWhileStillClose()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        paths.Update(Open1(At(60, 55), Target(1, 500, 500)), 100);
        paths.Update(Open1(At(50 + Paths.RecomputeDistance / 2, 50), Target(1, 502, 500)), 200);
        Check.Equal(1, calls.Count);
    }

    [Test]
    public void RecomputesAfterMovingFarEnough()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        paths.Update(Open1(At(50 + Paths.RecomputeDistance + 1, 50), Target(1, 500, 500)), 100);
        Check.Equal(2, calls.Count);
        Check.Equal(new Vector2(50 + Paths.RecomputeDistance + 1, 50), calls[1].Item1);
    }

    [Test]
    public void RecomputesWhenTheTargetMovesMoreThanACell()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        paths.Update(Open1(At(50, 50), Target(1, 505, 500)), 100);
        Check.Equal(1, calls.Count);
        paths.Update(Open1(At(50, 50), Target(1, 520, 500)), 200);
        Check.Equal(2, calls.Count);
    }

    [Test]
    public void RecomputesWhenTerrainInstanceChanges()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        var reloaded = new Terrain(100, 100, 10f, Vector2.Zero, new BitArray(10000, true));
        paths.Update(State(reloaded, At(50, 50), 1, Target(1, 500, 500)), 100);
        Check.Equal(2, calls.Count);
    }

    [Test]
    public void RecomputesWhenTheTargetSetChanges()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(2, 900, 900)), 100);
        Check.That(calls.Any(c => c.Item2 == new Vector2(900, 900)), "new target pathed");
    }

    [Test]
    public void KeepsOldPointsUntilNewResultLands()
    {
        Action? pending = null;
        var paths = new Paths(new Random(1)) { Runner = job => pending = job, Finder = (_, from, to) => [from, to] };

        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        Check.That(paths.Current.Count == 0 || paths.Current[0].Points is null, "nothing landed yet");
        pending!();
        pending = null;
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 100);
        var old = paths.Current[0].Points;
        Check.Equal(new Vector2(50, 50), old![0]);

        paths.Update(Open1(At(400, 50), Target(1, 500, 500)), 200);
        Check.That(pending is not null, "job handed to the worker");
        Check.That(ReferenceEquals(old, paths.Current[0].Points), "old points kept");
        pending!();
        paths.Update(Open1(At(400, 50), Target(1, 500, 500)), 300);
        Check.Equal(new Vector2(400, 50), paths.Current[0].Points![0]);
    }

    [Test]
    public void OnlyOneJobRunsAtATime()
    {
        var started = 0;
        Action? pending = null;
        var paths = new Paths(new Random(1)) { Runner = job => { started++; pending = job; }, Finder = (_, from, to) => [from, to] };

        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        paths.Update(Open1(At(900, 50), Target(1, 500, 500)), 100);
        Check.Equal(1, started);
        pending!();
        paths.Update(Open1(At(900, 50), Target(1, 500, 500)), 200);
        Check.Equal(2, started);
    }

    [Test]
    public void NullPlayerForOneTickKeepsColours()
    {
        var targets = Enumerable.Range(1, 5).Select(i => Target(i, i * 1000, 500)).ToArray();
        foreach (var gap in new[] { State(Open, null, 1, targets), State(null, At(50, 50), 1, targets) })
        {
            var paths = Sync();
            paths.Update(Open1(At(50, 50), targets), 0);
            var before = paths.Current.ToDictionary(p => p.Id, p => p.Colour);
            paths.Update(gap, 100);
            Check.Equal(0, paths.Current.Count);
            paths.Update(Open1(At(50, 50), targets), 200);
            Check.Equal(5, paths.Current.Count);
            foreach (var path in paths.Current) Check.Equal(before[path.Id], path.Colour);
        }
    }

    [Test]
    public void AreaChangeStillResetsTracking()
    {
        var calls = new List<(Vector2, Vector2)>();
        var paths = Sync(calls: calls);
        paths.Update(State(Open, At(50, 50), 1, Target(1, 500, 500)), 0);
        paths.Update(State(Open, At(50, 50), 2, Target(1, 500, 500)), 100);
        Check.Equal(2, calls.Count);
    }

    [Test]
    public void DuplicateIdsDoNotThrow()
    {
        var paths = Sync();
        paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(1, 900, 900), Target(2, 700, 100)), 0);
        Check.Equal(2, paths.Current.Count);
        Check.Equal(new Vector2(500, 500), paths.Current[0].Target);
    }

    [Test]
    public void ThrowingRunnerDoesNotJamTheSlot()
    {
        var throwing = true;
        var paths = new Paths(new Random(1)) { Finder = (_, from, to) => [from, to] };
        paths.Runner = job =>
        {
            if (throwing) throw new InvalidOperationException("no threads");
            job();
        };
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        Check.Equal(0, paths.Current.Count);
        throwing = false;
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 100);
        Check.Equal(1, paths.Current.Count);
    }

    [Test]
    public void UnreachableTargetHasNullPoints()
    {
        var paths = new Paths(new Random(1)) { Runner = job => job(), Finder = (_, _, _) => null };
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        Check.Equal(1, paths.Current.Count);
        Check.That(paths.Current[0].Points is null, "unreachable");
    }

    [Test]
    public void ArrivedSinglePointPathKeepsPointsAndHasZeroLength()
    {
        var paths = new Paths(new Random(1)) { Runner = job => job(), Finder = (_, from, _) => [from] };
        paths.Update(Open1(At(50, 50), Target(1, 52, 52)), 0);
        Check.Equal(1, paths.Current[0].Points!.Length);
        Check.Equal(0f, paths.Current[0].Length);
    }

    [Test]
    public void LengthIsTheSumOfSegments()
    {
        var paths = new Paths(new Random(1)) { Runner = job => job(), Finder = (_, from, to) => [from, new Vector2(from.X, to.Y), to] };
        paths.Update(Open1(At(0, 0), Target(1, 30, 40)), 0);
        Check.Equal(70f, paths.Current[0].Length);
    }

    [Test]
    public void NullTerrainOrPlayerYieldsNoPaths()
    {
        var paths = Sync();
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 0);
        Check.Equal(1, paths.Current.Count);
        paths.Update(State(null, At(50, 50), 1, Target(1, 500, 500)), 100);
        Check.Equal(0, paths.Current.Count);
        paths.Update(Open1(At(50, 50), Target(1, 500, 500)), 200);
        paths.Update(State(Open, null, 1, Target(1, 500, 500)), 300);
        Check.Equal(0, paths.Current.Count);
    }

    [Test]
    public void MovedArrowKeepsItsColour()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var paths = Sync(new Random(seed));
            paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(2, 2000, 2000)), 0);
            var colours = paths.Current.ToDictionary(p => p.Id, p => p.Colour);

            paths.Update(Open1(At(50, 50), Target(5, 560, 520), Target(2, 2000, 2000)), 100);
            var after = paths.Current.ToDictionary(p => p.Id, p => p.Colour);
            Check.Equal(colours[1], after[5]);
            Check.Equal(colours[2], after[2]);
        }
    }

    [Test]
    public void GenuinelyNewTargetFarFromAnyVanishedOneGetsAFreshColour()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var paths = Sync(new Random(seed));
            paths.Update(Open1(At(50, 50), Target(1, 500, 500), Target(2, 2000, 2000)), 0);
            var kept = paths.Current.First(p => p.Id == 2).Colour;

            paths.Update(Open1(At(50, 50), Target(2, 2000, 2000), Target(3, 5000, 5000)), 100);
            var fresh = paths.Current.First(p => p.Id == 3).Colour;
            Check.That(fresh != kept, "never the colour of a live target");
            Check.Equal(2, paths.Current.Select(p => p.Colour).Distinct().Count());
        }
    }
}
