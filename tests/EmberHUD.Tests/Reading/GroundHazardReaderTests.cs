using System.Numerics;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class GroundHazardReaderTests
{
    private static readonly (double X, double Y) Pool = (-1144.79, 22562.14);

    private static (FakeWorld World, GroundHazardReader Reader) Rig()
    {
        var world = new FakeWorld();
        return (world, new GroundHazardReader(world.Memory, FakeWorld.Module));
    }

    private static Entity Monster(float x, float y, bool friendly = false) =>
        new(7, 100, new Vector3(x, y, 0), EntityKind.Monster, Rarity.Normal) { Friendly = friendly };

    [Fact]
    public void ReadsAHostileCircleArea()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Pool, 500, 1);
        var hazard = Assert.Single(reader.Read([Monster(5000, 5000)]));
        Assert.Equal(3652, hazard.Id);
        Assert.Equal(HazardKind.Area, hazard.Kind);
        Assert.Equal(500, hazard.Radius);
        Assert.Equal(new Vector2(-1144.79f, 22562.14f), hazard.Centre);
    }

    [Fact]
    public void ScaleMultipliesTheRadius()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Pool, 500, 1, scale: 2);
        Assert.Equal(1000, Assert.Single(reader.Read([Monster(5000, 5000)])).Radius);
    }

    [Fact]
    public void PlayerCampAreasAreDropped()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Pool, 500, 2);
        Assert.Empty(reader.Read([Monster(5000, 5000)]));
    }

    [Fact]
    public void StackedAreasCollapseToOne()
    {
        var (world, reader) = Rig();
        for (uint i = 0; i < 16; i++) world.AddArea(3600 + i, Pool, 500, 1);
        Assert.Single(reader.Read([Monster(5000, 5000)]));
    }

    [Fact]
    public void AreaOnAHostileMonsterIsAnAuraAndDropped()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Pool, 500, 1);
        Assert.Empty(reader.Read([Monster((float)Pool.X + 30, (float)Pool.Y)]));
        Assert.Single(reader.Read([Monster((float)Pool.X + 500, (float)Pool.Y)]));
    }

    [Fact]
    public void ReadsAHostileOwnedDamageCircle()
    {
        var (world, reader) = Rig();
        world.AddRock(9031, 7504, (25355.76, -5705.46), 175);
        var hazard = Assert.Single(reader.Read([Monster(0, 0)]));
        Assert.Equal(HazardKind.Damage, hazard.Kind);
        Assert.Equal(175, hazard.Radius);
        Assert.Equal(new Vector2(25355.76f, -5705.46f), hazard.Centre);
    }

    [Fact]
    public void PlayerOwnedDamageCirclesAreDropped()
    {
        var (world, reader) = Rig();
        world.AddRock(9031, 7504, (25355.76, -5705.46), 175, ownerCamp: 2);
        Assert.Empty(reader.Read([Monster(0, 0)]));
    }

    [Fact]
    public void ADamageCircleWithABulletIsNotARock()
    {
        var (world, reader) = Rig();
        world.AddRock(9031, 7504, (25355.76, -5705.46), 175, bullet: true);
        Assert.Empty(reader.Read([Monster(0, 0)]));
    }

    [Fact]
    public void AnAreaWithDamageDataIsStillAnArea()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Pool, 500, 1, damage: true);
        Assert.Equal(HazardKind.Area, Assert.Single(reader.Read([Monster(5000, 5000)])).Kind);
    }

    [Fact]
    public void GarbageIsSkipped()
    {
        var (world, reader) = Rig();
        world.AddArea(1, (double.NaN, 0), 500, 1);
        world.AddArea(2, (100, 100), 0, 1);
        world.AddArea(3, (200, 200), 30000, 1);
        world.AddArea(4, (300, 300), 500, 1, scale: double.NaN);
        world.AddArea(5, Pool, 500, 1);
        Assert.Equal(5, Assert.Single(reader.Read([Monster(5000, 5000)])).Id);
    }

    [Fact]
    public void NoFightWorldReadsNothing()
    {
        var world = new FakeWorld();
        Assert.Empty(new GroundHazardReader(world.Memory, FakeWorld.Module).Read([Monster(0, 0)]));
    }
}
