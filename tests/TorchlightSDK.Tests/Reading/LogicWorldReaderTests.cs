using TorchlightSDK.Host;

namespace TorchlightSDK.Tests.Reading;

public class LogicWorldReaderTests
{
    private static LogicWorldReader ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    private static (Vector3? PlayerPosition, Entity[] Entities) ReadWorld(FakeWorld world)
    {
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        return reader.ReadWorld(scene);
    }

    private static Entity[] Read(FakeWorld world) => ReadWorld(world).Entities;

    private static FakeWorld WorldWithPlayerAndMonster()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer), world.AddEntity(11, 48, true, 1, 2, 0));
        return world;
    }

    [Fact]
    public void ReadsAliveEntitiesWithKeyAsIdAndPosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(41, 1001, true, 10.5, -20.25, 3), world.AddEntity(42, 1002, true, 100, 200, 0));
        var entities = Read(world).OrderBy(e => e.Id).ToArray();
        Assert.Equal(2, entities.Length);
        Assert.Equal(41, entities[0].Id);
        Assert.Equal(new Vector3(10.5f, -20.25f, 3), entities[0].Position);
        Assert.Equal(42, entities[1].Id);
        Assert.All(entities, e => Assert.Equal(Rarity.Normal, e.Rarity));
    }

    [Fact]
    public void KindComesFromTheFightMgrMapTheIdIsIn()
    {
        var world = new FakeWorld();
        (int Map, EntityKind Kind)[] expected =
        [
            (Offsets.FightMapPlayer, EntityKind.Player),
            (Offsets.FightMapMonster, EntityKind.Monster),
            (Offsets.FightMapNpc, EntityKind.Npc),
            (Offsets.FightMapPortal, EntityKind.Portal),
            (Offsets.FightMapInteractive, EntityKind.Interactive),
            (Offsets.FightMapFollower, EntityKind.Ally),
            (Offsets.FightMapServant, EntityKind.Ally),
        ];
        world.PublishEntities(expected.Select((e, i) => world.AddEntity((uint)(i + 1), 5, true, i, i, 0, mapOffset: e.Map)).ToArray());
        var entities = Read(world).OrderBy(e => e.Id).ToArray();
        Assert.Equal(expected.Select(e => e.Kind), entities.Select(e => e.Kind));
    }

    [Fact]
    public void IdsInNoMapAreSkipped()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 5, true, 1, 1, 0, mapOffset: FakeWorld.Unregistered), world.AddEntity(2, 6, true, 2, 2, 0));
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void MonstersUseTheRecordCfgIdAndFallBackToTheEntityOne()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 2200080),
            world.AddEntity(2, 48, true, 2, 2, 0),
            world.AddEntity(3, 77, true, 3, 3, 0, mapOffset: Offsets.FightMapNpc, monsterCfgId: 999));
        var byId = Read(world).ToDictionary(e => e.Id, e => e.CfgId);
        Assert.Equal(2200080, byId[1]);
        Assert.Equal(48, byId[2]);
        Assert.Equal(77, byId[3]);
    }

    [Fact]
    public void WalksEveryChainIncludingCollidingKeys()
    {
        var world = new FakeWorld();
        var entities = Enumerable.Range(1, 10).Select(i => world.AddEntity((uint)i, (uint)(100 + i), true, i, i, 0)).ToArray();
        world.PublishEntities(3, entities);
        Assert.Equal(Enumerable.Range(1, 10), Read(world).Select(e => e.Id).Order());
    }

    [Fact]
    public void KeysAboveIntMaxStillGiveAnId()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(0xFFFF_FFF0, 1, true, 1, 1, 0));
        Assert.Equal(unchecked((int)0xFFFF_FFF0), Assert.Single(Read(world)).Id);
    }

    [Fact]
    public void SkipsDeadEntitiesAndEntitiesWithoutPosition()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 1, true, 1, 1, 0),
            world.AddEntity(2, 2, false, 2, 2, 0),
            world.AddEntity(3, 3, true, 3, 3, 0, hasPosition: false));
        Assert.Equal([1], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void SkipsParkedEntitiesAtHighZ()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 1, true, 1, 1, 8700),
            world.AddEntity(2, 2, true, 2, 2, 8000),
            world.AddEntity(3, 3, true, 3, 3, 7999),
            world.AddEntity(4, 4, true, 4, 4, 0));
        Assert.Equal([3, 4], Read(world).Select(e => e.Id).Order());
    }

    [Fact]
    public void SkipsNonFiniteAndFloatOverflowPositionsButKeepsTheRest()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 1, true, double.NaN, 0, 0),
            world.AddEntity(2, 2, true, 5, double.PositiveInfinity, 0),
            world.AddEntity(3, 3, true, 1e200, 0, 0),
            world.AddEntity(4, 4, true, 0, 0, -1e200),
            world.AddEntity(5, 5, true, 7, 8, 9));
        Assert.Equal([5], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void NullAndUnmappedEntityPointersAreSkipped()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            new FakeEntity(1, 0, Offsets.FightMapMonster, 0x1111),
            world.AddEntity(2, 5, true, 1, 2, 3),
            new FakeEntity(3, 0x7777_0000_0000, Offsets.FightMapMonster, 0x2222));
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void LocalPlayerIsResolvedThroughTheWeakPointerAndExcludedFromEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(10, 52, true, -1463, 617, 5, mapOffset: Offsets.FightMapPlayer), world.AddEntity(11, 48, true, 1, 2, 0));
        world.SetLocalPlayer(10);
        var (position, entities) = ReadWorld(world);
        Assert.Equal(new Vector3(-1463, 617, 5), position);
        Assert.Equal([11], entities.Select(e => e.Id));
    }

    [Fact]
    public void LocalPlayerInASecondGObjectsChunk()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(10, 70000);
        Assert.Equal(new Vector3(1, 2, 3), ReadWorld(world).PlayerPosition);
    }

    [Fact]
    public void OtherPlayersStayInTheList()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer),
            world.AddEntity(12, 52, true, 4, 5, 6, mapOffset: Offsets.FightMapPlayer));
        world.SetLocalPlayer(10);
        var entity = Assert.Single(Read(world));
        Assert.Equal((12, EntityKind.Player), (entity.Id, entity.Kind));
    }

    [Fact]
    public void NoPlayerManagerSetUpMeansNoPlayerAndAllEntities()
    {
        var world = WorldWithPlayerAndMonster();
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal(2, entities.Length);
    }

    [Fact]
    public void NullPlayerMgrGivesNoPlayerButEntities()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(10);
        world.BreakPlayerMgr();
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal(2, entities.Length);
    }

    [Fact]
    public void WrongInternalIndexGivesNoPlayerButEntities()
    {
        var world = WorldWithPlayerAndMonster();
        var (obj, _) = world.SetLocalPlayer(10);
        world.Memory.Write(obj + Offsets.UObjectInternalIndex, 999u);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal(2, entities.Length);
    }

    [Fact]
    public void UnmappedChunkGivesNoPlayerButEntities()
    {
        var world = WorldWithPlayerAndMonster();
        var (_, chunks) = world.SetLocalPlayer(10, 70000);
        world.Memory.Write(chunks + 8, 0x7777_0000_0000L);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal(2, entities.Length);
    }

    [Fact]
    public void PlayerIdMissingFromTheHashMapGivesNoPlayer()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(404);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal(2, entities.Length);
    }

    [Fact]
    public void ATornMonsterMapOnlyDropsThatKind()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0),
            world.AddEntity(2, 5, true, 2, 2, 0, mapOffset: Offsets.FightMapNpc));
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TMapNum, 20001);
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void NoFightMgrMeansNoEntitiesAndNoPlayer()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(10);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.FightMgrGlobalRva, 0L);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Empty(entities);
    }

    [Fact]
    public void ResolvedIdThatIsNotInThePlayerMapGivesNoPlayer()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(11);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Equal([10, 11], entities.Select(e => e.Id).Order());
    }

    [Fact]
    public void ParkedPlayerGivesNoPlayer()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 8700, mapOffset: Offsets.FightMapPlayer));
        world.SetLocalPlayer(10);
        Assert.Null(ReadWorld(world).PlayerPosition);
    }

    [Fact]
    public void NpcsAndPortalsAndInteractivesDoNotNeedTheAliveFlag()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 5, false, 1, 1, 0, mapOffset: Offsets.FightMapNpc),
            world.AddEntity(2, 5, false, 2, 2, 0, mapOffset: Offsets.FightMapPortal),
            world.AddEntity(3, 5, false, 3, 3, 0, mapOffset: Offsets.FightMapInteractive),
            world.AddEntity(4, 5, false, 4, 4, 0, mapOffset: Offsets.FightMapMonster),
            world.AddEntity(5, 5, false, 5, 5, 0, mapOffset: Offsets.FightMapFollower),
            world.AddEntity(6, 5, false, 6, 6, 0, mapOffset: Offsets.FightMapPlayer));
        Assert.Equal([1, 2, 3], Read(world).Select(e => e.Id).Order());
    }

    [Fact]
    public void ZeroRecordCfgIdFallsBackToTheEntityCfgId()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 0));
        Assert.Equal(48, Assert.Single(Read(world)).CfgId);
    }

    [Fact]
    public void TornHashMapKeepsAPlayerAlreadyFoundAndDropsEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(11, 48, true, 1, 2, 0), world.AddEntity(12, 48, true, 1, 2, 0), world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer));
        world.SetLocalPlayer(10);
        world.Memory.Write(world.Nodes[0] + Offsets.NodeNext, 0x7777_0000_0000L);
        var (position, entities) = ReadWorld(world);
        Assert.Equal(new Vector3(1, 2, 3), position);
        Assert.Empty(entities);
    }

    [Fact]
    public void TornHashMapBeforeThePlayerGivesNothing()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer), world.AddEntity(11, 48, true, 1, 2, 0));
        world.SetLocalPlayer(10);
        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, 0x7777_0000_0000L);
        var (position, entities) = ReadWorld(world);
        Assert.Null(position);
        Assert.Empty(entities);
    }

    [Fact]
    public void MonstersGetTheirRarityFromTheConfigTableByRecordCfgId()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1), (2200080, 2), (190200, 4), (48, 4));
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1180001),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 2200080),
            world.AddEntity(3, 48, true, 3, 3, 0, monsterCfgId: 190200),
            world.AddEntity(4, 48, true, 4, 4, 0),
            world.AddEntity(5, 48, true, 5, 5, 0, mapOffset: Offsets.FightMapNpc, monsterCfgId: 190200));
        var rarity = Read(world).ToDictionary(e => e.Id, e => e.Rarity);
        Assert.Equal(Rarity.Normal, rarity[1]);
        Assert.Equal(Rarity.Rare, rarity[2]);
        Assert.Equal(Rarity.Boss, rarity[3]);
        Assert.Equal(Rarity.Normal, rarity[4]);
        Assert.Equal(Rarity.Normal, rarity[5]);
    }

    [Fact]
    public void MonsterRarityComesFromTheRoleBlockAndNotForOtherKinds()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1170006, 1), (190200, 4));
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1170006, role: 1),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 1170006, role: 2),
            world.AddEntity(3, 48, true, 3, 3, 0, monsterCfgId: 1170006, role: 3),
            world.AddEntity(4, 48, true, 4, 4, 0, monsterCfgId: 190200, role: 2),
            world.AddEntity(5, 48, true, 5, 5, 0, mapOffset: Offsets.FightMapNpc, monsterCfgId: 1170006, role: 3));
        var rarity = Read(world).ToDictionary(e => e.Id, e => e.Rarity);
        Assert.Equal([Rarity.Normal, Rarity.Magic, Rarity.Rare, Rarity.Boss, Rarity.Normal], new[] { 1, 2, 3, 4, 5 }.Select(i => rarity[i]));
    }

    [Fact]
    public void BucketCountOfZeroOrNothingPublishedGivesNoEntities()
    {
        var world = new FakeWorld();
        Assert.Empty(Read(world));
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBucketCount, 0u);
        Assert.Empty(Read(world));
    }

    [Theory]
    [InlineData(100001u)]
    [InlineData(uint.MaxValue)]
    public void AbsurdBucketCountGivesNoEntities(uint bucketCount)
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBucketCount, bucketCount);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void UnmappedBucketArrayGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBuckets, 0x7777_0000_0000L);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void ChainLongerThanTheLimitIsTornAndChainAtTheLimitIsFine()
    {
        var world = new FakeWorld();
        FakeEntity[] Make(int count) => Enumerable.Range(1, count).Select(i => world.AddEntity((uint)i, (uint)i, true, i, i, 0)).ToArray();
        world.PublishEntities(1, Make(Offsets.MaxChain));
        Assert.Equal(Offsets.MaxChain, Read(world).Length);
        world.PublishEntities(1, Make(Offsets.MaxChain + 1));
        Assert.Empty(Read(world));
    }

    [Fact]
    public void CyclicChainGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(1, 1, true, 1, 1, 0), world.AddEntity(2, 2, true, 2, 2, 0));
        world.Memory.Write(world.Nodes[0] + Offsets.NodeNext, world.Nodes[0]);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void UnmappedNodeInAChainGivesNoEntities()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(1, 1, true, 1, 1, 0), world.AddEntity(2, 2, true, 2, 2, 0));
        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, 0x7777_0000_0000L);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void MoreNodesThanTheLimitGivesNoEntities()
    {
        var world = new FakeWorld();
        var entities = Enumerable.Range(1, Offsets.MaxEntities + 1).Select(i => world.AddEntity((uint)i, (uint)i, true, i, i, 0)).ToArray();
        world.PublishEntities(512, entities);
        Assert.Empty(Read(world));
        world.PublishEntities(512, entities[..Offsets.MaxEntities]);
        Assert.Equal(Offsets.MaxEntities, Read(world).Length);
    }

    [Fact]
    public void NoWorldMeansAFailureTextNotAnException()
    {
        var world = new FakeWorld();
        world.BreakWorldPointer();
        var reader = ReaderFor(world);
        Assert.False(reader.TryReadScene(out _, out var failure));
        Assert.Contains("world", failure);
    }

    [Fact]
    public void NoSceneMeansNotInAMap()
    {
        var world = new FakeWorld();
        world.BreakScenePointer();
        var reader = ReaderFor(world);
        Assert.False(reader.TryReadScene(out _, out var failure));
        Assert.Contains("map", failure);
    }
}
