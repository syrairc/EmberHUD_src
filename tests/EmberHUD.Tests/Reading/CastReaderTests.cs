using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class CastReaderTests
{
    private static Entity Monster(FakeEntity fake) => new((int)fake.Id, 100, Vector3.Zero, EntityKind.Monster, Rarity.Normal);

    private static (FakeWorld World, CastReader Reader, FakeEntity Monster) Rig()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(7, 100, true, 0, 0, 0);
        return (world, new CastReader(world.Memory, FakeWorld.Module), monster);
    }

    [Fact]
    public void ReadsStoneStateProgressTargetAndDirection()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 170203, 1, remaining: 1.2625, total: 1.693, target: (13149.18, -3683.16), direction: (0.4945, 0.8692));
        var cast = Assert.NotNull(reader.Apply([Monster(monster)])[0].Cast);
        Assert.Equal(170203, cast.StoneId);
        Assert.Equal(1, cast.State);
        Assert.Equal(1 - 1.2625 / 1.693, cast.Progress, 3);
        Assert.Equal(1.2625, cast.SecondsLeft, 2);
        Assert.Equal(new Vector2(13149.18f, -3683.16f), cast.Target);
        Assert.Equal(new Vector2(0.4945f, 0.8692f), cast.Direction);
    }

    [Fact]
    public void SpeedMultiplierShortensTheTotal()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 180507, 1, remaining: 0.134, total: 0.89, speed: 1.6667);
        var cast = Assert.NotNull(reader.Apply([Monster(monster)])[0].Cast);
        Assert.Equal(1 - 0.134 / (0.89 / 1.6667), cast.Progress, 3);
    }

    [Fact]
    public void WorksThroughTheDirectTable()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 1, 0.5, 1, direct: true);
        Assert.Equal(5, reader.Apply([Monster(monster)])[0].Cast?.StoneId);
    }

    [Fact]
    public void StartDelayStateIsACastAtZeroProgress()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 0, 0, 2);
        var cast = Assert.NotNull(reader.Apply([Monster(monster)])[0].Cast);
        Assert.Equal((0, 0f, 2f), (cast.State, cast.Progress, cast.SecondsLeft));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    public void LaterStatesAreNotACast(byte state)
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, state, 0.5, 1);
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Fact]
    public void SkipsOverSkillsToFindTheCastingOne()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 1, 7, 0, 1);
        world.AddCast(monster, 2, 1, 0.5, 1);
        world.AddCast(monster, 3, 7, 0, 1);
        Assert.Equal(2, reader.Apply([Monster(monster)])[0].Cast?.StoneId);
    }

    [Fact]
    public void SkillOwnedBySomeoneElseIsRejected()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 1, 0.5, 1, caster: 99);
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Theory]
    [InlineData(double.NaN, 1, 1)]
    [InlineData(0.5, 0, 1)]
    [InlineData(0.5, 1e9, 1)]
    [InlineData(0.5, 1, 0)]
    [InlineData(0.5, 1, double.NaN)]
    [InlineData(-5, 1, 1)]
    [InlineData(500, 1, 1)]
    public void GarbageTimersAreRejected(double remaining, double total, double speed)
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 1, remaining, total, speed);
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Fact]
    public void NonFiniteTargetIsRejected()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 1, 0.5, 1, target: (double.NaN, 0));
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Fact]
    public void MonsterWithoutSkillComponentHasNoCast()
    {
        var (world, reader, monster) = Rig();
        var other = world.AddEntity(8, 100, true, 0, 0, 0);
        world.AddCast(monster, 5, 1, 0.5, 1);
        var result = reader.Apply([Monster(monster), Monster(other)]);
        Assert.Equal((true, false), (result[0].Cast is not null, result[1].Cast is not null));
    }

    [Fact]
    public void OnlyMonstersAreRead()
    {
        var (world, reader, monster) = Rig();
        world.AddCast(monster, 5, 1, 0.5, 1);
        Assert.Null(reader.Apply([Monster(monster) with { Kind = EntityKind.Npc }])[0].Cast);
    }

    [Fact]
    public void NoFightWorldMeansNoCasts()
    {
        var (_, reader, monster) = Rig();
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Fact]
    public void FreedMemoryDoesNotThrow()
    {
        var (world, reader, monster) = Rig();
        var cast = world.AddCast(monster, 5, 1, 0.5, 1);
        world.Memory.Write(cast.Skill + Offsets.SkillTimer, FakeWorld.Unmapped);
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
        world.Memory.Write(world.FightMgr + Offsets.FightWorldHop1, FakeWorld.Unmapped);
        Assert.Null(reader.Apply([Monster(monster)])[0].Cast);
    }

    [Fact]
    public void ReturnsTheSameArrayWhenNothingCasts()
    {
        var (_, reader, monster) = Rig();
        var entities = new[] { Monster(monster) };
        Assert.Same(entities, reader.Apply(entities));
    }
}
