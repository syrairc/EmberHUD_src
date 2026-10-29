using System.Diagnostics;
using System.Numerics;

namespace EmberHUD.Api.Tests;

public class FightTests
{
    [Fact]
    public void MoveCommandValuesAreTheGameValues()
    {
        string[] names = ["None", "Stop", "Straight", "Seek", "Path", "Circle", "Curve", "Arc", "Blink", "ToPoint", "Spiral", "Polyline", "Bezier"];
        for (var i = 0; i < names.Length; i++) Assert.Equal(i, (int)Enum.Parse<MoveCommandType>(names[i]));
    }

    [Fact]
    public void BulletMoveValuesAreTheGameValues()
    {
        string[] names = ["Straight", "StraightGround", "Lob", "LobSolved", "Link", "Flat", "FollowOwner", "FollowTarget", "Rise"];
        for (var i = 0; i < names.Length; i++) Assert.Equal(i, (int)Enum.Parse<BulletMoveType>(names[i]));
    }

    [Theory]
    [InlineData(0, BulletMoveType.Straight)]
    [InlineData(4, BulletMoveType.Link)]
    [InlineData(8, BulletMoveType.Rise)]
    public void BulletKindMapsKnownMoveTypes(int moveType, BulletMoveType expected)
    {
        Assert.Equal(expected, Bullet(moveType).Kind);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(255)]
    public void BulletKindIsNullOutsideKnownRange(int moveType)
    {
        Assert.Null(Bullet(moveType).Kind);
    }

    [Fact]
    public void ScanIsEmptyWithoutALister()
    {
        Fight.Lister = null;
        Assert.Empty(Fight.Scan(Vector2.Zero, 100));
    }

    [Fact]
    public void ScanAsksTheLister()
    {
        var entity = new FightEntity(1, Vector3.Zero, Vector2.UnitX, FightArchetype.Bullet, [19]);
        try
        {
            Fight.Lister = (at, radius) => radius == 50 ? [entity] : [];
            Assert.Same(entity, Assert.Single(Fight.Scan(Vector2.Zero, 50)));
        }
        finally { Fight.Lister = null; }
    }

    [Fact]
    public void FightersAreWantedForAWhileAfterAsking()
    {
        Game.WantFighters();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.FightersWanted(now, 3));
        Assert.False(Game.FightersWanted(now + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void ReadingFightersAsksForThem()
    {
        Game.FightersAsked = 0;
        Assert.False(Game.FightersWanted(Stopwatch.GetTimestamp(), 3));
        _ = GameState.Empty.Fighters;
        Assert.True(Game.FightersWanted(Stopwatch.GetTimestamp(), 3));
    }

    [Fact]
    public void FightersAreNullByDefault()
    {
        Assert.Null(GameState.Empty.Fighters);
    }

    [Fact]
    public void EntityDefaultsAreEmptyAndNull()
    {
        var entity = new FightEntity(1, Vector3.Zero, Vector2.UnitX, FightArchetype.Unknown, []);
        Assert.Empty(entity.Buffs);
        Assert.Null(entity.Motion);
        Assert.Null(entity.Command);
        Assert.Null(entity.Bullet);
        Assert.Null(entity.Track);
        Assert.Null(entity.Link);
        Assert.Null(entity.Timed);
        Assert.Null(entity.Beam);
        Assert.Null(entity.Npc);
        Assert.Null(entity.Role);
        Assert.Null(entity.Shape);
        Assert.Null(entity.Scale);
        Assert.Null(entity.Camp);
        Assert.Null(entity.Lifetime);
        Assert.Equal(0, entity.OwnerId);
    }

    private static FightBullet Bullet(int moveType) => new(moveType, 0, Vector3.Zero, Vector3.Zero, Vector3.Zero, false, false, false, 0);

    static FightEntity Circle(uint radius, double? scale = null, uint kind = 1) =>
        new(1, new Vector3(10, 20, 99), Vector2.UnitX, FightArchetype.GroundEffect, []) { Shape = new FightShape(kind, radius, 0, 0), Scale = scale };

    [Fact]
    public void CircleCoversPointsInsideItsRadiusFlat()
    {
        var circle = Circle(100);

        Assert.True(circle.Covers(new Vector2(10, 20)));
        Assert.True(circle.Covers(new Vector2(110, 20)));
        Assert.False(circle.Covers(new Vector2(110.5f, 20)));
    }

    [Theory]
    [InlineData(2.0, 150, true)]
    [InlineData(2.0, 250, false)]
    [InlineData(0.5, 60, false)]
    [InlineData(double.NaN, 90, true)]
    [InlineData(0.0, 90, true)]
    [InlineData(-3.0, 90, true)]
    public void CircleRadiusIsScaledOnlyWhenScaleIsSane(double scale, float distance, bool covered) =>
        Assert.Equal(covered, Circle(100, scale).Covers(new Vector2(10 + distance, 20)));

    [Fact]
    public void NoShapeNonCircleOrZeroRadiusCoversNothing()
    {
        var at = new Vector2(10, 20);

        Assert.False(new FightEntity(1, new Vector3(10, 20, 0), Vector2.UnitX, FightArchetype.Unknown, []).Covers(at));
        Assert.False(Circle(100, kind: 2).Covers(at));
        Assert.False(Circle(0).Covers(at));
    }

    [Theory]
    [InlineData(FightCamps.Hostile, true)]
    [InlineData(FightCamps.BossArea, true)]
    [InlineData(FightCamps.Player, false)]
    [InlineData(FightCamps.Servant, false)]
    [InlineData(null, false)]
    public void HostileIsCampOneOrThree(int? camp, bool hostile) =>
        Assert.Equal(hostile, new FightEntity(1, default, default, FightArchetype.Monster, []) { Camp = camp }.IsHostile);

    static FightEntity Area(FightShape shape, Vector2? direction = null, double? scale = null, float x = 10, float y = 20) =>
        new(1, new Vector3(x, y, 0), direction ?? Vector2.UnitX, FightArchetype.Anchor, []) { Shape = shape, Scale = scale };

    [Fact]
    public void ShapeKeepsFourArgumentEqualityAndDefaults()
    {
        var shape = new FightShape(1, 300, 0, 0);

        Assert.Equal(new FightShape(1, 300, 0, 0, 0, 0, 0, 0, 0), shape);
        Assert.Equal(0u, shape.Inner);
        Assert.Equal(0.0, shape.OffsetX);
        Assert.Null(new FightEntity(1, default, default, FightArchetype.Unknown, []).Attack);
        Assert.Equal(new FightAttack(1, 2, 3, 0.5), new FightAttack(1, 2, 3, 0.5));
    }

    [Fact]
    public void RectCoversAlongTheDirectionWithFullExtents()
    {
        var rect = Area(new FightShape(2, 0, 400, 100), Vector2.UnitY);

        Assert.True(rect.Covers(new Vector2(10, 220)));
        Assert.True(rect.Covers(new Vector2(10, -180)));
        Assert.False(rect.Covers(new Vector2(10, 221)));
        Assert.True(rect.Covers(new Vector2(60, 20)));
        Assert.False(rect.Covers(new Vector2(61, 20)));
    }

    [Fact]
    public void RectSizeFollowsScaleAndOffsetMovesTheCentre()
    {
        var scaled = Area(new FightShape(2, 0, 100, 100), scale: 2);
        Assert.True(scaled.Covers(new Vector2(110, 120)));
        Assert.False(scaled.Covers(new Vector2(111, 20)));

        var offset = Area(new FightShape(2, 0, 100, 100, OffsetX: 200, OffsetY: -50), Vector2.UnitY);
        Assert.True(offset.Covers(new Vector2(10 + 50, 20 + 200)));
        Assert.False(offset.Covers(new Vector2(10, 20)));
    }

    [Fact]
    public void SectorCoversOnlyItsAnglesRelativeToTheDirection()
    {
        var sector = Area(new FightShape(4, 100, 0, 0, AngleA: -45, AngleB: 45));

        Assert.True(sector.Covers(new Vector2(10, 20)));
        Assert.True(sector.Covers(new Vector2(60, 20)));
        Assert.True(sector.Covers(new Vector2(10 + 50, 20 + 50)));
        Assert.False(sector.Covers(new Vector2(10 + 50, 20 + 60)));
        Assert.False(sector.Covers(new Vector2(-40, 20)));
        Assert.False(sector.Covers(new Vector2(111, 20)));

        var turned = Area(new FightShape(4, 100, 0, 0, AngleA: -45, AngleB: 45), Vector2.UnitY);
        Assert.True(turned.Covers(new Vector2(10, 80)));
        Assert.False(turned.Covers(new Vector2(60, 20)));
    }

    [Fact]
    public void SectorAngleWrapsAndFullSpanIsACircle()
    {
        var behind = Area(new FightShape(4, 100, 0, 0, AngleA: 135, AngleB: 225));
        Assert.True(behind.Covers(new Vector2(-40, 20)));
        Assert.False(behind.Covers(new Vector2(60, 20)));

        var full = Area(new FightShape(4, 100, 0, 0, AngleA: 0, AngleB: 360));
        Assert.True(full.Covers(new Vector2(-80, 20)));
        Assert.False(Area(new FightShape(4, 100, 0, 0, AngleA: 10, AngleB: 10)).Covers(new Vector2(20, 20)));
    }

    [Fact]
    public void RingExcludesTheHoleAndScalesIt()
    {
        var ring = Area(new FightShape(5, 200, 0, 0, Inner: 100));

        Assert.False(ring.Covers(new Vector2(10, 20)));
        Assert.False(ring.Covers(new Vector2(10 + 99, 20)));
        Assert.True(ring.Covers(new Vector2(10 + 100, 20)));
        Assert.True(ring.Covers(new Vector2(10 + 200, 20)));
        Assert.False(ring.Covers(new Vector2(10 + 201, 20)));

        var scaled = Area(new FightShape(5, 200, 0, 0, Inner: 100), scale: 2);
        Assert.False(scaled.Covers(new Vector2(10 + 150, 20)));
        Assert.True(scaled.Covers(new Vector2(10 + 350, 20)));
    }

    [Fact]
    public void KindHighBitsAreIgnoredAndPolygonIsUnsupported()
    {
        Assert.True(Area(new FightShape(0x0100 | 5, 200, 0, 0, Inner: 100)).Covers(new Vector2(10 + 150, 20)));
        var polygon = Area(new FightShape(3, 100, 100, 100));
        Assert.False(polygon.Covers(new Vector2(10, 20)));
        Assert.Equal(0, polygon.Outline(new Vector2[64]));
    }

    [Fact]
    public void CircleOutlineIsAThirtyTwoGonOnTheRadius()
    {
        var points = new Vector2[64];
        var circle = Area(new FightShape(1, 100, 0, 0), scale: 2);

        Assert.Equal(32, circle.Outline(points));
        for (var i = 0; i < 32; i++) Assert.Equal(200, Vector2.Distance(points[i], new Vector2(10, 20)), 0.01);
    }

    [Fact]
    public void RingOutlineIsTheOuterCircle()
    {
        var points = new Vector2[64];

        Assert.Equal(32, Area(new FightShape(5, 150, 0, 0, Inner: 50)).Outline(points));
        Assert.Equal(150, Vector2.Distance(points[7], new Vector2(10, 20)), 0.01);
    }

    [Fact]
    public void RectOutlineIsFourCornersInTheLocalFrame()
    {
        var points = new Vector2[64];
        var rect = Area(new FightShape(2, 0, 400, 100, OffsetX: 100), Vector2.UnitY);

        Assert.Equal(4, rect.Outline(points));
        Assert.Equal([-40f, -40f, 60f, 60f], points[..4].Select(point => MathF.Round(point.X)).Order());
        Assert.Equal([-80f, -80f, 320f, 320f], points[..4].Select(point => MathF.Round(point.Y)).Order());
    }

    [Fact]
    public void SectorOutlineStartsAtTheCentreAndSpansTheArc()
    {
        var points = new Vector2[64];
        var sector = Area(new FightShape(4, 100, 0, 0, AngleA: -45, AngleB: 45));

        var count = sector.Outline(points);

        Assert.True(count >= 4);
        Assert.Equal(new Vector2(10, 20), points[0]);
        var edge = 100 * MathF.Sin(MathF.PI / 4);
        Assert.Equal(20 - edge, points[1].Y, 0.01);
        Assert.Equal(20 + edge, points[count - 1].Y, 0.01);
        Assert.All(points[1..count], point => Assert.Equal(100, Vector2.Distance(point, points[0]), 0.01));
        Assert.Equal(32, Area(new FightShape(4, 100, 0, 0, AngleA: 0, AngleB: 360)).Outline(points));
    }

    [Fact]
    public void OutlineIsEmptyWithoutAShapeOrRoom()
    {
        var points = new Vector2[64];

        Assert.Equal(0, new FightEntity(1, default, Vector2.UnitX, FightArchetype.Anchor, []).Outline(points));
        Assert.Equal(0, Area(new FightShape(1, 100, 0, 0)).Outline(new Vector2[31]));
        Assert.Equal(0, Area(new FightShape(2, 0, 100, 100)).Outline(new Vector2[3]));
        Assert.Equal(0, Area(new FightShape(4, 100, 0, 0, AngleA: -45, AngleB: 45)).Outline(new Vector2[2]));
        Assert.Equal(0, Area(new FightShape(1, 0, 0, 0)).Outline(points));
    }

    [Fact]
    public void NonFiniteInputsDrawAndCoverNothing()
    {
        var points = new Vector2[64];
        var nan = float.NaN;
        FightEntity[] bad =
        [
            Area(new FightShape(1, 100, 0, 0), x: nan),
            Area(new FightShape(2, 0, 100, 100), new Vector2(0, nan)),
            Area(new FightShape(5, 100, 0, 0, OffsetX: double.PositiveInfinity)),
            Area(new FightShape(4, 100, 0, 0, AngleA: -45, AngleB: 45, OffsetY: double.NaN)),
        ];

        foreach (var entity in bad)
        {
            Assert.Equal(0, entity.Outline(points));
            Assert.False(entity.Covers(new Vector2(10, 20)));
        }
        Assert.False(Area(new FightShape(1, 100, 0, 0)).Covers(new Vector2(nan, 20)));
    }
}
