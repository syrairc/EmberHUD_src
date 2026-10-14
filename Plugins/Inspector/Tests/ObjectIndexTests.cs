using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

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
        Check.Equal($"Actor  4###class{actor}", index.Node(actor)!.Label);
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
}
