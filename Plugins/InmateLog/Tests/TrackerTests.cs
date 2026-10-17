using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace InmateLog;

public sealed class TrackerTests
{
    static readonly InmateKind Rare = new(InmateSpecies.Wolf, 3);

    static Entity Mob(int id, double life, double maxLife, Vector3? at = null, EntityKind kind = EntityKind.Monster, int cfgId = 5027023) =>
        new(id, cfgId, at ?? Vector3.Zero, kind, Rarity.Rare) { Life = life, MaxLife = maxLife };

    static GameState State(int area, params Entity[] entities) =>
        new() { AreaId = area, Player = new Player(Vector3.Zero, Vector2.UnitX), Entities = entities };

    static Tracker InmatesOnly() => new(cfgId => cfgId == 5027023 ? Rare : null);

    [Test]
    public void DeadInmateThatLeavesIsKilled()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(11, 40, 90)), 1);
        tracker.Update(State(7, Mob(11, 0, 90)), 2);
        var events = tracker.Update(State(7), 3);
        Check.Equal(1, events.Length);
        Check.Equal(SightingEnd.Killed, events[0].End);
        Check.Equal(Rare, events[0].Kind);
        Check.Equal(5027023, events[0].CfgId);
    }

    [Test]
    public void LivingInmateThatLeavesIsUnsure()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(12, 55, 80)), 1);
        var events = tracker.Update(State(7), 2);
        Check.Equal(1, events.Length);
        Check.Equal(SightingEnd.Unsure, events[0].End);
        Check.Equal(55.0, events[0].LastLife);
        Check.Equal(80.0, events[0].MaxLife);
    }

    [Test]
    public void AreaChangeDropsTrackingWithoutEvents()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(13, 0, 70)), 1);
        Check.Equal(0, tracker.Update(State(8), 2).Length);
        Check.Equal(0, tracker.Update(State(8), 3).Length);
    }

    [Test]
    public void ReusedIdInANewAreaIsANewInmate()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(14, 20, 60)), 10);
        tracker.Update(State(8, Mob(14, 30, 65)), 50);
        var events = tracker.Update(State(8), 53);
        Check.Equal(1, events.Length);
        Check.Equal(0.0, events[0].SecondsSeen);
        Check.Equal(30.0, events[0].LastLife);
    }

    [Test]
    public void NonInmateMonstersAreIgnored()
    {
        var tracker = new Tracker(_ => null);
        tracker.Update(State(7, Mob(15, 0, 50)), 1);
        Check.Equal(0, tracker.Update(State(7), 2).Length);
    }

    [Test]
    public void NonMonsterEntitiesAreIgnored()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(21, 0, 50, kind: EntityKind.Npc)), 1);
        Check.Equal(0, tracker.Update(State(7), 2).Length);
    }

    [Test]
    public void UnreadableLifeIsNeverKilled()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(16, 0, 0)), 1);
        var events = tracker.Update(State(7), 2);
        Check.Equal(1, events.Length);
        Check.Equal(SightingEnd.Unsure, events[0].End);
    }

    [Test]
    public void EachInmateReportsOnce()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(17, 0, 75)), 1);
        Check.Equal(1, tracker.Update(State(7), 2).Length);
        Check.Equal(0, tracker.Update(State(7), 3).Length);
    }

    [Test]
    public void SecondsSeenAndDistanceAreFilled()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(18, 30, 95, new Vector3(6, 8, 0))), 100);
        tracker.Update(State(7, Mob(18, 25, 95, new Vector3(30, 40, 0))), 104.5);
        var events = tracker.Update(State(7), 106);
        Check.Equal(4.5, events[0].SecondsSeen);
        Check.Equal(50f, events[0].Distance);
        Check.Equal(25.0, events[0].LastLife);
    }

    [Test]
    public void OneFrameFlickerProducesNoSighting()
    {
        var tracker = InmatesOnly();
        Check.Equal(0, tracker.Update(State(7, Mob(31, 40, 90)), 1).Length);
        Check.Equal(0, tracker.Update(State(7), 1.1).Length);
        Check.Equal(0, tracker.Update(State(7, Mob(31, 40, 90)), 1.2).Length);
        Check.Equal(0, tracker.Update(State(7, Mob(31, 40, 90)), 5).Length);
    }

    [Test]
    public void MissingFrameThenGoneForHalfASecondIsOneKill()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(32, 0, 90)), 1);
        Check.Equal(0, tracker.Update(State(7), 1.25).Length);
        tracker.Update(State(7, Mob(32, 0, 90)), 1.5);
        Check.Equal(0, tracker.Update(State(7), 1.75).Length);
        var events = tracker.Update(State(7), 2);
        Check.Equal(1, events.Length);
        Check.Equal(SightingEnd.Killed, events[0].End);
        Check.Equal(0.5, events[0].SecondsSeen);
        Check.Equal(0, tracker.Update(State(7), 3).Length);
    }

    [Test]
    public void SceneDeadEndsAsKilled()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(41, 55, 90)), 1);
        var events = tracker.Update(State(7), 2, (_, _) => false);
        Check.Equal(SightingEnd.Killed, events[0].End);
        Check.Equal(SightingSource.SceneDead, events[0].Source);
        Check.Equal(55.0, events[0].LastLife);
    }

    [Test]
    public void SceneAliveEndsAsUnsure()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(42, 0, 90)), 1);
        var events = tracker.Update(State(7), 2, (_, _) => true);
        Check.Equal(SightingEnd.Unsure, events[0].End);
        Check.Equal(SightingSource.SceneAlive, events[0].Source);
    }

    [Test]
    public void UnknownSceneFallsBackToLife()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(43, 0, 90), Mob(44, 40, 90)), 1);
        var events = tracker.Update(State(7), 2, (_, _) => null);
        Check.Equal(2, events.Length);
        Check.Equal(SightingEnd.Killed, events[0].End);
        Check.Equal(SightingSource.Life, events[0].Source);
        Check.Equal(SightingEnd.Unsure, events[1].End);
        Check.Equal(SightingSource.Life, events[1].Source);
    }

    [Test]
    public void LookupOnlyCalledWhenAnInmateEnds()
    {
        var tracker = InmatesOnly();
        var calls = 0;
        bool? Count(int id, int cfgId) { calls++; return null; }
        tracker.Update(State(7, Mob(45, 40, 90)), 1, Count);
        tracker.Update(State(7, Mob(45, 40, 90)), 1.2, Count);
        tracker.Update(State(7), 1.4, Count);
        Check.Equal(0, calls);
        tracker.Update(State(7), 2, Count);
        Check.Equal(1, calls);
    }

    [Test]
    public void LookupGetsTheIdAndCfgIdOfTheEndingInmate()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(46, 40, 90)), 1);
        (int, int)? asked = null;
        tracker.Update(State(7), 2, (id, cfgId) => { asked = (id, cfgId); return null; });
        Check.Equal((46, 5027023), asked);
    }

    [Test]
    public void GoneForLessThanHalfASecondIsNotEndedYet()
    {
        var tracker = InmatesOnly();
        tracker.Update(State(7, Mob(33, 0, 90)), 1);
        Check.Equal(0, tracker.Update(State(7), 1.4).Length);
    }
}
