using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class MonsterRarityReaderTests
{
    private static MonsterRarityReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

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
    public void FindsRowsAcrossCollidingChains()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(3, Enumerable.Range(1, 30).Select(i => ((uint)(i * 3), i % 2 == 0 ? 4u : 1u)).ToArray());
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Boss, reader.Read(6, 0));
        Assert.Equal(Rarity.Normal, reader.Read(3, 0));
        Assert.Equal(Rarity.Boss, reader.Read(90, 0));
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

    [Theory]
    [InlineData(0u)]
    [InlineData(100001u)]
    [InlineData(uint.MaxValue)]
    public void AbsurdBucketCountIsNormal(uint bucketCount)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.MonsterCfgBucketCountRva, bucketCount);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(2200080, 0));
    }

    [Fact]
    public void UnmappedBucketArrayIsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.MonsterCfgBucketsRva, 0x7777_0000_0000L);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(2200080, 0));
    }

    [Fact]
    public void ChainLongerThanTheLimitIsNormalAndCycleIsNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(1, Enumerable.Range(1, Offsets.MaxChain + 1).Select(i => ((uint)i, 4u)).ToArray());
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(1, 0));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read((uint)Offsets.MaxChain + 1, 0));

        world.SetMonsterConfigTable(1, (1, 4), (2, 4));
        world.Memory.Write(world.ConfigNodes[1] + Offsets.NodeNext, world.ConfigNodes[1]);
        Assert.Equal(Rarity.Normal, ReaderFor(world).Read(99, 0));
    }

    [Fact]
    public void UnmappedRowIsNormalAndNotCached()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        world.Memory.Write(world.ConfigNodes[0] + Offsets.NodeEntity, 0x7777_0000_0000L);
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, reader.Read(2200080, 0));
        world.SetMonsterConfigTable(7, (2200080, 2));
        Assert.Equal(Rarity.Rare, reader.Read(2200080, 0));
    }

    private static nint EntityWithRole(FakeWorld world, int? role) => unchecked((nint)world.AddEntity(1, 48, true, 0, 0, 0, role: role).Address);

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
        Assert.Equal(expected, ReaderFor(world).Read(1170006, EntityWithRole(world, role)));
    }

    [Fact]
    public void RoleBeatsTheConfigTierForRareAndNormal()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1171010, 2), (1170001, 1));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(1171010, EntityWithRole(world, 3)));
        Assert.Equal(Rarity.Normal, reader.Read(1171010, EntityWithRole(world, 1)));
        Assert.Equal(Rarity.Magic, reader.Read(1171010, EntityWithRole(world, 2)));
    }

    [Fact]
    public void ConfigTierFourOverridesTheRole()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (190200, 4));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read(190200, EntityWithRole(world, 2)));
    }

    [Fact]
    public void ConfigTierFiveOverridesTheRole()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (555, 5));
        Assert.Equal(Rarity.Boss, ReaderFor(world).Read(555, EntityWithRole(world, 1)));
    }

    [Fact]
    public void ConfigTierThreeIsRareWithoutARoleAndRoleWinsBelowBoss()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (333, 3));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(333, EntityWithRole(world, null)));
        Assert.Equal(Rarity.Magic, reader.Read(333, EntityWithRole(world, 2)));
        Assert.Equal(Rarity.Normal, reader.Read(333, EntityWithRole(world, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ZeroOrNegativeRoleFallsBackToTheConfigTier(int role)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2), (1180001, 1), (333, 3));
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, reader.Read(2200080, EntityWithRole(world, role)));
        Assert.Equal(Rarity.Normal, reader.Read(1180001, EntityWithRole(world, role)));
        Assert.Equal(Rarity.Rare, reader.Read(333, EntityWithRole(world, role)));
    }

    [Fact]
    public void UnreadableRoleBlockFallsBackToTheConfigTier()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (2200080, 2));
        var entity = EntityWithRole(world, 1);
        world.Memory.Write(entity + Offsets.EntityRole, 0x7777_0000_0000L);
        Assert.Equal(Rarity.Rare, ReaderFor(world).Read(2200080, entity));
        Assert.Equal(Rarity.Rare, ReaderFor(world).Read(2200080, EntityWithRole(world, null)));
    }

    [Fact]
    public void RoleStillWorksWithoutAConfigRowAndTheMissIsNotCached()
    {
        var world = new FakeWorld();
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Magic, reader.Read(55, EntityWithRole(world, 2)));
        world.SetMonsterConfigTable(7, (55, 4));
        Assert.Equal(Rarity.Boss, reader.Read(55, EntityWithRole(world, 2)));
        world.BreakMonsterConfigTable();
        Assert.Equal(Rarity.Boss, reader.Read(55, EntityWithRole(world, 2)));
    }
}
