using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class LogicWorldReader(IReadBackend memory, nint moduleBase)
{
    private readonly record struct Known(nint Entity, nint Value, nint Role, int CfgId, uint Tier);

    private readonly MonsterRarityReader _rarity = new(memory, moduleBase);
    private Dictionary<int, Known> _known = [];
    private Dictionary<int, Known> _seen = [];
    private int? _generation;

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

    public (Vector3? PlayerPosition, Entity[] Entities) ReadWorld(nint scene, int generation)
    {
        if (_generation != generation)
        {
            _known.Clear();
            _generation = generation;
        }

        _seen.Clear();
        var kinds = ReadKinds();
        var localPlayerId = TryReadLocalPlayerId(out var id) && kinds.TryGetValue(id, out var playerKind) && playerKind.Kind == EntityKind.Player ? id : (int?)null;
        Vector3? playerPosition = null;
        var entities = new List<Entity>();
        var whole = TryWalkScene(scene, (entityId, entity) =>
        {
            var mapped = kinds.TryGetValue(entityId, out var kind);
            if (!mapped && _known.TryGetValue(entityId, out var seen) && seen.Entity == entity && seen.Value == 0 && seen.CfgId != Offsets.VigilCoffinCfg)
            {
                _seen[entityId] = seen;
                return;
            }

            Span<byte> header = stackalloc byte[Offsets.EntityHeaderSize];
            if (!TryReadHeader(entity, header)) return;

            if (entityId == localPlayerId)
                playerPosition = TryReadPosition(header, out var position) && position.Z < Offsets.ParkedZ ? position : playerPosition;
            else if (mapped)
            {
                if (TryReadEntity(header, entity, entityId, kind.Kind, kind.Value, out var result))
                    entities.Add(result);
            }
            else
            {
                var cfgId = (int)CfgId(header);
                if (cfgId != 0) _seen[entityId] = new Known(entity, 0, 0, cfgId, 0);
                if (cfgId == Offsets.VigilCoffinCfg && TryReadEntity(header, entity, entityId, EntityKind.Coffin, 0, out var coffin))
                    entities.Add(coffin);
            }
        });

        if (whole)
            (_known, _seen) = (_seen, _known);
        return (playerPosition, whole ? [.. entities] : []);
    }

    public SceneEntity[] ListScene(nint scene, TryName tryName)
    {
        var kinds = ReadKinds();
        var listed = new List<SceneEntity>();
        var whole = TryWalkScene(scene, (id, entity) =>
        {
            Span<byte> header = stackalloc byte[Offsets.EntityHeaderSize];
            if (!TryReadHeader(entity, header)) return;

            var cfgId = CfgId(header);
            var mapped = kinds.TryGetValue(id, out var kind);
            EntityKind? listedKind = (mapped, kind.Kind) switch
            {
                (false, _) => cfgId == Offsets.VigilCoffinCfg ? EntityKind.Coffin : null,
                (true, EntityKind.Altar) when cfgId != Offsets.SeasonAltarCfg => null,
                _ => kind.Kind,
            };
            if (listedKind == EntityKind.Monster && TryReadMonsterCfgId(kind.Value, out var monsterCfgId)) cfgId = monsterCfgId;
            listed.Add(new SceneEntity(id, (int)cfgId, TryReadPosition(header, out var position) ? position : null,
                header[Offsets.EntityAlive] != 0, listedKind, mapped ? ClassName(kind.Value, tryName) : "") { Address = entity });
        });
        return whole ? [.. listed] : [];
    }

    private bool TryWalkScene(nint scene, Action<int, nint> visit)
    {
        if (!memory.TryReadBuckets(scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, scene + Offsets.SceneEntityBuckets, Offsets.MaxBuckets, out var buckets))
            return false;

        Span<byte> node = stackalloc byte[Offsets.NodeSize];
        var nodeCount = 0;
        foreach (var head in buckets)
        {
            var chainLength = 0;
            for (var at = head; at != 0; )
            {
                if (++chainLength > Offsets.MaxChain || ++nodeCount > Offsets.MaxEntities || !memory.TryRead(at, node))
                    return false;

                visit(unchecked((int)BitConverter.ToUInt32(node[Offsets.NodeKey..])), (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]));
                at = (nint)BitConverter.ToInt64(node[Offsets.NodeNext..]);
            }
        }

        return true;
    }

    private string ClassName(nint obj, TryName tryName) =>
        memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && memory.TryRead(cls + Offsets.UObjectName, out uint index) && tryName(index, out var name) ? name : "";

    private bool TryReadMonsterCfgId(nint value, out uint cfgId)
    {
        cfgId = 0;
        return memory.TryReadPtr(value + Offsets.MonsterRecord, out var record) && memory.TryRead(record + Offsets.MonsterRecordCfgId, out cfgId) && cfgId != 0;
    }

    public bool TryReadEntityPosition(nint scene, int id, out Vector3 position)
    {
        position = default;
        Span<byte> header = stackalloc byte[Offsets.EntityHeaderSize];
        return memory.TryFindHashValue(scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, scene + Offsets.SceneEntityBuckets, unchecked((uint)id), out var entity) &&
               TryReadHeader(entity, header) && TryReadPosition(header, out position);
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
            foreach (var (key, value) in memory.ReadTMapPointers(fightMgr + map))
                kinds[key] = (kind, value);
        return kinds;
    }

    public bool TryReadLocalPawn(out nint pawn)
    {
        pawn = 0;
        if (!memory.TryReadPtr(moduleBase + Offsets.PlayerMgrGlobalRva, out var playerMgr) ||
            !memory.TryRead(playerMgr + Offsets.PlayerMgrEntity, out uint index) ||
            !memory.TryReadGObject(moduleBase, index, out var obj, out _) ||
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

    private bool TryReadHeader(nint entity, Span<byte> header) => entity != 0 && memory.TryRead(entity, header);

    private static uint CfgId(ReadOnlySpan<byte> header) => BitConverter.ToUInt32(header[Offsets.EntityCfgId..]);

    private bool TryReadPosition(ReadOnlySpan<byte> header, out Vector3 position)
    {
        position = default;
        var block = (nint)BitConverter.ToInt64(header[Offsets.EntityPosition..]);
        if (header[Offsets.EntityHasPosition] == 0 || block == 0 || !memory.TryReadArray(block + Offsets.PositionX, 3, out double[] xyz))
            return false;

        position = new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
    }

    private bool TryReadEntity(ReadOnlySpan<byte> header, nint entity, int id, EntityKind kind, nint value, out Entity result)
    {
        result = default;
        var cfgId = CfgId(header);
        if (!IsAliveIfRequired(header, kind) ||
            !TryReadPosition(header, out var position) || position.Z >= Offsets.ParkedZ ||
            kind == EntityKind.Altar && cfgId != Offsets.SeasonAltarCfg)
            return false;

        var rarity = Rarity.Normal;
        if (kind == EntityKind.Monster)
        {
            var role = (nint)BitConverter.ToInt64(header[Offsets.EntityRole..]);
            if (_known.TryGetValue(id, out var known) && known.Entity == entity && known.Value == value && known.Role == role)
            {
                _seen[id] = known;
                cfgId = (uint)known.CfgId;
                rarity = _rarity.Resolve(known.Tier, role);
            }
            else if (TryReadMonsterCfgId(value, out var monsterCfgId))
            {
                cfgId = monsterCfgId;
                if (_rarity.TryGetTier(monsterCfgId, out var tier))
                    _seen[id] = new Known(entity, value, role, (int)monsterCfgId, tier);
                rarity = _rarity.Resolve(tier, role);
            }
        }

        result = new Entity(id, (int)cfgId, position, kind, rarity) { Address = entity };
        return true;
    }

    private static bool IsAliveIfRequired(ReadOnlySpan<byte> header, EntityKind kind) =>
        kind is EntityKind.Npc or EntityKind.Portal or EntityKind.Interactive or EntityKind.Altar or EntityKind.Elevator or EntityKind.MapObject ||
        header[Offsets.EntityAlive] != 0;
}
