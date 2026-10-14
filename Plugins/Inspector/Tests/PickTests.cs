using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class PickTests
{
    static readonly Vector2 Screen = new(1920, 1080);

    [Test]
    public void ClickingWhilePickingPinsTheCandidateUnderTheCursor()
    {
        var main = new Main { Pick = { Active = true } };
        var position = new Vector2(13, 24);
        TestGame.Set(Samples.Rich with { Player = null, Entities = [], MapIcons = [new MapIcon(9, "Free", position, 1, 0, 0)] });
        var size = Vector2.Zero;
        TestFrame.Run(() => size = ImGui.GetIO().DisplaySize);
        var at = Samples.Camera.ToScreen(new Vector3(position, 0), size)!.Value;
        var io = ImGui.GetIO();
        try
        {
            io.AddMousePosEvent(at.X, at.Y);
            TestFrame.Run(main.Render);
            Check.Equal(0, main.Pins.Count);
            io.AddMouseButtonEvent(0, true);
            TestFrame.Run(main.Render);
            io.AddMouseButtonEvent(0, false);
            TestFrame.Run(main.Render);
        }
        finally
        {
            io.AddMouseButtonEvent(0, false);
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
            TestFrame.Run(() => { });
        }

        Check.Equal("Icon Free", main.Pins.Single().Label);
    }

    static readonly Camera TopDown = new(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);

    static PickMode NewPick() => new(() => 0, Samples.NoPin);

    static Entity Monster(int id, float x, float y, float z) => new(id, 0, new Vector3(x, y, z), EntityKind.Monster, Rarity.Normal);

    static GameState Stage(params Entity[] entities) => Samples.Rich with { Player = null, Entities = entities, MapIcons = [] };

    [Test]
    public void SourcesFollowTheTogglesAndFreeIconsArePickable()
    {
        var pick = NewPick();
        pick.Actors = [new ActorSpot(0x5000, "BP_Chest_C", new Vector3(1, 1, 0))];
        var mouse = Samples.Camera.ToScreen(Vector3.Zero, Screen)!.Value;

        pick.Collect(Samples.Rich, Samples.Camera, Screen, mouse, new Settings { PickRadius = 100000 });
        var targets = pick.Hits.Select(hit => hit.Target).ToArray();

        Check.That(targets.Any(t => t is Player), "the player");
        Check.Equal(2, targets.Count(t => t is Entity));
        Check.Equal(1, targets.Count(t => t is MapIcon));
        Check.That(targets.Any(t => t is MapIcon { EntityId: 0 }), "the free icon is pickable");
        Check.That(targets.Any(t => t is ActorSpot), "the scanned actor");

        pick.Actors = [];
        pick.Collect(Samples.Rich, Samples.Camera, Screen, mouse, new Settings { PickRadius = 100000, PickEntities = false, PickIcons = false });
        Check.Equal(1, pick.Hits.Count);
    }

    [Test]
    public void HitsAreTheNearestWithinTheRadiusClosestFirst()
    {
        var pick = NewPick();
        var state = Stage(Monster(1, 500, 500, 0), Monster(2, 2, 0, 0), Monster(3, 0, 0, 0), Monster(4, -3000, 0, 2000));
        var centre = TopDown.ToScreen(Vector3.Zero, Screen)!.Value;
        var settings = new Settings { PickRadius = 24 };

        pick.Collect(state, TopDown, Screen, centre, settings);

        Check.Equal("3,2", string.Join(",", pick.Hits.Select(hit => ((Entity)hit.Target).Id)));
        pick.Collect(state, TopDown, Screen, centre + new Vector2(300, 0), settings);
        Check.Equal(0, pick.Hits.Count);
    }

    [Test]
    public void AtMostFiveHits()
    {
        var pick = NewPick();
        var state = Stage([.. Enumerable.Range(0, 8).Select(i => Monster(i, 0, 0, 0))]);

        pick.Collect(state, TopDown, Screen, TopDown.ToScreen(Vector3.Zero, Screen)!.Value, new Settings { PickRadius = 24 });

        Check.Equal(5, pick.Hits.Count);
    }

    [Test]
    public void ScanActorsFindsLiveActorsWithARootComponent()
    {
        var (ue, hero, root, monster) = Samples.ActorWorld();
        var template = ue.Object("Default__EMonster", monster);
        TestMemory.Write(template + 0x30, root);
        ue.Object("Rootless", monster);

        var spot = PickMode.ScanActors().Single();

        Check.Equal(hero, spot.Address);
        Check.Equal("EMonster", spot.Class);
        Check.Equal(new Vector3(1, 2, 3), spot.Position);
    }

    [Test]
    public void PickModeTogglesOnItsHotkeyAndEscLeaves()
    {
        var main = new Main();
        main.Initialize();
        TestInput.Hold(Key.F7);
        TestInput.Dispatch(main);
        Check.That(main.Pick.Active, "F7 starts pick mode");
        TestInput.Hold();
        TestInput.Hold(Key.Escape);
        main.Tick();
        Check.That(!main.Pick.Active, "Esc leaves pick mode");
    }

    [Test]
    public void PickModeDrawsWithAndWithoutACamera()
    {
        var main = new Main { Pick = { Active = true } };
        TestGame.Set(Samples.Rich with { Camera = null });
        TestFrame.Run(main.Render);
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
    }

    [Test]
    public void TheTooltipSurvivesFormatSpecifiersInGameNames()
    {
        var main = new Main { Pick = { Active = true } };
        var position = new Vector2(13, 24);
        TestGame.Set(Samples.Rich with { Entities = [], MapIcons = [new MapIcon(9, "Bad%s%n%d%s", position, 1, 0, 0)] });
        var size = Vector2.Zero;
        TestFrame.Run(() => size = ImGui.GetIO().DisplaySize);
        var at = Samples.Camera.ToScreen(new Vector3(position, Samples.Player.Position.Z), size)!.Value;

        ImGui.GetIO().AddMousePosEvent(at.X, at.Y);
        TestFrame.Run(main.Render);
        TestFrame.Run(main.Render);
    }

    [Test]
    public void LeavingPickModeDropsTheScannedActors()
    {
        var main = new Main { Pick = { Active = true } };
        main.Pick.Actors = [new ActorSpot(0x5000, "Actor", Vector3.Zero)];

        TestInput.Hold(Key.Escape);
        main.Tick();

        Check.That(!main.Pick.Active, "Esc leaves pick mode");
        Check.Equal(0, main.Pick.Actors.Length);
    }

    [Test]
    public void PickModeScansActorsOnlyWhenAsked()
    {
        var (_, hero, _, _) = Samples.ActorWorld();
        var main = new Main { Pick = { Active = true, ActorsJob = new(inline: true) } };
        TestGame.Set(Samples.Rich);

        TestFrame.Run(main.Render);
        TestFrame.Run(main.Render);
        Check.Equal(0, main.Pick.Actors.Length);

        main.Settings.PickActors = true;
        TestFrame.Run(main.Render);
        TestFrame.Run(main.Render);
        Check.Equal(hero, main.Pick.Actors.Single().Address);

        main.PinThing(main.Pick.Actors[0]);
        Check.Equal("EMonster", main.Pins.Single().Label);
    }
}
