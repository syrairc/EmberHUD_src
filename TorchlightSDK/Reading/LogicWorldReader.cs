using System.Numerics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

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
                node = next;
            }
        }

        return (playerPosition, [.. entities]);
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
        ];
        foreach (var (map, kind) in maps)
            foreach (var (key, value) in _maps.Read(fightMgr + map))
                kinds[key] = (kind, value);
        return kinds;
    }

    private bool TryReadLocalPlayerId(out int id)
    {
        id = 0;
        if (!memory.TryReadPtr(moduleBase + Offsets.PlayerMgrGlobalRva, out var playerMgr) ||
            !memory.TryRead(playerMgr + Offsets.PlayerMgrEntity, out uint index) ||
            !memory.TryReadPtr(moduleBase + Offsets.GObjectsGlobalRva, out var chunks) ||
            !memory.TryReadPtr(chunks + (nint)(index / Offsets.GObjectsPerChunk) * 8, out var chunk) ||
            !memory.TryReadPtr(chunk + (nint)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize, out var obj) ||
            !memory.TryRead(obj + Offsets.UObjectInternalIndex, out uint internalIndex) || internalIndex != index)
            return false;

        return memory.TryRead(obj + Offsets.UObjectEntityId, out id);
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
            !memory.TryRead(entity + Offsets.EntityCfgId, out uint cfgId))
            return false;

        var rarity = Rarity.Normal;
        if (kind == EntityKind.Monster &&
            memory.TryReadPtr(value + Offsets.MonsterRecord, out var record) &&
            memory.TryRead(record + Offsets.MonsterRecordCfgId, out uint monsterCfgId) && monsterCfgId != 0)
        {
            cfgId = monsterCfgId;
            rarity = _rarity.Read(monsterCfgId);
        }

        result = new Entity(id, (int)cfgId, position, kind, rarity);
        return true;
    }

    private bool TryReadAliveIfRequired(nint entity, EntityKind kind) =>
        kind is EntityKind.Npc or EntityKind.Portal or EntityKind.Interactive ||
        memory.TryRead(entity + Offsets.EntityAlive, out byte alive) && alive != 0;
}
