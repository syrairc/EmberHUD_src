using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class ProjectileReaderTests
{
    private static readonly (double X, double Y) Position = (4127.27, 4389.85);
    private static readonly (double X, double Y) Direction = (0.914, -0.405);
    private static readonly (double X, double Y) Origin = (3825.37, 4523.12);

    private static Entity Owner(int id = 7, EntityKind kind = EntityKind.Monster, Rarity rarity = Rarity.Boss, bool friendly = false) =>
        new(id, 100, Vector3.Zero, kind, rarity) { Friendly = friendly };

    private static (FakeWorld World, ProjectileReader Reader) Rig()
    {
        var world = new FakeWorld();
        return (world, new ProjectileReader(world.Memory, FakeWorld.Module));
    }

    private static FakeWorld.FakeBullet Good(FakeWorld world, uint id = 50, uint owner = 7) =>
        world.AddBullet(id, owner, Position, Direction, Origin);

    [Fact]
    public void ReadsPositionOriginDirectionRadiusAndSpeed()
    {
        var (world, reader) = Rig();
        Good(world);
        var projectile = Assert.Single(reader.Read([Owner()]));
        Assert.Equal(50, projectile.Id);
        Assert.Equal(7, projectile.OwnerId);
        Assert.Equal(Rarity.Boss, projectile.OwnerRarity);
        Assert.Equal(new Vector2(4127.27f, 4389.85f), projectile.Position);
        Assert.Equal(new Vector2(3825.37f, 4523.12f), projectile.Origin);
        Assert.Equal(240, projectile.Radius);
        Assert.Equal(1200, projectile.Speed);
        Assert.Equal(1, projectile.Direction.Length(), 3);
        Assert.Equal(0.914f / MathF.Sqrt(0.914f * 0.914f + 0.405f * 0.405f), projectile.Direction.X, 3);
    }

    [Fact]
    public void OrbitingBulletsHeadAlongTheirMovementNotTheirStaleTransformDirection()
    {
        var (world, reader) = Rig();
        var bullet = Good(world);
        reader.Read([Owner()]);
        world.Memory.WriteArray(bullet.Transform + Offsets.TransformPosition, new[] { Position.X, Position.Y + 40 });
        var projectile = Assert.Single(reader.Read([Owner()]));
        Assert.Equal(0, projectile.Direction.X, 3);
        Assert.Equal(1, projectile.Direction.Y, 3);
    }

    [Fact]
    public void BulletsKeepTheirLastMovementDirectionWhileStandingStill()
    {
        var (world, reader) = Rig();
        var bullet = Good(world);
        reader.Read([Owner()]);
        world.Memory.WriteArray(bullet.Transform + Offsets.TransformPosition, new[] { Position.X, Position.Y + 40 });
        reader.Read([Owner()]);
        var projectile = Assert.Single(reader.Read([Owner()]));
        Assert.Equal(1, projectile.Direction.Y, 3);
    }

    [Fact]
    public void MovingLasersKeepTheirOwnHeading()
    {
        var (world, reader) = Rig();
        var transform = world.AddLaser(60, 7, Position, (0.2537, 0.9673), (5000, 100));
        reader.Read([Owner()]);
        world.Memory.WriteArray(transform + Offsets.TransformPosition,new[] { Position.X + 40, Position.Y });
        Assert.Equal(0.2537f, Assert.Single(reader.Read([Owner()])).Direction.X, 3);
    }

    [Fact]
    public void OwnBulletsAreDropped()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, ownerCamp: 2);
        Assert.Empty(reader.Read([Owner()]));
        Assert.Empty(reader.Read([Owner(kind: EntityKind.Player)]));
    }

    [Fact]
    public void FriendlyConfigFlagDoesNotHideAHostileOwner()
    {
        var (world, reader) = Rig();
        Good(world);
        Assert.Single(reader.Read([Owner(friendly: true)]));
    }

    [Fact]
    public void RectangleShapesUseTheSmallerExtentAsRadius()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, radius: 0, rect: (150, 140));
        Assert.Equal(140, Assert.Single(reader.Read([Owner()])).Radius);
    }

    [Fact]
    public void RectangleWithNoExtentIsDropped()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, radius: 0, rect: (0, 140));
        Assert.Empty(reader.Read([Owner()]));
    }

    [Fact]
    public void ParkedBulletsWithNoMoveDirectionAreDroppedBecauseTheirDirectionIsStale()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, speed: 0, moveDirection: false);
        Assert.Empty(reader.Read([Owner()]));
    }

    [Fact]
    public void ParkedRectanglesAreKeptWithoutAMoveDirectionBecauseARectangleHasAHeading()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, radius: 0, speed: 0, rect: (3100, 3100), moveDirection: false);
        Assert.Single(reader.Read([Owner()]));
    }

    [Fact]
    public void MovingBulletsAreKeptWithoutAMoveDirection()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, moveDirection: false);
        Assert.Single(reader.Read([Owner()]));
    }

    [Fact]
    public void ParkedWaveTelegraphsWithAMoveDirectionAreKept()
    {
        var (world, reader) = Rig();
        world.AddBullet(50, 7, Position, Direction, Origin, speed: 0);
        Assert.Single(reader.Read([Owner()]));
    }

    [Fact]
    public void ABulletlessHostileRectWithDamageIsALaserAlongItsOwnDirection()
    {
        var (world, reader) = Rig();
        world.AddLaser(60, 7, Position, (0.2537, 0.9673), (5000, 100));
        var laser = Assert.Single(reader.Read([Owner()]));
        Assert.Equal(60, laser.Id);
        Assert.Equal(7, laser.OwnerId);
        Assert.Equal(new Vector2(4127.27f, 4389.85f), laser.Position);
        Assert.Equal(100, laser.Radius);
        Assert.Equal(0.2537f, laser.Direction.X, 3);
        Assert.Equal(0.9673f, laser.Direction.Y, 3);
    }

    [Fact]
    public void LaserLikeRectsAreDroppedWithoutDamageOrWhenOwnedByUs()
    {
        var (world, reader) = Rig();
        world.AddLaser(60, 7, Position, Direction, (5000, 100), damage: false);
        Assert.Empty(reader.Read([Owner()]));
        var (own, ownReader) = Rig();
        own.AddLaser(61, 7, Position, Direction, (5000, 100), ownerCamp: 2);
        Assert.Empty(ownReader.Read([Owner()]));
    }

    [Fact]
    public void UnknownOwnerIsDropped()
    {
        var (world, reader) = Rig();
        Good(world);
        Assert.Empty(reader.Read([Owner(id: 8)]));
    }

    [Fact]
    public void GarbageIsSkipped()
    {
        var (world, reader) = Rig();
        var good = Good(world, 50);
        world.AddBullet(51, 7, (double.NaN, 0), Direction, Origin);
        world.AddBullet(56, 7, Position, (1e30, 1e30), Origin);
        world.AddBullet(52, 7, Position, (0, 0), Origin);
        world.AddBullet(53, 7, Position, Direction, Origin, radius: Offsets.MaxProjectileRadius + 1);
        world.AddBullet(54, 7, Position, Direction, Origin, speed: Offsets.MaxProjectileSpeed + 1);
        var noOwner = world.AddBullet(55, 7, Position, Direction, Origin);
        world.Memory.Write(noOwner.Attachment + Offsets.AttachmentOwnerId, 0u);
        var projectile = Assert.Single(reader.Read([Owner()]));
        Assert.Equal(50, projectile.Id);
        Assert.NotEqual(0, good.Bullet);
    }

    [Fact]
    public void MonstersAndOtherEntitiesAreNotProjectiles()
    {
        var (world, reader) = Rig();
        var monster = world.AddEntity(7, 100, true, 0, 0, 0);
        world.AddCast(monster, 1, 1, 0.5, 1);
        Assert.Empty(reader.Read([Owner()]));
    }

    [Fact]
    public void NoFightWorldGivesEmpty()
    {
        var (_, reader) = Rig();
        Assert.Empty(reader.Read([Owner()]));
    }

    [Fact]
    public void ReaderNeverThrowsOnAreaChange()
    {
        var (world, reader) = Rig();
        Good(world);
        Assert.Single(reader.Read([Owner()]));
        var next = new FakeWorld();
        Good(next);
        var other = new ProjectileReader(next.Memory, FakeWorld.Module);
        Assert.Single(other.Read([Owner()]));
        Assert.Empty(new ProjectileReader(new FakeMemory(), FakeWorld.Module).Read([Owner()]));
    }
}
