using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class SuiteTests
{
    const int Columns = 7;
    const int Rows = 5;

    static BagCell Cell(int slot, float originX = 0) => new(slot, new Vector2(originX + slot % Columns * 50, slot / Columns * 50), new Vector2(originX + slot % Columns * 50 + 40, slot / Columns * 50 + 40));

    static StashView Stash(params int[] without) => new(0, Vector2.Zero, new Vector2(400, 300), Enumerable.Range(0, Columns * Rows).Where(slot => !without.Contains(slot)).Select(slot => Cell(slot)).ToArray());

    static BagView Bag(params int[] slots) => new(InventoryBag.Equipment, Vector2.Zero, new Vector2(400, 300), slots.Select(slot => Cell(slot, 1000)).ToArray());

    static ScreenRect Rect(BagCell cell) => new(cell.Min, cell.Max);

    [Test]
    public void FullStashBuildsSevenScenariosInOrder()
    {
        var bag = Bag(3, 4);
        var build = Suite.Build(Stash(), bag);
        Check.Equal(0, build.Skipped.Count);
        Check.That(build.Scenarios.Select(scenario => scenario.Name).SequenceEqual(["Adjacent hop", "Row sweep", "Column sweep", "Diagonal", "Return trip", "Hop chain", "Bag to stash"]), string.Join(",", build.Scenarios.Select(scenario => scenario.Name)));
        var stash = Stash().Cells;
        Check.That(build.Scenarios[0].Steps.Skip(1).Single().Target == Rect(stash[1]), "adjacent hop goes to (1,0)");
        Check.That(build.Scenarios[1].Steps.Skip(1).Single().Target == Rect(stash[6]), "row sweep goes to (6,0)");
        Check.That(build.Scenarios[2].Steps.Skip(1).Single().Target == Rect(stash[4 * Columns]), "column sweep goes to (0,4)");
        Check.That(build.Scenarios[3].Steps.Skip(1).Single().Target == Rect(stash[4 * Columns + 6]), "diagonal goes to (6,4)");
        Check.That(build.Scenarios[4].Steps.Skip(1).Select(step => step.Target).SequenceEqual([Rect(stash[2 * Columns + 3]), Rect(stash[0])]), "return trip");
        Check.That(build.Scenarios[6].Steps.Skip(1).Select(step => step.Target).SequenceEqual([Rect(bag.Cells[0]), Rect(stash[0])]), "bag to stash");
    }

    [Test]
    public void EachScenarioStartsWithAnUnscoredMoveToCellZero()
    {
        var stash = Stash();
        foreach (var scenario in Suite.Build(stash, Bag(0)).Scenarios)
        {
            Check.That(scenario.Steps[0].Target == Rect(stash.Cells[0]) && !scenario.Steps[0].Scored, scenario.Name);
            Check.That(scenario.Steps.Skip(1).All(step => step.Scored), scenario.Name + " scored");
        }
    }

    [Test]
    public void CellsAreLookedUpBySlotNotListOrder()
    {
        var stash = Stash();
        var shuffled = stash with { Cells = stash.Cells.Reverse().ToArray() };
        var bag = Bag(5, 2, 9);
        var shuffledBag = bag with { Cells = bag.Cells.Reverse().ToArray() };
        var expected = Suite.Build(stash, bag);
        var actual = Suite.Build(shuffled, shuffledBag);
        for (var index = 0; index < expected.Scenarios.Count; index++)
            Check.That(expected.Scenarios[index].Steps.SequenceEqual(actual.Scenarios[index].Steps), expected.Scenarios[index].Name);
        Check.That(actual.Scenarios[6].Steps[1].Target == Rect(bag.Cells[1]), "lowest bag slot is first");
    }

    [Test]
    public void MissingCellDropsOnlyThatScenarioAndNamesIt()
    {
        var build = Suite.Build(Stash(1), null);
        Check.That(build.Scenarios.All(scenario => scenario.Name != "Adjacent hop"), "adjacent dropped");
        Check.Equal(5, build.Scenarios.Count);
        Check.Equal(2, build.Skipped.Count);
        Check.That(build.Skipped[0].Contains("Adjacent hop") && build.Skipped[0].Contains("(1,0)"), build.Skipped[0]);
    }

    [Test]
    public void NoBagSkipsBagToStash()
    {
        foreach (var bag in new BagView?[] { null, Bag() })
        {
            var build = Suite.Build(Stash(), bag);
            Check.Equal(6, build.Scenarios.Count);
            Check.Equal(1, build.Skipped.Count);
            Check.That(build.Skipped[0].StartsWith("Bag to stash"), build.Skipped[0]);
        }
    }

    [Test]
    public void HopChainIsTheSameForTheSameSeedAndDifferentForAnother()
    {
        static Step[] Chain(int seed) => Suite.Build(Stash(), null, seed).Scenarios.Single(scenario => scenario.Name == "Hop chain").Steps.ToArray();
        var one = Chain(1);
        Check.Equal(9, one.Length);
        Check.Equal(8, one.Count(step => step.Scored));
        Check.That(one.SequenceEqual(Chain(1)), "same seed");
        Check.That(!one.SequenceEqual(Chain(2)), "other seed");
        Check.That(one.Zip(one.Skip(1)).All(pair => pair.First.Target != pair.Second.Target), "every hop moves");
    }
}
