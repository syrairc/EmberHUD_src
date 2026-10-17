using System.Numerics;
using System.Runtime.InteropServices;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class LogicWorldReader(IReadBackend memory, nint moduleBase)
{
    private readonly record struct Known(nint Entity, nint Value, int CfgId);

    private readonly Names _names = new(memory, moduleBase);

    private readonly Dictionary<uint, uint> _tiers = [];
    private readonly HashSet<uint> _friendlyConfigs = [];
    private Dictionary<int, Known> _known = [];
    private Dictionary<int, Known> _seen = [];
    private int? _generation;
    private Entity[] _lastEntities = [];
    private Vector3? _lastPlayerPosition;
    private nint _lastScene;
    private int _tornTicks;
    private nint _playerRole;
    private nint _lastPlayerRole;
    private float _groundZ;

    internal readonly record struct Vitals(double Life, double MaxLife, double Shield, double MaxShield);

    public Vitals PlayerVitals { get; private set; }

    private readonly (int Map, EntityKind Kind)[] KindMaps =
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

    private readonly Dictionary<int, (EntityKind Kind, nint Value)> _kinds = [];
    private readonly List<(int Key, nint Value)> _pairs = [];

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

    public (Vector3? PlayerPosition, Entity[] Entities, nint Pawn) ReadWorld(nint scene, int generation)
    {
        var sameTickSource = _generation == generation && _lastScene == scene;
        if (_generation != generation)
        {
            _known.Clear();
            _generation = generation;
            _groundZ = 0;
        }

        if (!sameTickSource)
        {
            _lastEntities = [];
            _lastPlayerPosition = null;
            _lastPlayerRole = 0;
            _tornTicks = 0;
            _lastScene = scene;
        }

        _seen.Clear();
        _playerRole = 0;
        var kinds = ReadKinds();
        var hasPawn = TryReadLocalPawn(out var pawn);
        var localPlayerId = hasPawn && memory.TryRead(pawn + Offsets.UObjectEntityId, out int id) && kinds.TryGetValue(id, out var playerKind) && playerKind.Kind == EntityKind.Player ? id : (int?)null;
        if (localPlayerId is { } playerId && TryReadRawPosition(scene, playerId, out var rawPlayer) && TryReadWorldZ(pawn, out var worldZ))
            _groundZ = worldZ - rawPlayer.Z;
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
            {
                playerPosition = TryReadPosition(header, out var position) && position.Z < Offsets.ParkedZ ? Lift(position) : playerPosition;
                _playerRole = (nint)BitConverter.ToInt64(header[Offsets.EntityRole..]);
            }
            else if (mapped)
            {
                if (TryReadEntity(header, entity, entityId, kind.Kind, kind.Value, out var result))
                    entities.Add(result);
            }
            else
            {
                var cfgId = (int)CfgId(header);
                if (cfgId != 0) _seen[entityId] = new Known(entity, 0, cfgId);
                if (cfgId == Offsets.VigilCoffinCfg && TryReadEntity(header, entity, entityId, EntityKind.Coffin, 0, out var coffin))
                    entities.Add(coffin);
            }
        });

        if (whole)
        {
            (_known, _seen) = (_seen, _known);
            _lastEntities = [.. entities];
            _lastPlayerPosition = playerPosition;
            _lastPlayerRole = _playerRole;
            PlayerVitals = ReadVitals(_playerRole);
            _tornTicks = 0;
            return (playerPosition, _lastEntities, pawn);
        }

        if (_tornTicks < Offsets.MaxTornReuseTicks) PlayerVitals = ReadVitals(_lastPlayerRole);
        return ++_tornTicks <= Offsets.MaxTornReuseTicks ? (playerPosition ?? _lastPlayerPosition, _lastEntities, pawn) : (playerPosition, [], pawn);
    }

    public SceneEntity[] ListScene(nint scene, Names names)
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
            listed.Add(new SceneEntity(id, (int)cfgId, TryReadPosition(header, out var position) ? WorldPosition(position, kind.Value) : null,
                header[Offsets.EntityAlive] != 0, listedKind, mapped && names.TryReadClassName(kind.Value, out var className) ? className : "") { Address = entity });
        });
        return whole ? [.. listed] : [];
    }

    private bool TryWalkScene(nint scene, Action<int, nint> visit) =>
        memory.TryWalkBuckets(scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, scene + Offsets.SceneEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                visit(unchecked((int)BitConverter.ToUInt32(node[Offsets.NodeKey..])), (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]));
                return true;
            });

    private bool TryReadMonsterCfgId(nint value, out uint cfgId)
    {
        cfgId = 0;
        return memory.TryReadPtr(value + Offsets.MonsterRecord, out var record) && memory.TryRead(record + Offsets.MonsterRecordCfgId, out cfgId) && cfgId != 0;
    }

    public bool TryReadEntityPosition(nint scene, int id, out Vector3 position)
    {
        if (!TryReadRawPosition(scene, id, out position)) return false;
        position = Lift(position);
        return true;
    }

    private bool TryReadRawPosition(nint scene, int id, out Vector3 position)
    {
        position = default;
        Span<byte> header = stackalloc byte[Offsets.EntityHeaderSize];
        return memory.TryFindHashValue(scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, scene + Offsets.SceneEntityBuckets, unchecked((uint)id), out var entity) &&
               TryReadHeader(entity, header) && TryReadPosition(header, out position);
    }

    private Dictionary<int, (EntityKind Kind, nint Value)> ReadKinds()
    {
        _kinds.Clear();
        if (!memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr))
            return _kinds;

        foreach (var (map, kind) in KindMaps)
        {
            _pairs.Clear();
            memory.ReadTMapPointers(fightMgr + map, _pairs);
            foreach (var (key, value) in _pairs)
                _kinds[key] = (kind, value);
        }

        return _kinds;
    }

    public bool TryReadLocalPlayerId(out int id)
    {
        id = 0;
        return TryReadLocalPawn(out var pawn) && memory.TryRead(pawn + Offsets.UObjectEntityId, out id) && id != 0;
    }

    private bool TryReadLocalPawn(out nint pawn)
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

    public Camera? ReadCamera(nint pawn)
    {
        Span<byte> pov = stackalloc byte[Offsets.CameraPovSize];
        if (!memory.TryReadPtr(pawn + Offsets.PawnController, out var controller) ||
            !memory.TryReadPtr(controller + Offsets.ControllerCameraManager, out var manager) ||
            !memory.TryRead(manager + Offsets.CameraCache, pov))
            return null;

        var location = MemoryMarshal.Read<Vector3>(pov[Offsets.PovLocation..]);
        var rotation = MemoryMarshal.Read<Vector3>(pov[Offsets.PovRotation..]);
        var fov = BitConverter.ToSingle(pov[Offsets.PovFov..]);
        return float.IsFinite(fov) && fov is > 0 and < 180 && IsFinite(location) && IsFinite(rotation) ? new Camera(location, rotation, fov) : null;
    }

    private bool TryReadWorldZ(nint pawn, out float z)
    {
        z = 0;
        return memory.TryReadPtr(pawn + Offsets.PawnRootComponent, out var root) && memory.TryRead(root + Offsets.RootRelativeZ, out z) && float.IsFinite(z);
    }

    private Vector3 Lift(Vector3 raw) => raw with { Z = raw.Z + _groundZ };

    private Vector3 WorldPosition(Vector3 raw, nint actor) => TryReadWorldZ(actor, out var z) ? raw with { Z = z } : Lift(raw);

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private bool TryReadHeader(nint entity, Span<byte> header) => entity != 0 && memory.TryRead(entity, header);

    private static uint CfgId(ReadOnlySpan<byte> header) => BitConverter.ToUInt32(header[Offsets.EntityCfgId..]);

    private bool TryReadPosition(ReadOnlySpan<byte> header, out Vector3 position)
    {
        position = default;
        var block = (nint)BitConverter.ToInt64(header[Offsets.EntityPosition..]);
        Span<double> xyz = stackalloc double[3];
        if (header[Offsets.EntityHasPosition] == 0 || block == 0 || !memory.TryRead(block + Offsets.PositionX, xyz))
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

        position = WorldPosition(position, value);

        var rarity = Rarity.Normal;
        var role = (nint)0;
        if (kind == EntityKind.Monster)
        {
            role = (nint)BitConverter.ToInt64(header[Offsets.EntityRole..]);
            if (_known.TryGetValue(id, out var known) && known.Entity == entity && known.Value == value && TryGetTier((uint)known.CfgId, out var knownTier))
            {
                _seen[id] = known;
                cfgId = (uint)known.CfgId;
                rarity = Resolve(knownTier, role);
            }
            else if (TryReadMonsterCfgId(value, out var monsterCfgId))
            {
                cfgId = monsterCfgId;
                if (TryGetTier(monsterCfgId, out var tier))
                    _seen[id] = new Known(entity, value, (int)monsterCfgId);
                rarity = Resolve(tier, role);
            }
        }

        if (kind is EntityKind.Npc or EntityKind.Ally)
            cfgId = Identity(id, entity, value, kind, cfgId);

        var life = 0d;
        var maxLife = 0d;
        if (kind == EntityKind.Monster)
            TryReadStat(role, Offsets.RoleLifeMax, false, out life, out maxLife);
        var hidden = kind == EntityKind.Monster && memory.TryRead(value + Offsets.ActorFlags, out byte actorFlags) && (actorFlags & Offsets.ActorHiddenMask) != 0;
        result = new Entity(id, (int)cfgId, position, kind, rarity) { Address = entity, Life = life, MaxLife = maxLife, Friendly = kind == EntityKind.Monster && _friendlyConfigs.Contains(cfgId), Hidden = hidden, Invulnerable = kind == EntityKind.Monster && InvulnerableMonsters.Ids.Contains(cfgId) };
        return true;
    }

    private uint Identity(int id, nint entity, nint value, EntityKind kind, uint cfgId)
    {
        if (_known.TryGetValue(id, out var known) && known.Entity == entity && known.Value == value)
        {
            _seen[id] = known;
            return (uint)known.CfgId;
        }

        var read = kind switch
        {
            EntityKind.Npc => TryFindComponent(value, Offsets.NpcComponentClassName, out var npc) && memory.TryRead(npc + Offsets.NpcComponentNpcId, out uint npcId) && npcId != 0 ? npcId : (uint?)null,
            _ => TryReadMonsterCfgId(value, out var servantId) ? servantId : (uint?)null,
        };
        if (read is not { } found) return cfgId;
        _seen[id] = new Known(entity, value, (int)found);
        return found;
    }

    private bool TryFindComponent(nint actor, string className, out nint component)
    {
        component = 0;
        _pairs.Clear();
        memory.ReadTMapPointers(actor + Offsets.EntityComponents, _pairs);
        foreach (var (_, candidate) in _pairs)
            if (_names.TryReadClassName(candidate, out var name) && name == className)
            {
                component = candidate;
                return true;
            }
        return false;
    }

    private Vitals ReadVitals(nint role)
    {
        TryReadStat(role, Offsets.RoleLifeMax, false, out var life, out var maxLife);
        TryReadStat(role, Offsets.RoleShieldMax, true, out var shield, out var maxShield);
        return new Vitals(life, maxLife, shield, maxShield);
    }

    private bool TryReadStat(nint role, int maxOffset, bool zeroMaxAllowed, out double current, out double max)
    {
        current = max = 0;
        Span<double> pair = stackalloc double[2];
        if (role == 0 || !memory.TryRead(role + maxOffset, pair))
            return false;

        var (m, c) = (pair[0], pair[1]);
        if (!double.IsFinite(m) || !double.IsFinite(c) || m < 0 || m > Offsets.MaxStatValue || m == 0 && !zeroMaxAllowed || c < 0 || c > m * 1.001)
            return false;

        (max, current) = (m, Math.Min(c, m));
        return true;
    }

    private static bool IsAliveIfRequired(ReadOnlySpan<byte> header, EntityKind kind) =>
        kind is EntityKind.Npc or EntityKind.Portal or EntityKind.Interactive or EntityKind.Altar or EntityKind.Elevator or EntityKind.MapObject ||
        header[Offsets.EntityAlive] != 0;

    internal Rarity Resolve(uint tier, nint roleBlock)
    {
        var role = 0;
        if (roleBlock != 0)
            memory.TryRead(roleBlock + Offsets.RoleRarity, out role);
        return (tier, role) switch
        {
            (4 or 5, _) => Rarity.Boss,
            (_, 1) => Rarity.Normal,
            (_, 2) => Rarity.Magic,
            (_, 3) => Rarity.Rare,
            (_, >= 4) => Rarity.Boss,
            (2 or 3, _) => Rarity.Rare,
            _ => Rarity.Normal,
        };
    }

    internal bool TryGetTier(uint configId, out uint tier)
    {
        if (_tiers.TryGetValue(configId, out tier))
            return true;
        if (!TryReadTier(configId, out tier))
            return false;

        _tiers[configId] = tier;
        return true;
    }

    private bool TryReadTier(uint configId, out uint tier)
    {
        tier = 0;
        if (!memory.TryFindHashValue(moduleBase + Offsets.MonsterCfgBucketCountRva, moduleBase + Offsets.MonsterCfgBucketsRva, configId, out var row) || !memory.TryRead(row + Offsets.MonsterCfgRarity, out tier))
            return false;

        if (memory.TryRead(row + Offsets.MonsterCfgFriendly, out uint friendly) && friendly == 1)
            _friendlyConfigs.Add(configId);
        return true;
    }
}
