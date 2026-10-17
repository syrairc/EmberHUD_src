using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class LogicWorldReaderTests
{
    private static LogicWorldReader ReaderFor(FakeWorld world) => new(world.Memory, FakeWorld.Module);

    private static (Vector3? PlayerPosition, Entity[] Entities) ReadWorld(FakeWorld world)
    {
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        var (playerPosition, entities, _) = reader.ReadWorld(scene, 0);
        return (playerPosition, entities);
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
    public void EntitiesCarryTheirLogicAddress()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(7, 1180000, true, 10, 20, 0);
        world.PublishEntities(monster);

        Assert.Equal(monster.Address, Assert.Single(Read(world)).Address);
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
    public void NpcsUseTheNpcIdFromTheirNpcComponent()
    {
        var world = new FakeWorld();
        var npc = world.AddEntity(1, 51, true, 1, 1, 0, mapOffset: Offsets.FightMapNpc);
        var components = world.AddComponents(npc.Value, "ECfgComponent", Offsets.NpcComponentClassName);
        world.Memory.Write(components[1] + Offsets.NpcComponentNpcId, 4250);
        world.PublishEntities(npc);
        Assert.Equal(4250, Assert.Single(Read(world)).CfgId);
    }

    [Fact]
    public void AlliesUseTheRecordCfgId()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 61, true, 1, 1, 0, mapOffset: Offsets.FightMapServant, monsterCfgId: 5320));
        Assert.Equal(5320, Assert.Single(Read(world)).CfgId);
    }

    [Fact]
    public void ComponentsAreReadOncePerEntity()
    {
        var world = new FakeWorld();
        var npc = world.AddEntity(1, 51, true, 1, 1, 0, mapOffset: Offsets.FightMapNpc);
        var components = world.AddComponents(npc.Value, Offsets.NpcComponentClassName);
        world.Memory.Write(components[0] + Offsets.NpcComponentNpcId, 4250);
        world.PublishEntities(npc);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        reader.ReadWorld(scene, 0);
        world.Memory.Write(npc.Value + Offsets.EntityComponents + Offsets.TArrayNum, 0);
        Assert.Equal(4250, Assert.Single(reader.ReadWorld(scene, 0).Entities).CfgId);
    }

    private static (Entity First, Entity Second) TwoFrames(FakeWorld world, Action between)
    {
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        var first = Assert.Single(reader.ReadWorld(scene, 0).Entities);
        between();
        return (first, Assert.Single(reader.ReadWorld(scene, 0).Entities));
    }

    [Fact]
    public void UnreadableNpcComponentsAreRetriedNextFrame()
    {
        var world = new FakeWorld();
        var npc = world.AddEntity(1, 51, true, 1, 1, 0, mapOffset: Offsets.FightMapNpc);
        var components = world.AddComponents(npc.Value, Offsets.NpcComponentClassName);
        world.Memory.Write(components[0] + Offsets.NpcComponentNpcId, 4250);
        world.PublishEntities(npc);
        world.Memory.TryReadPtr(npc.Value + Offsets.EntityComponents, out var data);
        world.Memory.Write(npc.Value + Offsets.EntityComponents, FakeWorld.Unmapped);

        var (first, second) = TwoFrames(world, () => world.Memory.Write(npc.Value + Offsets.EntityComponents, data));
        Assert.Equal((51, ""), (first.CfgId, first.Name));
        Assert.Equal(4250, second.CfgId);
    }

    [Fact]
    public void UnreadableServantRecordIsRetriedNextFrame()
    {
        var world = new FakeWorld();
        var servant = world.AddEntity(1, 61, true, 1, 1, 0, mapOffset: Offsets.FightMapServant, monsterCfgId: 5320);
        world.PublishEntities(servant);
        world.Memory.TryReadPtr(servant.Value + Offsets.MonsterRecord, out var record);
        world.Memory.Write(servant.Value + Offsets.MonsterRecord, FakeWorld.Unmapped);

        var (first, second) = TwoFrames(world, () => world.Memory.Write(servant.Value + Offsets.MonsterRecord, record));
        Assert.Equal((61, ""), (first.CfgId, first.Name));
        Assert.Equal(5320, second.CfgId);
    }

    [Fact]
    public void ZeroNpcIdIsRejected()
    {
        var world = new FakeWorld();
        var npc = world.AddEntity(1, 51, true, 1, 1, 0, mapOffset: Offsets.FightMapNpc);
        world.AddComponents(npc.Value, Offsets.NpcComponentClassName);
        world.PublishEntities(npc);
        var entity = Assert.Single(Read(world));
        Assert.Equal((51, ""), (entity.CfgId, entity.Name));
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
            new FakeEntity(3, FakeWorld.Unmapped, Offsets.FightMapMonster, 0x2222));
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
        world.Memory.Write(chunks + 8, FakeWorld.Unmapped);
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
        world.Memory.Write(world.FightMgr + Offsets.FightMapMonster + Offsets.TArrayNum, Offsets.MaxMapElements + 1);
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void NoFightMgrMeansNoEntitiesAndNoPlayer()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(10);
        world.Memory.Write(FakeWorld.Module + Offsets.FightMgrGlobalRva, 0L);
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
    public void PreloadedMapEntitiesGetTheirKindFromTheirFightMgrMap()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, (uint)Offsets.SeasonAltarCfg, true, 1, 1, 5620, mapOffset: Offsets.FightMapGamePlay),
            world.AddEntity(2, 40, true, 2, 2, 0, mapOffset: Offsets.FightMapElevator),
            world.AddEntity(3, 36, true, 3, 3, 0, mapOffset: Offsets.FightMapObject),
            world.AddEntity(4, 45, true, 4, 4, 0, mapOffset: Offsets.FightMapObject),
            world.AddEntity(5, 39, true, 5, 5, 0, mapOffset: Offsets.FightMapDestructible));
        var kinds = Read(world).ToDictionary(e => e.Id, e => e.Kind);
        Assert.Equal(EntityKind.Altar, kinds[1]);
        Assert.Equal(EntityKind.Elevator, kinds[2]);
        Assert.Equal(EntityKind.MapObject, kinds[3]);
        Assert.Equal(EntityKind.MapObject, kinds[4]);
        Assert.Equal(EntityKind.Destructible, kinds[5]);
    }

    [Fact]
    public void OnlySeasonAltarsComeFromTheGamePlayMap()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 10, true, 0, 0, 0, mapOffset: Offsets.FightMapGamePlay),
            world.AddEntity(2, (uint)Offsets.SeasonAltarCfg, true, 2, 2, 5620, mapOffset: Offsets.FightMapGamePlay));
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void TheVigilCoffinIsFoundByCfgIdOutsideEveryKindMap()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, (uint)Offsets.VigilCoffinCfg, true, 1, 2, 5000, mapOffset: FakeWorld.Unregistered),
            world.AddEntity(2, 66, true, 2, 2, 0, mapOffset: FakeWorld.Unregistered));
        var coffin = Assert.Single(Read(world));
        Assert.Equal(1, coffin.Id);
        Assert.Equal(EntityKind.Coffin, coffin.Kind);
        Assert.Equal(new Vector3(1, 2, 5000), coffin.Position);
    }

    [Fact]
    public void CoffinsAndDestructiblesNeedTheAliveFlagAndTheOtherMapEntitiesDoNot()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, (uint)Offsets.SeasonAltarCfg, false, 1, 1, 0, mapOffset: Offsets.FightMapGamePlay),
            world.AddEntity(2, 40, false, 2, 2, 0, mapOffset: Offsets.FightMapElevator),
            world.AddEntity(3, 36, false, 3, 3, 0, mapOffset: Offsets.FightMapObject),
            world.AddEntity(4, 39, false, 4, 4, 0, mapOffset: Offsets.FightMapDestructible),
            world.AddEntity(5, (uint)Offsets.VigilCoffinCfg, false, 5, 5, 0, mapOffset: FakeWorld.Unregistered));
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
        world.Memory.Write(world.Nodes[0] + Offsets.NodeNext, FakeWorld.Unmapped);
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
        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, FakeWorld.Unmapped);
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
    public void MonstersWhoseConfigRowIsFriendlyAreMarkedFriendly()
    {
        var world = new FakeWorld();
        world.FriendlyConfigIds.Add(11305);
        world.SetMonsterConfigTable(7, (11305, 1), (1180001, 1));
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 11305, role: 1),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 1180001, role: 1),
            world.AddEntity(3, 48, true, 3, 3, 0, monsterCfgId: 777, role: 1));
        var reader = ReaderFor(world);
        foreach (var _ in new[] { 1, 2 })
            Assert.Equal([true, false, false], Tick(reader).OrderBy(e => e.Id).Select(e => e.Friendly));
    }

    [Fact]
    public void ConfigFriendlyMonsterInTheHostileCampIsNotFriendly()
    {
        var world = new FakeWorld();
        world.FriendlyConfigIds.Add(11305);
        world.SetMonsterConfigTable(7, (11305, 1));
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 11305, role: 1),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 11305, role: 1));
        world.AddSideFighter(1, 1);
        world.AddSideFighter(2, 2);
        var reader = ReaderFor(world);
        Assert.Equal([false, true], Tick(reader).OrderBy(e => e.Id).Select(e => e.Friendly));
    }

    [Fact]
    public void MonstersInTheInvulnerableListAreMarkedInvulnerable()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 2191020, role: 1),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 2191030, role: 1),
            world.AddEntity(3, 48, true, 3, 3, 0, monsterCfgId: 1180001, role: 1),
            world.AddEntity(4, 2191020, true, 4, 4, 0, mapOffset: Offsets.FightMapNpc));
        var reader = ReaderFor(world);
        Assert.Equal([true, true, false, false], Tick(reader).OrderBy(e => e.Id).Select(e => e.Invulnerable));
    }

    [Fact]
    public void MonstersWithTheActorHiddenBitAreMarkedHidden()
    {
        var world = new FakeWorld();
        world.PublishEntities(
            world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1180001, role: 1, hidden: true),
            world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 1180001, role: 1),
            world.AddEntity(3, 48, true, 3, 3, 0, mapOffset: Offsets.FightMapNpc, hidden: true));
        var reader = ReaderFor(world);
        foreach (var _ in new[] { 1, 2 })
            Assert.Equal([true, false, false], Tick(reader).OrderBy(e => e.Id).Select(e => e.Hidden));
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
    public void AGarbageBucketTableGivesNoEntities()
    {
        var world = new FakeWorld();
        Assert.Empty(Read(world));
        world.PublishEntities(world.AddEntity(1, 9, true, 1, 1, 1));
        world.Memory.Write(world.Scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, (uint)Offsets.MaxBuckets + 1);
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
        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, FakeWorld.Unmapped);
        Assert.Empty(Read(world));
    }

    [Fact]
    public void EachSceneNodeIsOneReadOfTheNodeSize()
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(1, 5, true, 1, 1, 0), world.AddEntity(2, 6, true, 2, 2, 0), world.AddEntity(3, 7, true, 3, 3, 0));
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        world.Memory.Reads.Clear();

        Assert.Equal(3, reader.ReadWorld(scene, 0).Entities.Length);

        Assert.Equal(3, world.Nodes.Count);
        foreach (var node in world.Nodes)
        {
            var at = node;
            var read = Assert.Single(world.Memory.Reads, r => r.Address >= at && r.Address < at + Offsets.NodeSize);
            Assert.Equal((at, Offsets.NodeSize), read);
        }
    }

    [Fact]
    public void AnEntityIsOneHeaderReadAndOnePositionRead()
    {
        var world = new FakeWorld();
        var npc = world.AddEntity(1, 5, true, 1, 2, 3, mapOffset: Offsets.FightMapNpc);
        world.PublishEntities(npc);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        world.Memory.Reads.Clear();

        Assert.Equal(new Vector3(1, 2, 3), Assert.Single(reader.ReadWorld(scene, 0).Entities).Position);

        var entity = npc.Address;
        Assert.Equal([(entity, Offsets.EntityHeaderSize)], world.Memory.Reads.Where(r => r.Address >= entity && r.Address < entity + 0x400).ToArray());
        Assert.True(world.Memory.TryReadPtr(entity + Offsets.EntityPosition, out var block));
        Assert.Single(world.Memory.Reads, r => r.Address == block + Offsets.PositionX && r.Length == 3 * sizeof(double));
    }

    [Fact]
    public void AnEntityWhoseHeaderRunsOffItsMappedMemoryIsSkippedAndTheRestStillRead()
    {
        var world = new FakeWorld();
        var truncated = world.Alloc(Offsets.EntityHeaderSize - 1);
        world.PublishEntities(new FakeEntity(1, truncated, Offsets.FightMapMonster, 0x1111), world.AddEntity(2, 5, true, 1, 2, 3));
        Assert.Equal([2], Read(world).Select(e => e.Id));
    }

    [Fact]
    public void EachFightMapCostsOneHeaderRead()
    {
        var world = new FakeWorld();
        world.PublishEntities();
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        world.Memory.Reads.Clear();

        reader.ReadWorld(scene, 0);

        var fightMgr = world.FightMgr;
        var inFightMgr = world.Memory.Reads.Where(r => r.Address >= fightMgr && r.Address < fightMgr + 0x1000).ToArray();
        Assert.Equal(11, inFightMgr.Length);
        Assert.All(inFightMgr, r => Assert.Equal(Offsets.TMapHeaderSize, r.Length));
    }

    private static Entity[] Tick(LogicWorldReader reader, int generation = 1)
    {
        Assert.True(reader.TryReadScene(out var scene, out _));
        return reader.ReadWorld(scene, generation).Entities;
    }

    private static void SetRecordCfg(FakeWorld world, FakeEntity monster, uint cfgId)
    {
        Assert.True(world.Memory.TryReadPtr(monster.Value + Offsets.MonsterRecord, out var record));
        world.Memory.Write(record + Offsets.MonsterRecordCfgId, cfgId);
    }

    private static (FakeWorld World, FakeEntity Monster, LogicWorldReader Reader) KnownMonster()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1), (2200080, 1));
        var monster = world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1180001, role: 1);
        world.PublishEntities(monster);
        var reader = ReaderFor(world);
        Assert.Equal(1180001, Assert.Single(Tick(reader)).CfgId);
        return (world, monster, reader);
    }

    private static int SecondTickReads(int monsters)
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1));
        world.PublishEntities(Enumerable.Range(1, monsters).Select(i => world.AddEntity((uint)i, 48, true, i, i, 0, monsterCfgId: 1180001, role: 1)).ToArray());
        var reader = ReaderFor(world);
        Assert.Equal(monsters, Tick(reader).Length);
        world.Memory.Reads.Clear();
        Assert.Equal(monsters, Tick(reader).Length);
        return world.Memory.Reads.Count;
    }

    [Fact]
    public void AKnownMonsterCostsItsNodeHeaderPositionActorRootRoleLifeAndHiddenBit()
    {
        Assert.Equal(7 * 10, SecondTickReads(11) - SecondTickReads(1));
    }

    [Fact]
    public void AKnownMonsterReadsTheSameOnTheSecondTick()
    {
        var (_, monster, reader) = KnownMonster();
        Assert.Equal(new Entity(1, 1180001, new Vector3(1, 1, 0), EntityKind.Monster, Rarity.Normal) { Address = monster.Address }, Assert.Single(Tick(reader)));
    }

    [Fact]
    public void AGenerationChangeForgetsKnownMonsters()
    {
        var (world, monster, reader) = KnownMonster();
        SetRecordCfg(world, monster, 2200080);
        Assert.Equal(1180001, Assert.Single(Tick(reader)).CfgId);
        Assert.Equal(2200080, Assert.Single(Tick(reader, 2)).CfgId);
    }

    [Fact]
    public void AMonsterAtAnotherEntityAddressIsReadAgain()
    {
        var (world, monster, reader) = KnownMonster();
        var replacement = world.AddEntity(1, 48, true, 1, 1, 0);
        Assert.True(world.Memory.TryReadPtr(monster.Address + Offsets.EntityRole, out var role));
        world.Memory.Write(replacement.Address + Offsets.EntityRole, (long)role);
        SetRecordCfg(world, monster, 2200080);
        world.PublishEntities(new FakeEntity(1, replacement.Address, Offsets.FightMapMonster, monster.Value));
        Assert.Equal(2200080, Assert.Single(Tick(reader)).CfgId);
    }

    [Fact]
    public void AMonsterWithAnotherLogicObjectIsReadAgain()
    {
        var (world, monster, reader) = KnownMonster();
        var other = world.AddEntity(2, 48, true, 1, 1, 0, monsterCfgId: 2200080);
        world.PublishEntities(new FakeEntity(1, monster.Address, Offsets.FightMapMonster, other.Value));
        Assert.Equal(2200080, Assert.Single(Tick(reader)).CfgId);
    }

    [Fact]
    public void ASwappedRoleBlockIsReadAgain()
    {
        var (world, monster, reader) = KnownMonster();
        var swapped = world.Alloc(0x80);
        world.Memory.Write(swapped + Offsets.RoleRarity, 3);
        world.Memory.Write(monster.Address + Offsets.EntityRole, swapped);
        Assert.Equal(Rarity.Rare, Assert.Single(Tick(reader)).Rarity);
    }

    [Fact]
    public void AMonsterUpgradedInPlaceShowsItsNewRarityOnTheNextTick()
    {
        var (world, monster, reader) = KnownMonster();
        Assert.True(world.Memory.TryReadPtr(monster.Address + Offsets.EntityRole, out var role));
        Assert.Equal(Rarity.Normal, Assert.Single(Tick(reader)).Rarity);

        world.Memory.Write(role + Offsets.RoleRarity, 2);

        Assert.Equal(Rarity.Magic, Assert.Single(Tick(reader)).Rarity);
    }

    [Fact]
    public void AnAddressTakenByAnotherIdIsReadAgain()
    {
        var (world, monster, reader) = KnownMonster();
        var other = world.AddEntity(2, 48, true, 1, 1, 0, monsterCfgId: 2200080, role: 1);
        world.PublishEntities(new FakeEntity(2, monster.Address, Offsets.FightMapMonster, other.Value));
        var entity = Assert.Single(Tick(reader));
        Assert.Equal((2, 2200080), (entity.Id, entity.CfgId));
    }

    [Fact]
    public void AMonsterThatLeftTheSceneIsForgotten()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1180001, 1), (2200080, 1));
        var monster = world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1180001, role: 1);
        var other = world.AddEntity(2, 48, true, 2, 2, 0, monsterCfgId: 1180001, role: 1);
        world.PublishEntities(monster, other);
        var reader = ReaderFor(world);
        Tick(reader);
        world.PublishEntities(other);
        Tick(reader);

        SetRecordCfg(world, monster, 2200080);
        world.PublishEntities(monster, other);

        Assert.Equal(2200080, Tick(reader).Single(e => e.Id == 1).CfgId);
    }

    private static (FakeWorld World, LogicWorldReader Reader, nint TearAt) TwoEntitiesInOneChain()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(1, 5, true, 1, 1, 0), world.AddEntity(2, 6, true, 2, 2, 0));
        var reader = ReaderFor(world);
        Assert.Equal([1, 2], Tick(reader).Select(e => e.Id).Order());
        return (world, reader, world.Nodes[1] + Offsets.NodeNext);
    }

    [Fact]
    public void ATornWalkReusesThePreviousTicksEntities()
    {
        var (world, reader, tearAt) = TwoEntitiesInOneChain();
        world.Memory.Write(tearAt, FakeWorld.Unmapped);

        Assert.Equal([1, 2], Tick(reader).Select(e => e.Id).Order());
    }

    [Fact]
    public void TheReuseEndsAfterTheLimitAndAWholeWalkStartsItOver()
    {
        var (world, reader, tearAt) = TwoEntitiesInOneChain();
        Assert.True(world.Memory.TryRead(tearAt, out nint original));
        world.Memory.Write(tearAt, FakeWorld.Unmapped);

        for (var i = 0; i < Offsets.MaxTornReuseTicks; i++)
            Assert.Equal(2, Tick(reader).Length);
        Assert.Empty(Tick(reader));

        world.Memory.Write(tearAt, original);
        Assert.Equal(2, Tick(reader).Length);

        world.Memory.Write(tearAt, FakeWorld.Unmapped);
        Assert.Equal(2, Tick(reader).Length);
    }

    [Fact]
    public void AGenerationChangeNeverReusesTheOldArea()
    {
        var (world, reader, tearAt) = TwoEntitiesInOneChain();
        world.Memory.Write(tearAt, FakeWorld.Unmapped);

        Assert.Empty(Tick(reader, 2));
    }

    [Fact]
    public void ATornWalkRightAfterASceneChangeNeverShowsTheOldScene()
    {
        var (_, reader, _) = TwoEntitiesInOneChain();
        Assert.True(reader.TryReadScene(out var scene, out _));
        var tornScene = unchecked((nint)0x5555_0000_0000L);

        Assert.Empty(reader.ReadWorld(tornScene, 1).Entities);
        Assert.Empty(reader.ReadWorld(tornScene, 1).Entities);
        Assert.Equal(2, reader.ReadWorld(scene, 1).Entities.Length);
    }

    [Fact]
    public void ATornWalkKeepsThePlayerOfThePreviousTick()
    {
        var world = new FakeWorld();
        world.PublishEntities(1, world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer), world.AddEntity(11, 48, true, 1, 2, 0));
        world.SetLocalPlayer(10);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        Assert.Equal(new Vector3(1, 2, 3), reader.ReadWorld(scene, 1).PlayerPosition);

        world.Memory.Write(world.Nodes[1] + Offsets.NodeNext, FakeWorld.Unmapped);

        Assert.Equal(new Vector3(1, 2, 3), reader.ReadWorld(scene, 1).PlayerPosition);
    }

    [Fact]
    public void AKnownMonsterWhoseRoleWasEmptyShowsTheRoleOnceItIsWritten()
    {
        var world = new FakeWorld();
        world.SetMonsterConfigTable(7, (1170006, 2));
        var monster = world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 1170006, role: 0);
        world.PublishEntities(monster);
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Rare, Assert.Single(Tick(reader)).Rarity);

        Assert.True(world.Memory.TryReadPtr(monster.Address + Offsets.EntityRole, out var role));
        world.Memory.Write(role + Offsets.RoleRarity, 2);

        Assert.Equal(Rarity.Magic, Assert.Single(Tick(reader)).Rarity);
    }

    [Fact]
    public void AMonsterWhoseConfigRowIsMissingIsNotCached()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(1, 48, true, 1, 1, 0, monsterCfgId: 190200, role: 1);
        world.PublishEntities(monster);
        var reader = ReaderFor(world);
        Assert.Equal(Rarity.Normal, Assert.Single(Tick(reader)).Rarity);

        world.SetMonsterConfigTable(7, (190200, 4));

        Assert.Equal(Rarity.Boss, Assert.Single(Tick(reader)).Rarity);
    }

    [Fact]
    public void AnUnmappedNodeWhoseCfgIdIsNotWrittenYetIsNotCached()
    {
        var world = new FakeWorld();
        var node = world.AddEntity(9, 0, true, 1, 2, 5000, mapOffset: FakeWorld.Unregistered);
        world.PublishEntities(node);
        var reader = ReaderFor(world);
        Assert.Empty(Tick(reader));

        world.Memory.Write(node.Address + Offsets.EntityCfgId, (uint)Offsets.VigilCoffinCfg);

        Assert.Equal(9, Assert.Single(Tick(reader)).Id);
    }

    [Fact]
    public void AnUnmappedNonCoffinIsNotReadAgainButTheCoffinStaysLive()
    {
        var world = new FakeWorld();
        var junk = Enumerable.Range(1, 5).Select(i => world.AddEntity((uint)i, 66, true, i, i, 0, mapOffset: FakeWorld.Unregistered)).ToArray();
        var coffin = world.AddEntity(9, (uint)Offsets.VigilCoffinCfg, true, 1, 2, 5000, mapOffset: FakeWorld.Unregistered);
        world.PublishEntities(junk.Append(coffin).ToArray());
        var reader = ReaderFor(world);
        Assert.Equal(new Vector3(1, 2, 5000), Assert.Single(Tick(reader)).Position);

        world.MoveEntity(coffin, 7, 8, 5000);
        world.Memory.Reads.Clear();

        Assert.Equal(new Vector3(7, 8, 5000), Assert.Single(Tick(reader)).Position);
        foreach (var node in junk)
            Assert.DoesNotContain(world.Memory.Reads, r => r.Address >= node.Address && r.Address < node.Address + 0x400);
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

    [Fact]
    public void AnEntityInNoKindMapCanStillBeFoundByIdForItsPosition()
    {
        var world = new FakeWorld();
        var vigil = world.AddEntity(72, 67, true, -625.5, -24197, 5000, mapOffset: FakeWorld.Unregistered);
        world.PublishEntities(4, world.AddEntity(5, 77, true, 1, 2, 0), vigil);
        var reader = ReaderFor(world);

        Assert.True(reader.TryReadEntityPosition(world.Scene, 72, out var at));
        Assert.Equal(new System.Numerics.Vector3(-625.5f, -24197, 5000), at);
        Assert.False(reader.TryReadEntityPosition(world.Scene, 76, out _));
    }

    private static SceneEntity[] ListScene(FakeWorld world) =>
        ReaderFor(world).ListScene(world.Scene, new Names(world.Memory, FakeWorld.Module));

    [Fact]
    public void ListSceneListsEveryEntityWithKindAndClassWhenItHasThem()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(1, 48, true, 1, 2, 3, monsterCfgId: 1180001);
        var loose = world.AddEntity(2, 66, false, 0, 0, 0, hasPosition: false, mapOffset: FakeWorld.Unregistered);
        var statue = world.AddEntity(3, 77, false, 3, 3, 0, mapOffset: Offsets.FightMapGamePlay);
        var altar = world.AddEntity(4, (uint)Offsets.SeasonAltarCfg, true, 4, 4, 9000, mapOffset: Offsets.FightMapGamePlay);
        var coffin = world.AddEntity(5, (uint)Offsets.VigilCoffinCfg, true, 5, 5, 0, mapOffset: FakeWorld.Unregistered);
        world.Memory.Write(statue.Value + Offsets.UObjectClass, world.AddClass("EMapGodOfWar"));
        world.PublishEntities(monster, loose, statue, altar, coffin);

        var listed = ListScene(world).ToDictionary(e => e.Id);

        Assert.Equal(5, listed.Count);
        Assert.Equal(new SceneEntity(1, 1180001, new Vector3(1, 2, 3), true, EntityKind.Monster, "") { Address = monster.Address }, listed[1]);
        Assert.Equal(new SceneEntity(2, 66, null, false, null, "") { Address = loose.Address }, listed[2]);
        Assert.Equal(new SceneEntity(3, 77, new Vector3(3, 3, 0), false, null, "EMapGodOfWar") { Address = statue.Address }, listed[3]);
        Assert.Equal((EntityKind.Altar, new Vector3(4, 4, 9000)), (listed[4].Kind, listed[4].Position!.Value));
        Assert.Equal(EntityKind.Coffin, listed[5].Kind);
    }

    [Fact]
    public void ListSceneKeepsTheLocalPlayer()
    {
        var world = WorldWithPlayerAndMonster();
        world.SetLocalPlayer(10);
        Assert.Equal(EntityKind.Player, ListScene(world).Single(e => e.Id == 10).Kind);
    }

    [Fact]
    public void ListSceneOfATornHashMapIsEmpty()
    {
        var world = WorldWithPlayerAndMonster();
        world.Memory.Write(world.Nodes[0] + Offsets.NodeNext, FakeWorld.Unmapped);
        Assert.Empty(ListScene(world));
    }

    private static (FakeWorld World, nint Pawn, nint Manager) CameraChain()
    {
        var world = new FakeWorld();
        var pawn = world.Alloc(0x400);
        var controller = world.Alloc(0x400);
        var manager = world.Alloc(0x2000);
        world.Memory.Write(pawn + Offsets.PawnController, controller);
        world.Memory.Write(controller + Offsets.ControllerCameraManager, manager);
        world.Memory.WriteArray(manager + Offsets.CameraCache + Offsets.PovLocation, new[] { 1f, 2f, 3f });
        world.Memory.WriteArray(manager + Offsets.CameraCache + Offsets.PovRotation, new[] { -45f, 90f, 0f });
        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 60f);
        return (world, pawn, manager);
    }

    [Fact]
    public void ReadCameraReadsThePovThroughTheControllerAndCameraManager()
    {
        var (world, pawn, _) = CameraChain();

        Assert.Equal(new Camera(new Vector3(1, 2, 3), new Vector3(-45, 90, 0), 60), ReaderFor(world).ReadCamera(pawn));
    }

    [Fact]
    public void ReadCameraNeedsTheWholeChain()
    {
        var (world, pawn, manager) = CameraChain();
        var reader = ReaderFor(world);

        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 0f);
        Assert.Null(reader.ReadCamera(pawn));

        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, float.NaN);
        Assert.Null(reader.ReadCamera(pawn));

        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 60f);
        world.Memory.WriteArray(manager + Offsets.CameraCache + Offsets.PovRotation, new[] { float.NaN, 0f, 0f });
        Assert.Null(reader.ReadCamera(pawn));

        world.Memory.Write(pawn + Offsets.PawnController, 0L);
        Assert.Null(reader.ReadCamera(pawn));
    }

    [Fact]
    public void ThePovIsOneBlockRead()
    {
        var (world, pawn, manager) = CameraChain();
        var reader = ReaderFor(world);
        world.Memory.Reads.Clear();

        reader.ReadCamera(pawn);

        Assert.Single(world.Memory.Reads, r => r.Address >= manager && r.Address < manager + 0x2000);
        Assert.Contains((manager + Offsets.CameraCache, Offsets.CameraPovSize), world.Memory.Reads);
    }

    private static (FakeWorld World, LogicWorldReader Reader, nint Scene, nint Pawn) WorldWithRootZ(float rootZ)
    {
        var world = new FakeWorld();
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer), world.AddEntity(11, 48, true, 1, 2, 0), world.AddEntity(12, 48, true, 1, 2, 7999), world.AddEntity(13, 48, true, 1, 2, 8000));
        var (pawn, _) = world.SetLocalPlayer(10);
        var root = world.Alloc(0x200);
        world.Memory.Write(pawn + Offsets.PawnRootComponent, root);
        world.Memory.Write(root + Offsets.RootRelativeZ, rootZ);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));
        return (world, reader, scene, pawn);
    }

    [Fact]
    public void ReadWorldLiftsThePlayerAndEntitiesByTheRootComponentHeight()
    {
        var (_, reader, scene, _) = WorldWithRootZ(1010);

        var (player, entities, _) = reader.ReadWorld(scene, 0);

        Assert.Equal(1010f, player!.Value.Z);
        Assert.Equal(1007f, entities.Single(e => e.Id == 11).Position.Z);
    }

    [Fact]
    public void TheParkedCheckStaysOnTheRawLogicZ()
    {
        var (_, reader, scene, _) = WorldWithRootZ(1010);

        var entities = reader.ReadWorld(scene, 0).Entities;

        Assert.Contains(entities, e => e.Id == 12);
        Assert.DoesNotContain(entities, e => e.Id == 13);
    }

    [Fact]
    public void EntityPositionLookupsAreLiftedToo()
    {
        var (_, reader, scene, _) = WorldWithRootZ(1010);
        reader.ReadWorld(scene, 0);

        Assert.True(reader.TryReadEntityPosition(scene, 11, out var position));
        Assert.Equal(1007f, position.Z);
    }

    [Fact]
    public void AMonsterWithAnActorUsesTheActorsWorldZNotItsLogicZ()
    {
        var world = new FakeWorld();
        var monster = world.AddEntity(11, 48, true, 1, 2, 1335);
        var other = world.AddEntity(12, 48, true, 3, 4, 0);
        world.PublishEntities(world.AddEntity(10, 52, true, 1, 2, 3, mapOffset: Offsets.FightMapPlayer), monster, other);
        var (pawn, _) = world.SetLocalPlayer(10);
        var playerRoot = world.Alloc(0x200);
        world.Memory.Write(pawn + Offsets.PawnRootComponent, playerRoot);
        world.Memory.Write(playerRoot + Offsets.RootRelativeZ, 1010f);
        var monsterRoot = world.Alloc(0x200);
        world.Memory.Write(monster.Value + Offsets.PawnRootComponent, monsterRoot);
        world.Memory.Write(monsterRoot + Offsets.RootRelativeZ, 1010f);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));

        var entities = reader.ReadWorld(scene, 0).Entities;

        Assert.Equal(1010f, entities.Single(e => e.Id == 11).Position.Z);
        Assert.Equal(1007f, entities.Single(e => e.Id == 12).Position.Z);
    }

    [Fact]
    public void WithoutARootComponentPositionsStayRaw()
    {
        var (world, reader, scene, pawn) = WorldWithRootZ(1010);
        world.Memory.Write(pawn + Offsets.PawnRootComponent, 0L);

        var (player, entities, _) = reader.ReadWorld(scene, 0);

        Assert.Equal(3f, player!.Value.Z);
        Assert.Equal(0f, entities.Single(e => e.Id == 11).Position.Z);
    }

    [Fact]
    public void ReadWorldReturnsTheLocalPawnAndZeroWithoutOne()
    {
        var world = WorldWithPlayerAndMonster();
        var (pawn, _) = world.SetLocalPlayer(10);
        var reader = ReaderFor(world);
        Assert.True(reader.TryReadScene(out var scene, out _));

        Assert.Equal(pawn, reader.ReadWorld(scene, 0).Pawn);

        world.BreakPlayerMgr();
        Assert.Equal((nint)0, reader.ReadWorld(scene, 0).Pawn);
    }
}
