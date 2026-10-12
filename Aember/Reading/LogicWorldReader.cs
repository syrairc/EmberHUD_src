using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class LogicWorldReader(IReadBackend memory, nint moduleBase)
{
    private readonly FightMapReader _maps = new(memory);
    private readonly MonsterRarityReader _rarity = new(memory, moduleBase);

    public bool TryReadScene(out nint scene, out string? failure)
    {
        scene = 0;
        if (!memory.TryReadPtr(moduleBase + Offsets.LogicWorldGlobalRva, out var world))
        {
            failure = "logic world not created yet";
            return false;
        }

        if (!memory.TryReadPtr(world + Offsets.WorldScene, out scene))
        {
            failure = "no scene - not in a map";
            return false;
        }

        failure = null;
        return true;
    }

    public (Vector3? PlayerPosition, Entity[] Entities) ReadWorld(nint scene)
    {
        if (!memory.TryRead(scene + Offsets.SceneEntityBucketCount, out uint bucketCount) ||
            bucketCount == 0 || bucketCount > Offsets.MaxBuckets ||
            !memory.TryReadPtr(scene + Offsets.SceneEntityBuckets, out var bucketArray) ||
            !memory.TryReadArray(bucketArray, (int)bucketCount, out nint[] buckets))
            return (null, []);

        var kinds = ReadKinds();
        var localPlayerId = TryReadLocalPlayerId(out var id) && kinds.TryGetValue(id, out var playerKind) && playerKind.Kind == EntityKind.Player ? id : (int?)null;
        Vector3? playerPosition = null;
        var entities = new List<Entity>();
        var nodeCount = 0;
        foreach (var head in buckets)
        {
            var chainLength = 0;
            for (var node = head; node != 0; )
            {
                if (++chainLength > Offsets.MaxChain || ++nodeCount > Offsets.MaxEntities ||
                    !memory.TryRead(node + Offsets.NodeKey, out uint key) ||
                    !memory.TryRead(node + Offsets.NodeEntity, out nint entity) ||
                    !memory.TryRead(node + Offsets.NodeNext, out nint next))
                    return (playerPosition, []);

                var entityId = unchecked((int)key);
                if (entityId == localPlayerId)
                    playerPosition = TryReadPosition(entity, out var position) && position.Z < Offsets.ParkedZ ? position : playerPosition;
                else if (kinds.TryGetValue(entityId, out var kind) && TryReadEntity(entity, entityId, kind.Kind, kind.Value, out var result))
                    entities.Add(result);
                else if (!kinds.ContainsKey(entityId) && memory.TryRead(entity + Offsets.EntityCfgId, out uint cfgId) && cfgId == Offsets.VigilCoffinCfg &&
                         TryReadEntity(entity, entityId, EntityKind.Coffin, 0, out var coffin))
                    entities.Add(coffin);
                node = next;
            }
        }

        return (playerPosition, [.. entities]);
    }

    public bool TryReadEntityPosition(nint scene, int id, out Vector3 position)
    {
        position = default;
        if (!memory.TryRead(scene + Offsets.SceneEntityBucketCount, out uint bucketCount) ||
            bucketCount == 0 || bucketCount > Offsets.MaxBuckets ||
            !memory.TryReadPtr(scene + Offsets.SceneEntityBuckets, out var bucketArray) ||
            !memory.TryRead(bucketArray + (nint)(unchecked((uint)id) % bucketCount) * nint.Size, out nint node))
            return false;

        for (var chainLength = 0; node != 0 && chainLength < Offsets.MaxChain; chainLength++)
        {
            if (!memory.TryRead(node + Offsets.NodeKey, out uint key) || !memory.TryRead(node + Offsets.NodeNext, out nint next))
                return false;
            if (unchecked((int)key) == id)
                return memory.TryRead(node + Offsets.NodeEntity, out nint entity) && TryReadPosition(entity, out position);
            node = next;
        }

        return false;
    }

    private Dictionary<int, (EntityKind Kind, nint Value)> ReadKinds()
    {
        var kinds = new Dictionary<int, (EntityKind, nint)>();
        if (!memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr))
            return kinds;

        (int Map, EntityKind Kind)[] maps =
        [
            (Offsets.FightMapPlayer, EntityKind.Player),
            (Offsets.FightMapMonster, EntityKind.Monster),
            (Offsets.FightMapNpc, EntityKind.Npc),
            (Offsets.FightMapPortal, EntityKind.Portal),
            (Offsets.FightMapInteractive, EntityKind.Interactive),
            (Offsets.FightMapFollower, EntityKind.Ally),
            (Offsets.FightMapServant, EntityKind.Ally),
            (Offsets.FightMapGamePlay, EntityKind.Altar),
            (Offsets.FightMapElevator, EntityKind.Elevator),
            (Offsets.FightMapObject, EntityKind.MapObject),
            (Offsets.FightMapDestructible, EntityKind.Destructible),
        ];
        foreach (var (map, kind) in maps)
            foreach (var (key, value) in _maps.Read(fightMgr + map))
                kinds[key] = (kind, value);
        return kinds;
    }

    public bool TryReadLocalPawn(out nint pawn)
    {
        pawn = 0;
        if (!memory.TryReadPtr(moduleBase + Offsets.PlayerMgrGlobalRva, out var playerMgr) ||
            !memory.TryRead(playerMgr + Offsets.PlayerMgrEntity, out uint index) ||
            !memory.TryReadPtr(moduleBase + Offsets.GObjectsGlobalRva, out var chunks) ||
            !memory.TryReadPtr(chunks + (nint)(index / Offsets.GObjectsPerChunk) * 8, out var chunk) ||
            !memory.TryReadPtr(chunk + (nint)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize, out var obj) ||
            !memory.TryRead(obj + Offsets.UObjectInternalIndex, out uint internalIndex) || internalIndex != index)
            return false;

        pawn = obj;
        return true;
    }

    private bool TryReadLocalPlayerId(out int id)
    {
        id = 0;
        return TryReadLocalPawn(out var pawn) && memory.TryRead(pawn + Offsets.UObjectEntityId, out id);
    }

    private bool TryReadPosition(nint entity, out Vector3 position)
    {
        position = default;
        if (entity == 0 ||
            !memory.TryRead(entity + Offsets.EntityHasPosition, out byte hasPosition) || hasPosition == 0 ||
            !memory.TryReadPtr(entity + Offsets.EntityPosition, out var block) ||
            !memory.TryReadArray(block + Offsets.PositionX, 3, out double[] xyz))
            return false;

        position = new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
    }

    private bool TryReadEntity(nint entity, int id, EntityKind kind, nint value, out Entity result)
    {
        result = default;
        if (entity == 0 ||
            !TryReadAliveIfRequired(entity, kind) ||
            !TryReadPosition(entity, out var position) || position.Z >= Offsets.ParkedZ ||
            !memory.TryRead(entity + Offsets.EntityCfgId, out uint cfgId) ||
            kind == EntityKind.Altar && cfgId != Offsets.SeasonAltarCfg)
            return false;

        var rarity = Rarity.Normal;
        if (kind == EntityKind.Monster &&
            memory.TryReadPtr(value + Offsets.MonsterRecord, out var record) &&
            memory.TryRead(record + Offsets.MonsterRecordCfgId, out uint monsterCfgId) && monsterCfgId != 0)
        {
            cfgId = monsterCfgId;
            rarity = _rarity.Read(monsterCfgId, entity);
        }

        result = new Entity(id, (int)cfgId, position, kind, rarity) { Address = entity };
        return true;
    }

    private bool TryReadAliveIfRequired(nint entity, EntityKind kind) =>
        kind is EntityKind.Npc or EntityKind.Portal or EntityKind.Interactive or EntityKind.Altar or EntityKind.Elevator or EntityKind.MapObject ||
        memory.TryRead(entity + Offsets.EntityAlive, out byte alive) && alive != 0;
}
