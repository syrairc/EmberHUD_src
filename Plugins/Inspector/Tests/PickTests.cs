using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class PickTests
{
    static readonly Vector2 Screen = new(1920, 1080);

    [Test]
    public void CandidatesFollowTheSourceTogglesAndDropIconsOfListedEntities()
    {
        var actors = new[] { new ActorSpot(0x5000, "BP_Chest_C", new Vector3(1, 1, 0)) };

        var all = Pick.Candidates(Samples.Rich, true, true, actors);
        var none = Pick.Candidates(Samples.Rich, false, false, []);

        Check.Equal("Player", all[0].Label);
        Check.Equal(2, all.Count(c => c.Target is Entity));
        Check.Equal(1, all.Count(c => c.Target is MapIcon));
        Check.That(all.Any(c => c.Target is MapIcon { EntityId: 0 }), "the free icon is pickable");
        Check.Equal("BP_Chest_C", all[^1].Label);
        Check.Equal(1, none.Count);
    }

    [Test]
    public void UnderPicksTheNearestWithinTheRadiusFirst()
    {
        var camera = new Camera(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);
        var centre = camera.ToScreen(Vector3.Zero, Screen)!.Value;
        Candidate[] candidates = [new("far", new Vector3(500, 500, 0), 1), new("near", new Vector3(2, 0, 0), 2), new("exact", Vector3.Zero, 3), new("behind", new Vector3(-3000, 0, 2000), 4)];

        var under = Pick.Under(candidates, camera, Screen, centre, 24);

        Check.That(under.Select(u => u.Candidate.Label).SequenceEqual(["exact", "near"]), string.Join(",", under.Select(u => u.Candidate.Label)));
        Check.Equal(0, Pick.Under(candidates, camera, Screen, centre + new Vector2(300, 0), 24).Length);
    }

    [Test]
    public void UnderListsAtMostFive()
    {
        var camera = new Camera(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);
        var stacked = Enumerable.Range(0, 8).Select(i => new Candidate($"c{i}", Vector3.Zero, i));

        Check.Equal(5, Pick.Under(stacked, camera, Screen, camera.ToScreen(Vector3.Zero, Screen)!.Value, 24).Length);
    }

    [Test]
    public void ScanActorsFindsLiveActorsWithARootComponent()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var scene = ue.Class("SceneComponent", 0, 0x40, new TestProp("RelativeLocation", "StructProperty", 0x30, 12, Struct: vector));
        var actor = ue.Class("Actor", 0, 0x40, new TestProp("RootComponent", "ObjectProperty", 0x30, 8));
        var monster = ue.Class("Monster", actor, 0x40);
        var root = ue.Object("Root", scene);
        TestMemory.Write(root + 0x30, new Vector3(1, 2, 3));
        var boss = ue.Object("Boss", monster);
        TestMemory.Write(boss + 0x30, root);
        var template = ue.Object("Default__Monster", monster);
        TestMemory.Write(template + 0x30, root);
        ue.Object("Rootless", monster);

        var spot = Pick.ScanActors().Single();

        Check.Equal(boss, spot.Address);
        Check.Equal("Monster", spot.Class);
        Check.Equal(new Vector3(1, 2, 3), spot.Position);
    }

    [Test]
    public void PickModeTogglesOnItsHotkeyAndEscLeaves()
    {
        var main = new Main();
        main.Initialize();
        TestInput.Hold(Key.F7);
        TestInput.Dispatch(main);
        Check.That(main.Picking, "F7 starts pick mode");
        TestInput.Hold();
        TestInput.Hold(Key.Escape);
        main.Tick();
        Check.That(!main.Picking, "Esc leaves pick mode");
    }

    [Test]
    public void PickModeDrawsWithAndWithoutACamera()
    {
        var main = new Main { Picking = true };
        TestGame.Set(Samples.Rich with { Camera = null });
        TestFrame.Run(main.Render);
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
    }

    [Test]
    public void TheTooltipSurvivesFormatSpecifiersInGameNames()
    {
        var main = new Main { Picking = true };
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
        var main = new Main { Picking = true };
        main.Actors = [new ActorSpot(0x5000, "Actor", Vector3.Zero)];

        TestInput.Hold(Key.Escape);
        main.Tick();

        Check.That(!main.Picking, "Esc leaves pick mode");
        Check.Equal(0, main.Actors.Length);
    }

    [Test]
    public void PickModeScansActorsOnlyWhenAsked()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12);
        var scene = ue.Class("SceneComponent", 0, 0x40, new TestProp("RelativeLocation", "StructProperty", 0x30, 12, Struct: vector));
        var actor = ue.Class("Actor", 0, 0x40, new TestProp("RootComponent", "ObjectProperty", 0x30, 8));
        var hero = ue.Object("Hero", actor);
        TestMemory.Write(hero + 0x30, ue.Object("Root", scene));
        var main = new Main { Picking = true, ActorsJob = new(inline: true) };
        TestGame.Set(Samples.Rich);

        TestFrame.Run(main.Render);
        TestFrame.Run(main.Render);
        Check.Equal(0, main.Actors.Length);

        main.Settings.PickActors.Value = true;
        TestFrame.Run(main.Render);
        TestFrame.Run(main.Render);
        Check.Equal(hero, main.Actors.Single().Address);

        main.PinThing(main.Actors[0]);
        Check.Equal("Actor", main.Pins.Single().Label);
    }
}
