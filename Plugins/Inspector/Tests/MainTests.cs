using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class MainTests
{
    [Test]
    public void FrozenHoldsTheSnapshotUntilResume()
    {
        TestGame.Set(Samples.Rich);
        var main = new Main();
        main.Freeze();

        TestGame.Set(GameState.Empty with { AreaId = 99 });

        Check.That(main.Frozen, "not frozen");
        Check.Equal(7, main.Shown.AreaId);

        main.Resume();

        Check.That(!main.Frozen, "still frozen");
        Check.Equal(99, main.Shown.AreaId);
    }

    [Test]
    public void TheWindowDrawsClosedOpenFrozenAndWithoutAGame()
    {
        var main = new Main();
        TestFrame.Run(main.Render);
        Check.That(!main.Open, "rendering opened a closed window");

        main.Open = true;
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
        Check.That(main.Open && !main.Frozen, "open frame changed state");

        main.Freeze();
        TestFrame.Run(main.Render);
        Check.That(main.Open && main.Frozen, "frozen frame changed state");
        main.Resume();

        TestGame.Set(GameState.Empty);
        TestFrame.Run(main.Render);
        Check.That(main.Open && !main.Frozen, "empty game frame changed state");
    }

    [Test]
    public void HighlightRingsDrawThroughTheCameraAndTheMapView()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);
        main.Browser.Selected = Samples.Entities[0];

        TestFrame.Run(main.Render);
        Map.View = new MapView(new Vector2(100, 100), 2f);
        TestFrame.Run(main.Render);
        TestGame.Set(Samples.Rich with { Camera = null });
        TestFrame.Run(main.Render);

        Map.View = null;
        TestFrame.Run(main.Render);

        Check.Equal(Samples.Entities[0].Position, main.Browser.Highlights().Single().Position);
    }

    [Test]
    public void TheToggleHotkeyOpensAndClosesTheWindow()
    {
        var main = new Main();
        main.Initialize();
        TestInput.Hold();
        TestInput.Dispatch(main);
        Check.That(!main.Open, "no key, no toggle");
        TestInput.Hold(Key.F6);
        TestInput.Dispatch(main);
        Check.That(main.Open, "press opens");
        TestInput.Hold();
        TestInput.Hold(Key.F6);
        TestInput.Dispatch(main);
        Check.That(!main.Open, "second press closes");
    }

    [Test]
    public void RegistersTwoHotkeysWithTheOldDefaults()
    {
        var main = new Main();
        main.Initialize();
        Check.Equal("Toggle window,Pick mode", string.Join(",", main.Hotkeys.Select(binding => binding.Id)));
        Check.Equal("F6,F7", string.Join(",", main.Hotkeys.Select(binding => binding.Default.Key)));
    }

    [Test]
    public void DisposeClearsTheCaches()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
        main.Browser.Selected = Samples.Entities[0];
        Walker.Clear();
        Walker.Read(typeof(Thrower), Walker.Members(typeof(Thrower)).Single(m => m.Name == "Bad"), new Thrower());
        Check.Equal(1, Walker.ErrorsLogged);

        main.Dispose();

        Check.That(main.Browser.Selected is null, "selection survived Dispose");
        Check.Equal(0, Walker.ErrorsLogged);
    }

    [Test]
    public void TheExplorerSitsBehindTheObjectsTab()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);

        Check.That(main.Explorer is not null, "explorer exists");
        TestFrame.Run(main.Render);
    }

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
    public void EachPinWindowKeepsItsOwnSearchAndHideDefaults()
    {
        var (_, hero, root, _) = Samples.ActorWorld();
        var main = new Main();
        main.PinThing(new RawPin(hero, "Hero"));
        main.PinThing(new RawPin(root, "Root"));

        main.Pins[0].View.Search = "rare";
        main.Pins[0].View.HideDefaults = true;

        Check.Equal("", main.Pins[1].View.Search);
        Check.That(!main.Pins[1].View.HideDefaults, "hide defaults leaked into the other pin");
        Check.That(!ReferenceEquals(main.Pins[0].View, main.Explorer.View), "a pin shares the Objects pane view");
        Check.That(!ReferenceEquals(main.Pins[0].View, main.Raw), "a pin shares the Globals tab view");
        TestFrame.Run(main.Render);
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

    [Test]
    public void EntityPinsKeepTheSameIconsUntilTheStateChanges()
    {
        var main = new Main();
        var entity = Samples.Entities[1];
        main.PinThing(entity);
        var pin = main.Pins.Single();
        var state = Samples.Rich with { MapIcons = [new MapIcon(5, "A", Vector2.Zero, 1, 0, entity.Id), new MapIcon(6, "B", Vector2.Zero, 1, 0, 99)] };

        var first = main.IconsOf(pin, entity, state);

        Check.Equal(1, first.Length);
        Check.That(ReferenceEquals(first, main.IconsOf(pin, entity, state)), "same state, same icons");
        Check.That(!ReferenceEquals(first, main.IconsOf(pin, entity, state with { MapIcons = [.. state.MapIcons] })), "a new icon array rebuilds");
    }

    [Test]
    public void TheSettingsPageDraws() => Samples.Frame(new Settings().Draw);
}
