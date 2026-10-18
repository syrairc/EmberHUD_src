using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class EntityVitalsTests
{
    private static (Entity Monster, LogicWorldReader.Vitals Player) Read((double Max, double Cur)? monsterLife, (double Max, double Cur)? life = null, (double Max, double Cur)? shield = null, bool monsterRole = true)
    {
        var world = new FakeWorld();
        var monster = monsterRole ? world.AddEntity(11, 48, true, 1, 2, 0, life: monsterLife) : world.AddEntity(11, 48, true, 1, 2, 0);
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer, life: life, shield: shield), monster);
        world.SetLocalPlayer(10);
        var reader = new LogicWorldReader(world.Memory, FakeWorld.Module);
        Assert.True(reader.TryReadScene(out var scene, out _));
        var entities = reader.ReadWorld(scene, 0).Entities;
        return (Assert.Single(entities), reader.PlayerVitals);
    }

    [Fact]
    public void GoodBlocksReadForMonsterAndPlayer()
    {
        var (monster, player) = Read((165, 137.996), (207, 207), (133, 100));
        Assert.Equal((137.996, 165d), (monster.Life, monster.MaxLife));
        Assert.Equal(new LogicWorldReader.Vitals(207, 207, 100, 133), player);
    }

    [Fact]
    public void CurrentAboveMaxIsClampedWithinTolerance()
    {
        var (monster, _) = Read((100, 100.05));
        Assert.Equal((100d, 100d), (monster.Life, monster.MaxLife));
    }

    [Fact]
    public void TensOfBillionsOfLifeReadForAPlaneBoss()
    {
        var (monster, _) = Read((49120657559.04, 48812850548.61));
        Assert.Equal((48812850548.61, 49120657559.04), (monster.Life, monster.MaxLife));
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(100, -1)]
    [InlineData(100, double.NaN)]
    [InlineData(double.NaN, 5)]
    [InlineData(double.PositiveInfinity, 5)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(1e243, 3e-57)]
    [InlineData(3e-57, 1e243)]
    [InlineData(1e16, 1e16)]
    public void GarbageBlocksReadAsUnknown(double max, double cur)
    {
        var (monster, player) = Read((max, cur), (max, cur), (max, cur));
        Assert.Equal((0d, 0d), (monster.Life, monster.MaxLife));
        Assert.Equal(0, player.Life);
        Assert.Equal(0, player.MaxLife);
    }

    [Fact]
    public void NullRoleReadsAsUnknown()
    {
        var (monster, _) = Read(null, monsterRole: false);
        Assert.Equal((0d, 0d), (monster.Life, monster.MaxLife));
    }

    [Fact]
    public void ZeroShieldMaxMeansNoShield()
    {
        var (_, player) = Read(null, (50, 50), (0, 0));
        Assert.Equal(new LogicWorldReader.Vitals(50, 50, 0, 0), player);
    }

    [Fact]
    public void LiveReaderCopiesPlayerVitals()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer, life: (207, 150), shield: (133, 133)));
        world.SetLocalPlayer(10);
        var reader = new LiveReader(world.Memory, FakeWorld.Module);
        Assert.True(reader.TryRead(out var state, out _));
        Assert.Equal((150d, 207d, 133d, 133d), (state.Player!.Life, state.Player.MaxLife, state.Player.Shield, state.Player.MaxShield));
    }
}
