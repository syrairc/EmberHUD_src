using System.Linq;
using Aember;
using Aember.Testing;

namespace Inspector;

public sealed class ObjectIndexTests
{
    static (ObjectIndex Index, nint Actor, nint Pawn, nint Monster) World()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor");
        var pawn = ue.Class("Pawn", actor);
        var monster = ue.Class("EMonster", pawn);
        ue.Object("Rock", actor);
        ue.Object("Bob", pawn);
        ue.Object("Imp", monster);
        ue.Object("Ghoul", monster);
        ue.Object("Default__EMonster", monster);
        return (ObjectIndex.Scan(), actor, pawn, monster);
    }

    [Test]
    public void TheClassTreeRollsCountsUpTheSuperChain()
    {
        var (index, actor, pawn, monster) = World();

        Check.Equal(4, index.Node(actor)!.Total);
        Check.Equal(1, index.Node(actor)!.Own);
        Check.Equal(3, index.Node(pawn)!.Total);
        Check.Equal(2, index.Node(monster)!.Total);
        Check.Equal(monster, index.Children(pawn).Single());
        Check.That(index.Roots.Contains(actor), "Actor is a root");
    }

    [Test]
    public void InstancesAreInheritanceAwareAndSkipDefaults()
    {
        var (index, actor, pawn, monster) = World();

        Check.Equal("Bob,Ghoul,Imp", string.Join(",", index.Instances(pawn).Select(r => r.Name).OrderBy(n => n)));
        Check.Equal(4, index.Instances(actor).Count());
        Check.Equal(2, index.Instances(monster).Count());
        Check.Equal(0, index.Instances(0x1234).Count());
    }

    [Test]
    public void SearchMatchesNameClassAndAddress()
    {
        var (index, _, _, _) = World();
        var imp = index.Rows.Single(r => r.Name == "Imp");

        Check.Equal(1, index.Search("imp").Length);
        Check.Equal(4, index.Search("EMonster").Length);
        Check.Equal(imp.Address, index.Search($"0x{imp.Address:X}").Single().Address);
        Check.Equal(0, index.Search("0xZZ").Length);
        Check.Equal(0, index.Search("nothing here").Length);
        Check.Equal(0, index.Search("").Length);
    }

    [Test]
    public void AnEmptyOrDetachedIndexIsEmpty()
    {
        TestMemory.Clear();

        Check.Equal(0, ObjectIndex.Scan().Rows.Length);
        Check.Equal(0, ObjectIndex.Empty.Roots.Count);
    }

    [Test]
    public void HistoryWalksBackAndForwardAndDropsTheForwardTailOnPush()
    {
        var history = new History();
        history.Push(1);
        history.Push(2);
        history.Push(3);
        history.Push(3);

        Check.Equal((nint)3, history.Current);
        Check.Equal((nint)2, history.Back());
        Check.That(history.CanForward, "can go forward");
        history.Push(9);
        Check.That(!history.CanForward, "forward tail dropped");
        Check.Equal((nint)2, history.Back());
        Check.Equal((nint)1, history.Back());
        Check.That(!history.CanBack, "at the start");
        Check.Equal((nint)1, history.Back());
    }

    [Test]
    public void HistoryIsCapped()
    {
        var history = new History();
        for (var i = 1; i <= 500; i++) history.Push(i);

        var steps = 0;
        while (history.CanBack)
        {
            history.Back();
            steps++;
        }
        Check.That(steps < 250, $"history kept {steps} entries");
    }
}
