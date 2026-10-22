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
    public void PinningAnEntityTwiceKeepsOneWindow()
    {
        var main = new Main();
        TestGame.Set(Samples.Rich);

        main.PinThing(Samples.Entities[0]);
        main.PinThing(Samples.Entities[0]);
        main.PinThing(Samples.Player);

        Check.Equal(2, main.Pins.Count);
        Check.Equal("Entity 1", main.Pins[0].Label);
        TestFrame.Run(main.Render);
    }

    [Test]
    public void AnEntityPinFollowsTheLiveRecordAndGoesGone()
    {
        var main = new Main();
        main.PinThing(Samples.Entities[0]);
        var moved = Samples.Entities[0] with { Position = new Vector3(50, 60, 0) };

        Check.Equal(moved, Main.Current(main.Pins[0], Samples.Rich with { Entities = [moved, Samples.Entities[1]] }));
        Check.That(Main.Current(main.Pins[0], Samples.Rich with { Entities = [Samples.Entities[1]] }) is null, "missing entity is gone");
    }

    [Test]
    public void APlayerPinGoesGoneWhenThePlayerIsNull()
    {
        var main = new Main();
        main.PinThing(Samples.Player);
        var live = Samples.Player with { Position = new Vector3(1, 1, 1) };

        Check.Equal(live, Main.Current(main.Pins[0], Samples.Rich with { Player = live }));
        Check.That(Main.Current(main.Pins[0], Samples.Rich with { Player = null }) is null, "no player is gone");
    }

    [Test]
    public void ALinkedIconPinIsRefoundByEntityIdAndASnapshotStays()
    {
        var main = new Main();
        main.PinThing(Samples.Icons[1]);
        main.PinThing(Samples.Icons[0]);
        var moved = Samples.Icons[1] with { Position = new Vector2(70, 80) };

        Check.Equal(moved, Main.Current(main.Pins[0], Samples.Rich with { MapIcons = [Samples.Icons[0], moved] }));
        Check.That(Main.Current(main.Pins[0], Samples.Rich with { MapIcons = [Samples.Icons[0]] }) is null, "missing linked icon is gone");
        Check.Equal(Samples.Icons[0], Main.Current(main.Pins[1], Samples.Rich with { MapIcons = [] }));
    }

    [Test]
    public void PinsWithoutAnAddressDedupeByIdentityNotByValue()
    {
        var main = new Main();

        main.PinThing(Samples.Player);
        main.PinThing(Samples.Player with { Position = new Vector3(99, 99, 0) });
        main.PinThing(Samples.Entities[1]);
        main.PinThing(Samples.Entities[1] with { Position = new Vector3(7, 7, 7) });
        main.PinThing(Samples.Icons[0]);
        main.PinThing(Samples.Icons[0] with { Rotation = 3f });
        main.PinThing(Samples.Icons[1]);
        main.PinThing(Samples.Icons[1] with { Position = new Vector2(9, 9) });

        Check.Equal(4, main.Pins.Count);
    }

    [Test]
    public void AnIconWithoutAnEntityIsLabelledSnapshot()
    {
        var main = new Main();
        main.PinThing(Samples.Icons[0]);
        main.PinThing(Samples.Icons[1]);
        Check.Equal("Icon UI_MiniMap_Portal (snapshot)", main.Pins[0].Label);
        Check.Equal("Icon UI_MiniMap_S15xiaoguai", main.Pins[1].Label);
    }

    [Test]
    public void OldSettingsKeysAreIgnored()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

        var settings = System.Text.Json.JsonSerializer.Deserialize<Settings>("""{"PickActors":true,"RefreshRate":30,"PickRadius":40}""", options)!;

        Check.Equal(40f, settings.PickRadius);
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
