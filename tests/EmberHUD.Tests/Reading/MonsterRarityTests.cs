using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class MonsterRarityTests
{
    private static LogicWorldReader ReaderFor(FakeWorld world) => new(world.Memory, FakeWorld.Module);

    [Fact]
    public void MapsTheKnownTiersAndTreatsUnknownOnesAsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1), (2200080, 2), (333, 3), (190200, 4), (555, 5), (777, 0), (888, 99));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(1180001, 0));
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
        Assert.Equal(Rarity.Rare, reader.Read(333, 0));
        Assert.Equal(Rarity.Boss, reader.Read(190200, 0));
        Assert.Equal(Rarity.Boss, reader.Read(555, 0));
        Assert.Equal(Rarity.Normal, reader.Read(777, 0));
        Assert.Equal(Rarity.Normal, reader.Read(888, 0));
    }

    [Fact]
    public void MissingRowIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1, 1));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(2200080, 0));
        world.SetMonsterConfigTable(7, (1, 1), (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
    }

    [Fact]
    public void FoundRowsAreCachedAndNotReadAgain()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
        world.BreakMonsterConfigTable();
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
    }

    [Fact]
    public void NoTableMeansNormal()
    {
        Assert.Equal(Rarity.Normal, ReaderFor(new FakeWorld()).Read(1, 0));
    }

    [Fact]
    public void AGarbageBucketCountIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        var reader = ReaderFor(world);
        world.Memory.Write(FakeWorld.Module + Offsets.MonsterCfgBucketCountRva, (uint)Offsets.MaxBuckets + 1);
        Assert.Equal(Rarity.Normal, reader.Read(2200080, 0));

        world.SetMonsterConfigTable(7, (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
    }

    [Fact]
    public void UnmappedRowIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(world.ConfigNodes[0] + Offsets.NodeEntity, FakeWorld.Unmapped);
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(2200080, 0));
        world.SetMonsterConfigTable(7, (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
    }

    private static nint RoleBlockWith(FakeWorld world, int? role)
    {
        var entity = world.AddEntity(1, 48, true, 0, 0, 0, role: role).Address;
        return world.Memory.TryReadPtr(entity + Offsets.EntityRole, out var block) ? block : 0;
    }

    [Theory]
    [InlineData(1, Rarity.Normal)]
    [InlineData(2, Rarity.Magic)]
    [InlineData(3, Rarity.Rare)]
    [InlineData(4, Rarity.Boss)]
    [InlineData(9, Rarity.Boss)]
    public void RoleValueIsTheRarityOfThatSpawn(int role, Rarity expected)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1170006, 1));
        Assert.Equal(expected, ReaderFor(world).Read(1170006, RoleBlockWith(world, role)));
    }

    [Fact]
    public void RoleBeatsTheConfigTierForRareAndNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1171010, 2), (1170001, 1));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(1171010, RoleBlockWith(world, 3)));
        Assert.Equal(Rarity.Normal, reader.Read(1171010, RoleBlockWith(world, 1)));
        Assert.Equal(Rarity.Magic, reader.Read(1171010, RoleBlockWith(world, 2)));
    }

    [Fact]
    public void ConfigTierFourOverridesTheRole()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (190200, 4));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read(190200, RoleBlockWith(world, 2)));
    }

    [Fact]
    public void ConfigTierFiveOverridesTheRole()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (555, 5));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read(555, RoleBlockWith(world, 1)));
    }

    [Fact]
    public void ConfigTierThreeIsRareWithoutARoleAndRoleWinsBelowBoss()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (333, 3));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(333, RoleBlockWith(world, null)));
        Assert.Equal(Rarity.Magic, reader.Read(333, RoleBlockWith(world, 2)));
        Assert.Equal(Rarity.Normal, reader.Read(333, RoleBlockWith(world, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ZeroOrNegativeRoleFallsBackToTheConfigTier(int role)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2), (1180001, 1), (333, 3));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(2200080, RoleBlockWith(world, role)));
        Assert.Equal(Rarity.Normal, reader.Read(1180001, RoleBlockWith(world, role)));
        Assert.Equal(Rarity.Rare, reader.Read(333, RoleBlockWith(world, role)));
    }

    [Fact]
    public void UnreadableRoleBlockFallsBackToTheConfigTier()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        Assert.Equal(Rarity.Rare, ReaderFor(world).Read(2200080, FakeWorld.Unmapped));
        Assert.Equal(Rarity.Rare, ReaderFor(world).Read(2200080, RoleBlockWith(world, null)));
    }

    [Fact]
    public void RoleStillWorksWithoutAConfigRowAndTheMissIsNotCached()
    {
        var world = new FakeWorld();
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Magic, reader.Read(55, RoleBlockWith(world, 2)));
        world.SetMonsterConfigTable(7, (55, 4));
        Assert.Equal(Rarity.Boss, reader.Read(55, RoleBlockWith(world, 2)));
        world.BreakMonsterConfigTable();
        Assert.Equal(Rarity.Boss, reader.Read(55, RoleBlockWith(world, 2)));
    }
}

file static class MonsterRarityExtensions
{
    public static Rarity Read(this LogicWorldReader reader, uint configId, nint roleBlock)
    {
        reader.TryGetTier(configId, out var tier);
        return reader.Resolve(tier, roleBlock);
    }
}
