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
    public void OwnBulletsAreDropped()
    {
        var (world, reader) = Rig();
        Good(world);
        Assert.Empty(reader.Read([Owner(friendly: true)]));
        Assert.Empty(reader.Read([Owner(kind: EntityKind.Player)]));
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
