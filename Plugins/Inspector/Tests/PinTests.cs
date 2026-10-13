using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class PinTests
{
    [Test]
    public void PinningAnEntityTwiceKeepsOneWindowAndTheWindowsDraw()
    {
        var ue = new TestUe();
        var hero = ue.Object("Hero", ue.Class("Actor"));
        var main = new Main();
        TestGame.Set(Samples.Rich);

        main.PinThing(Samples.Entities[0]);
        main.PinThing(Samples.Entities[0]);
        main.PinThing(Samples.Player);
        main.PinThing(new RawPin(hero, "Actor Hero"));

        Check.Equal(3, main.Pins.Count);
        Check.Equal("Entity 1", main.Pins[0].Label);
        Check.Equal((nint)0x1000, main.Pins[0].Address);
        Check.Equal(hero, main.Pins[2].Address);
        TestFrame.Run(main.Render);
    }

    [Test]
    public void PinsGoAwayWhenTheGameDoes()
    {
        var ue = new TestUe();
        var hero = ue.Object("Hero", ue.Class("Actor"));
        var logic = TestMemory.Alloc(0x20);
        var main = new Main();
        main.PinThing(new RawPin(hero, "Actor Hero"));
        main.PinThing(new RawPin(logic, "logic"));

        Check.That(main.Pins.All(p => !Main.Gone(p)), "fresh pins are not gone");

        TestMemory.Clear();

        Check.That(main.Pins.All(Main.Gone), "detached pins are gone");
        TestFrame.Run(main.Render);
    }

    [Test]
    public void APinGoesGoneWhenItsClassPointerChanges()
    {
        var ue = new TestUe();
        var hero = ue.Object("Hero", ue.Class("Actor"));
        var other = ue.Class("Other");
        var main = new Main();
        main.PinThing(new RawPin(hero, "Actor Hero"));
        Check.That(!Main.Gone(main.Pins[0]), "fresh pin is not gone");

        TestMemory.Write(hero + 0x18, other);

        Check.That(Main.Gone(main.Pins[0]), "a pin whose class pointer changed is gone");
    }

    [Test]
    public void FreezeAlsoFreezesRawReads()
    {
        var main = new Main();
        main.Freeze();
        Check.That(main.Snapshots.Frozen, "raw reads still refresh");
        main.Resume();
        Check.That(!main.Snapshots.Frozen, "raw reads still frozen");
    }

    [Test]
    public void PinsWithoutAnAddressDedupeByIdentityNotByValue()
    {
        var main = new Main();

        main.PinThing(Samples.Player);
        main.PinThing(Samples.Player with { Position = new System.Numerics.Vector3(99, 99, 0) });
        main.PinThing(Samples.Entities[1]);
        main.PinThing(Samples.Entities[1] with { Position = new System.Numerics.Vector3(7, 7, 7) });
        main.PinThing(Samples.Icons[0]);
        main.PinThing(Samples.Icons[0] with { Rotation = 3f });

        Check.Equal(3, main.Pins.Count);
    }
}
