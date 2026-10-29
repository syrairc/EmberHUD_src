using System.Collections.Generic;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Threats;

public sealed class FightDataTests
{
    static readonly Settings Plain = new() { LineLength = 1000 };

    static readonly Projectile Shot = new(7, 1, Rarity.Rare, new Vector2(0, 500), new Vector2(0, 500), Vector2.UnitX, 100, 600);

    static FightEntity Fighter(int id = 7, FightArchetype archetype = FightArchetype.Bullet) => new(id, Vector3.Zero, Vector2.UnitX, archetype, []);

    static FightEntity Flying(double range, double travelled, double speed = 600) => Fighter() with
    {
        Bullet = new FightBullet(0, range, Vector3.Zero, Vector3.Zero, Vector3.Zero, false, true, false, 0),
        Motion = new FightMotion(speed, 0, Vector2.Zero, Vector2.Zero, false, true, 0, travelled),
    };

    static float? Length(FightEntity? fighter) => FightData.LineLength(Shot, fighter, Plain);

    [Test]
    public void IndexKeysFightersByIdAndClearsTheDictionary()
    {
        var into = new Dictionary<int, FightEntity> { [99] = Fighter(99) };
        FightData.Index([Fighter(1), Fighter(2)], into);
        Check.Equal(2, into.Count);
        Check.That(into.ContainsKey(1) && into.ContainsKey(2) && !into.ContainsKey(99), "keys");
        FightData.Index(null, into);
        Check.Equal(0, into.Count);
    }

    [Test]
    public void NoFighterOrNoBlocksFallsBackToTheSetting()
    {
        Check.Equal<float?>(null, Length(null));
        Check.Equal<float?>(null, Length(Fighter()));
        Check.Equal<float?>(null, Length(Fighter() with { Bullet = Flying(800, 0).Bullet }));
        Check.Equal<float?>(null, Length(Fighter() with { Motion = Flying(800, 0).Motion }));
    }

    [Test]
    public void LengthStopsWhereTheRangeEnds()
    {
        Check.Equal<float?>(500f, Length(Flying(800, 300)));
        Check.Equal<float?>(1000f, Length(Flying(5000, 0)));
    }

    [Test]
    public void RangeOfZeroOrLessIsIgnored()
    {
        Check.Equal<float?>(null, Length(Flying(0, 0)));
        Check.Equal<float?>(null, Length(Flying(-5, 0)));
    }

    [Test]
    public void NothingLeftOfTheRangeDrawsNothing()
    {
        Check.Equal<float?>(0f, Length(Flying(800, 800)));
        Check.Equal<float?>(0f, Length(Flying(800, 900)));
    }

    [Test]
    public void LifetimeClipsToTheDistanceItStillFlies()
    {
        Check.Equal<float?>(300f, Length(Flying(5000, 0, 600) with { Lifetime = 0.5 }));
        Check.Equal<float?>(1000f, Length(Flying(5000, 0, 600) with { Lifetime = 0 }));
        Check.Equal<float?>(1000f, Length(Flying(5000, 0, 0) with { Lifetime = 0.5 }));
        Check.Equal<float?>(800f, Length(Flying(800, 0, 600) with { Lifetime = 10 }));
    }

    [Test]
    public void InsaneNumbersFallBack()
    {
        Check.Equal<float?>(null, Length(Flying(double.NaN, 0)));
        Check.Equal<float?>(null, Length(Flying(800, double.NaN)));
        Check.Equal<float?>(null, Length(Flying(double.PositiveInfinity, 0)));
        Check.Equal<float?>(null, Length(Flying(1e9, 0)));
        Check.Equal<float?>(1000f, Length(Flying(5000, 0, double.NaN) with { Lifetime = 0.5 }));
        Check.Equal<float?>(1000f, Length(Flying(5000, 0, 600) with { Lifetime = double.NaN }));
    }

    static FightEntity Beam(Vector3 start, Vector3 end) => Fighter() with { Beam = new FightBeam(start, end) };

    [Test]
    public void BeamGivesTheExactSegment()
    {
        var segment = FightData.Segment(Shot, Beam(new Vector3(10, 20, 5), new Vector3(110, 20, 9)), Plain);
        Check.Equal(new Vector2(10, 20), segment!.Value.Start);
        Check.Equal(new Vector2(110, 20), segment.Value.End);
    }

    [Test]
    public void InsaneOrShortBeamsGiveNoSegment()
    {
        Check.That(FightData.Segment(Shot, null, Plain) is null, "null");
        Check.That(FightData.Segment(Shot, Fighter(), Plain) is null, "no beam");
        Check.That(FightData.Segment(Shot, Beam(Vector3.Zero, new Vector3(0.5f, 0.5f, 0)), Plain) is null, "short");
        Check.That(FightData.Segment(Shot, Beam(Vector3.Zero, new Vector3(float.NaN, 0, 0)), Plain) is null, "nan");
        Check.That(FightData.Segment(Shot, Beam(new Vector3(float.PositiveInfinity, 0, 0), new Vector3(5, 0, 0)), Plain) is null, "infinite");
        Check.That(FightData.Segment(Shot, Beam(Vector3.Zero, new Vector3(1e9f, 0, 0)), Plain) is null, "huge");
    }

    static Entity Owner(int id, float x, float y) => new(id, 1, new Vector3(x, y, 0), EntityKind.Monster, Rarity.Rare);

    static FightEntity Commanded(MoveCommandType type) => Fighter() with { Command = new FightCommand(type, 1, 0, 0) };

    [Test]
    public void CircleCommandOrbitsTheOwner()
    {
        Check.Equal<Vector2?>(new Vector2(300, 400), FightData.OrbitCentre(Shot, Commanded(MoveCommandType.Circle), [Owner(9, 1, 1), Owner(1, 300, 400)]));
    }

    [Test]
    public void CircleCommandWithoutAnOwnerKeepsTheHostCentre()
    {
        var guessed = Shot with { OrbitCentre = new Vector2(5, 6) };
        Check.Equal<Vector2?>(new Vector2(5, 6), FightData.OrbitCentre(guessed, Commanded(MoveCommandType.Circle), [Owner(9, 1, 1)]));
        Check.Equal<Vector2?>(null, FightData.OrbitCentre(Shot, Commanded(MoveCommandType.Circle), []));
    }

    [Test]
    public void AnyOtherCommandClearsAGuessedOrbit()
    {
        var guessed = Shot with { OrbitCentre = new Vector2(5, 6) };
        Check.Equal<Vector2?>(null, FightData.OrbitCentre(guessed, Commanded(MoveCommandType.Straight), [Owner(1, 300, 400)]));
        Check.Equal<Vector2?>(null, FightData.OrbitCentre(guessed, Commanded(MoveCommandType.Seek), []));
    }

    [Test]
    public void NoCommandLeavesTheHostGuess()
    {
        var guessed = Shot with { OrbitCentre = new Vector2(5, 6) };
        Check.Equal<Vector2?>(new Vector2(5, 6), FightData.OrbitCentre(guessed, null, []));
        Check.Equal<Vector2?>(new Vector2(5, 6), FightData.OrbitCentre(guessed, Fighter(), []));
        Check.Equal<Vector2?>(null, FightData.OrbitCentre(Shot, Fighter(), []));
    }

    [Test]
    public void InsaneOwnerPositionKeepsTheHostCentre()
    {
        var guessed = Shot with { OrbitCentre = new Vector2(5, 6) };
        Check.Equal<Vector2?>(new Vector2(5, 6), FightData.OrbitCentre(guessed, Commanded(MoveCommandType.Circle), [Owner(1, float.NaN, 0)]));
    }

    [Test]
    public void HomingNeedsATarget()
    {
        Check.That(!FightData.Homing(null), "null");
        Check.That(!FightData.Homing(Fighter()), "no track");
        Check.That(!FightData.Homing(Fighter() with { Track = new FightTrack(false, 0, true, false, 0) }), "no target");
        Check.That(FightData.Homing(Fighter() with { Track = new FightTrack(true, 3, true, false, 0) }), "target");
    }

    static FightEntity PlayerAt(int id, float x) => Fighter(id, FightArchetype.Player) with { Position = new Vector3(x, 0, 0) };

    [Test]
    public void PlayerFighterIsTheNearestPlayerWithinFiftyUnits()
    {
        Check.Equal(2, FightData.PlayerFighterId([PlayerAt(1, 40), PlayerAt(2, 10), Fighter(3, FightArchetype.Monster)], Vector2.Zero));
        Check.Equal(0, FightData.PlayerFighterId([PlayerAt(1, 60)], Vector2.Zero));
        Check.Equal(0, FightData.PlayerFighterId([Fighter(3, FightArchetype.Monster)], Vector2.Zero));
        Check.Equal(0, FightData.PlayerFighterId(null, Vector2.Zero));
        Check.Equal(0, FightData.PlayerFighterId([PlayerAt(1, 0)], new Vector2(float.NaN, 0)));
    }

    static FightEntity Hazard() => Fighter(7, FightArchetype.GroundEffect);

    [Test]
    public void HazardRemainingPrefersTimedThenLifetime()
    {
        Check.Equal<double?>(3.0, FightData.HazardRemaining(Hazard() with { Timed = new FightTimed(2, 5), Lifetime = 99 }));
        Check.Equal<double?>(99.0, FightData.HazardRemaining(Hazard() with { Timed = new FightTimed(2, 0), Lifetime = 99 }));
        Check.Equal<double?>(99.0, FightData.HazardRemaining(Hazard() with { Timed = new FightTimed(-1, 5), Lifetime = 99 }));
        Check.Equal<double?>(4.5, FightData.HazardRemaining(Hazard() with { Lifetime = 4.5 }));
        Check.Equal<double?>(null, FightData.HazardRemaining(Hazard()));
        Check.Equal<double?>(null, FightData.HazardRemaining(null));
        Check.Equal<double?>(null, FightData.HazardRemaining(Hazard() with { Timed = new FightTimed(double.NaN, 5) }));
        Check.Equal<double?>(null, FightData.HazardRemaining(Hazard() with { Lifetime = double.PositiveInfinity }));
    }
}
