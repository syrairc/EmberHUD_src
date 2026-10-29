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
}
