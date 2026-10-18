using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

internal readonly record struct FakeEntity(uint Id, nint Address, int MapOffset = 0, nint Value = 0);

internal readonly record struct FakeMinimap(nint Widget, nint Material);

internal readonly record struct FakeIcon(int Type, float X, float Y, int Zorder = 0, int EntityId = 0, float Size = 1, uint? Area = null, float Rotation = 0);

internal readonly record struct FakeNameTagLabel(int DropId, Vector2 Min, Vector2 Max, bool Valid = true, bool Allocated = true);

internal readonly record struct FakeNameTagGroup(int Key, Vector3 WorldPos, Vector2 ScreenPos, FakeNameTagLabel[] Labels, bool InScreen = true);

internal sealed class FakeWorld
{
    public static readonly nint Module = unchecked((nint)0x1_4000_0000L);
    public static readonly nint Unmapped = unchecked((nint)0x7777_0000_0000L);
    public const int Unregistered = 0;

    private readonly nint _worldAddress;
    private nint _next = unchecked((nint)0x2_0000_0000L);
    private readonly nint _nameBlock;
    private int _nameCursor = 2;
    private readonly Dictionary<long, nint> _objects = [];
    private long _nextObjectIndex = 1000;
    private nint _chunkTable;
    private readonly nint[] _chunks = new nint[Offsets.MaxGObjectChunks];

    public GameUiReader UiReader(Func<long>? clock = null) => new(Memory, Module, new Names(Memory, Module), clock);

    public FakeWorld()
    {
        Memory.Map(Module + Offsets.LogicWorldGlobalRva, 8);
        _worldAddress = Alloc(0x100);
        Scene = Alloc(0x800);
        Memory.Write(Module + Offsets.LogicWorldGlobalRva, _worldAddress);
        Memory.Write(_worldAddress + Offsets.WorldScene, Scene);
        Memory.Map(Module + Offsets.FightMgrGlobalRva, 8);
        FightMgr = Alloc(0x1000);
        Memory.Write(Module + Offsets.FightMgrGlobalRva, FightMgr);
        Memory.Map(Module + Offsets.PlayerMgrGlobalRva, 8);
        Memory.Map(Module + Offsets.MiniMapMgrGlobalRva, 8);
        Memory.Map(Module + Offsets.GObjectsGlobalRva, 0x20);
        Memory.Map(Module + Offsets.MonsterCfgBucketsRva, 8);
        Memory.Map(Module + Offsets.MonsterCfgBucketCountRva, 4);
        Memory.Map(Module + Offsets.MapIconInfoBucketsRva, 8);
        Memory.Map(Module + Offsets.MapIconInfoBucketCountRva, 4);
        Memory.Map(Module + Offsets.FNamePoolRva, Offsets.FNameBlocks + 8 * 4);
        _nameBlock = Alloc(Offsets.FNameBlockSize * Offsets.FNameEntryStride);
        Memory.Write(Module + Offsets.FNamePoolRva + Offsets.FNameBlocks, _nameBlock);
    }

    public nint FightMgr { get; }

    public FakeMemory Memory { get; } = new();

    public nint Scene { get; }

    public nint Alloc(int size)
    {
        var address = _next;
        _next += (size + 0xFFF) & ~0xFFF;
        Memory.Map(address, size);
        return address;
    }

    public List<nint> Nodes { get; } = [];

    public FakeEntity AddEntity(uint id, uint cfgId, bool alive, double x, double y, double z, bool hasPosition = true, int? mapOffset = null, uint? monsterCfgId = null, int? role = null, (double Max, double Cur)? life = null, (double Max, double Cur)? shield = null, bool hidden = false)
    {
        var map = mapOffset ?? Offsets.FightMapMonster;
        var entity = Alloc(0x400);
        var block = Alloc(0x80);
        Memory.Write(entity + Offsets.EntityCfgId, cfgId);
        Memory.Write(entity + Offsets.EntityAlive, (byte)(alive ? 1 : 0));
        Memory.Write(entity + Offsets.EntityHasPosition, (byte)(hasPosition ? 1 : 0));
        Memory.Write(entity + Offsets.EntityPosition, block);
        Memory.WriteArray(block + Offsets.PositionX, new[] { x, y, z });
        if (role is not null || life is not null || shield is not null)
        {
            var roleBlock = Alloc(0x100);
            Memory.Write(entity + Offsets.EntityRole, roleBlock);
            Memory.Write(roleBlock + Offsets.RoleRarity, role ?? 0);
            if (life is { } l) Memory.WriteArray(roleBlock + Offsets.RoleLifeMax, new[] { l.Max, l.Cur });
            if (shield is { } sh) Memory.WriteArray(roleBlock + Offsets.RoleShieldMax, new[] { sh.Max, sh.Cur });
        }

        var value = map == Unregistered ? 0 : Alloc(0x400);
        if (value != 0) Memory.Write(value + Offsets.ActorFlags, (byte)(0x90 | (hidden ? Offsets.ActorHiddenMask : 0)));
        if (value != 0 && monsterCfgId is { } monsterCfg)
        {
            var record = Alloc(0x200);
            Memory.Write(value + Offsets.MonsterRecord, record);
            Memory.Write(record + Offsets.MonsterRecordCfgId, monsterCfg);
        }

        return new FakeEntity(id, entity, map, value);
    }

    internal readonly record struct FakeCast(nint Skill, nint Timer, nint Timing);

    private readonly record struct FakeFighter(nint Entity, nint Component, List<(uint Key, nint Skill)> Skills);

    private nint _fightWorld;
    private readonly Dictionary<uint, FakeFighter> _fighters = [];

    public nint FightWorld => _fightWorld != 0 ? _fightWorld : _fightWorld = BuildFightWorld();

    private nint _fightMsg;
    private nint FightMsg => FightWorld != 0 ? _fightMsg : 0;
    private nint _fightHolder;

    private nint BuildFightWorld()
    {
        var holder = Alloc(0x100);
        var inner = Alloc(0x100);
        var world = Alloc(0x800);
        _fightHolder = holder;
        _fightMsg = Alloc(0x200);
        Memory.Write(holder + Offsets.FightWorldFightMsg, _fightMsg);
        Memory.Write(FightMgr + Offsets.FightWorldHop1, holder);
        Memory.Write(holder + Offsets.FightWorldHop2, inner);
        Memory.Write(inner + Offsets.FightWorldHop3, world);
        return world;
    }

    private void PublishFighters()
    {
        var (buckets, nodes) = WriteChains(4, _fighters.Select(f => (f.Key, f.Value.Entity)));
        foreach (var (node, key) in nodes.Zip(_fighters.Keys)) Memory.Write(node + Offsets.FightNodeKey, (long)key);
        Memory.Write(FightWorld + Offsets.FightWorldEntityBuckets, buckets);
        Memory.Write(FightWorld + Offsets.FightWorldEntityBucketCount, 4u);
    }

    private void WriteTable(nint table, nint[] slots, byte[] index)
    {
        var indexAt = Alloc(index.Length);
        var slotsAt = Alloc(slots.Length * 8);
        Memory.WriteArray(indexAt, index);
        Memory.WriteArray(slotsAt, slots);
        Memory.Write(table + Offsets.FightTableSlotCap, slots.Length - 1);
        Memory.Write(table + Offsets.FightTableIndex, indexAt);
        Memory.Write(table + Offsets.FightTableSlots, slotsAt);
    }

    public FakeCast AddCast(FakeEntity monster, uint stoneCfg, byte state, double remaining, double total, double speed = 1, (double X, double Y) target = default, (double X, double Y) direction = default, uint dynId = 450, int? caster = null, bool direct = false)
    {
        if (!_fighters.TryGetValue(monster.Id, out var fighter))
        {
            var entity = Alloc(0x200);
            var component = Alloc(0x600);
            var index = new byte[153];
            index[Offsets.SkillComponentId] = 1;
            if (direct)
            {
                WriteTable(entity + Offsets.FightEntityDirectTable, [0, component], index);
            }
            else
            {
                var entry = Alloc(0x40);
                Memory.Write(entry + Offsets.FightEntryComponent, component);
                WriteTable(entity + Offsets.FightEntityDirectTable, [0, 0], index);
                WriteTable(entity + Offsets.FightEntityEntryTable, [0, entry], index);
            }

            fighter = new FakeFighter(entity, component, []);
            _fighters[monster.Id] = fighter;
        }

        var skill = Alloc(0x400);
        var timer = Alloc(0x80);
        var timing = Alloc(0x80);
        Memory.Write(skill + Offsets.SkillCaster, caster ?? (int)monster.Id);
        Memory.Write(skill + Offsets.SkillStoneCfg, stoneCfg);
        Memory.Write(skill + Offsets.SkillState, state);
        Memory.WriteArray(skill + Offsets.SkillTarget, new[] { target.X, target.Y });
        Memory.WriteArray(skill + Offsets.SkillDirection, new[] { direction.X, direction.Y });
        Memory.Write(skill + Offsets.SkillTimer, timer);
        Memory.Write(skill + Offsets.SkillTiming, timing);
        Memory.Write(timer + Offsets.TimerPrecastRemaining, remaining);
        Memory.Write(timer + Offsets.TimerSpeed, speed);
        Memory.Write(timing + Offsets.TimingPrecastTotal, total);
        fighter.Skills.Add((dynId + (uint)fighter.Skills.Count, skill));
        var (buckets, _) = WriteChains(4, fighter.Skills);
        Memory.Write(fighter.Component + Offsets.SkillActiveBuckets, buckets);
        Memory.Write(fighter.Component + Offsets.SkillActiveBucketCount, 4u);
        PublishFighters();
        return new FakeCast(skill, timer, timing);
    }

    internal readonly record struct FakeBullet(nint Bullet, nint Shape, nint Move, nint Transform, nint Attachment);

    public FakeBullet AddBullet(uint id, uint ownerId, (double X, double Y) position, (double X, double Y) direction, (double X, double Y) origin, float radius = 240, double speed = 1200, byte ownerCamp = 1, (uint A, uint B)? rect = null, bool moveDirection = true)
    {
        if (!_fighters.ContainsKey(ownerId)) AddSideFighter(ownerId, ownerCamp);
        var entity = Alloc(0x200);
        var attachment = Alloc(0x100);
        var bullet = Alloc(0x100);
        var shape = Alloc(0x100);
        var move = Alloc(0x100);
        var transform = Alloc(0x100);
        Memory.Write(attachment + Offsets.AttachmentOwnerId, ownerId);
        Memory.WriteArray(transform + Offsets.TransformPosition, new[] { position.X, position.Y });
        Memory.WriteArray(transform + Offsets.TransformDirection, new[] { direction.X, direction.Y });
        Memory.WriteArray(bullet + Offsets.BulletOrigin, new[] { origin.X, origin.Y });
        Memory.Write(shape + Offsets.ShapeRadius, (uint)radius);
        if (rect is { } extents)
        {
            Memory.Write(shape + Offsets.ShapeKind, (uint)Offsets.ShapeRect);
            Memory.Write(shape + Offsets.ShapeExtentA, extents.A);
            Memory.Write(shape + Offsets.ShapeExtentB, extents.B);
        }

        Memory.Write(move + Offsets.MoveSpeed, speed);
        if (moveDirection) Memory.WriteArray(move + Offsets.MoveDirection, new[] { direction.X, direction.Y });
        var index = new byte[153];
        index[Offsets.AttachmentComponentId] = 1;
        index[Offsets.BulletComponentId] = 2;
        index[Offsets.MoveComponentId] = 3;
        index[Offsets.ShapeComponentId] = 4;
        index[Offsets.TransformComponentId] = 5;
        WriteTable(entity + Offsets.FightEntityDirectTable, [0, attachment, bullet, move, shape, transform], index);
        _fighters[id] = new FakeFighter(entity, bullet, []);
        PublishFighters();
        return new FakeBullet(bullet, shape, move, transform, attachment);
    }

    public void AddLaser(uint id, uint ownerId, (double X, double Y) position, (double X, double Y) direction, (uint A, uint B) rect, byte ownerCamp = 1, double speed = 0, bool damage = true)
    {
        if (!_fighters.ContainsKey(ownerId)) AddSideFighter(ownerId, ownerCamp);
        var attachment = Alloc(0x100);
        var shape = Alloc(0x100);
        var move = Alloc(0x100);
        var transform = Alloc(0x100);
        Memory.Write(attachment + Offsets.AttachmentOwnerId, ownerId);
        Memory.WriteArray(transform + Offsets.TransformPosition, new[] { position.X, position.Y });
        Memory.WriteArray(transform + Offsets.TransformDirection, new[] { direction.X, direction.Y });
        Memory.Write(shape + Offsets.ShapeKind, (uint)Offsets.ShapeRect);
        Memory.Write(shape + Offsets.ShapeExtentA, rect.A);
        Memory.Write(shape + Offsets.ShapeExtentB, rect.B);
        Memory.Write(move + Offsets.MoveSpeed, speed);
        var components = new List<(int, nint)> { (Offsets.AttachmentComponentId, attachment), (Offsets.MoveComponentId, move), (Offsets.ShapeComponentId, shape), (Offsets.TransformComponentId, transform) };
        if (damage) components.Add((Offsets.DamageDataComponentId, Alloc(0x100)));
        AddFightEntity(id, [.. components]);
    }

    public void AddSideFighter(uint id, byte camp)
    {
        var entity = Alloc(0x200);
        var side = Alloc(0x100);
        Memory.WriteArray(side + Offsets.SideCamp, new[] { camp });
        var index = new byte[153];
        index[Offsets.SideComponentId] = 1;
        WriteTable(entity + Offsets.FightEntityDirectTable, [0, side], index);
        _fighters[id] = new FakeFighter(entity, 0, []);
        PublishFighters();
    }

    public nint AddArea(uint id, (double X, double Y) centre, uint radius, byte camp, double scale = 1, uint kind = 1, bool damage = false)
    {
        var side = Alloc(0x100);
        Memory.Write(side + Offsets.SideCamp, camp);
        var extra = new List<(int, nint)> { (Offsets.BuffAreaComponentId, Alloc(0x100)), (Offsets.SideComponentId, side) };
        if (damage) extra.Add((Offsets.DamageDataComponentId, Alloc(0x100)));
        return AddHazard(id, centre, radius, scale, kind, [.. extra]);
    }

    public nint AddRock(uint id, uint ownerId, (double X, double Y) centre, uint radius, byte ownerCamp = 1, bool bullet = false)
    {
        if (!_fighters.ContainsKey(ownerId)) AddSideFighter(ownerId, ownerCamp);
        var attachment = Alloc(0x100);
        Memory.Write(attachment + Offsets.AttachmentOwnerId, ownerId);
        var extra = new List<(int, nint)> { (Offsets.DamageDataComponentId, Alloc(0x100)), (Offsets.AttachmentComponentId, attachment) };
        if (bullet) extra.Add((Offsets.BulletComponentId, Alloc(0x100)));
        return AddHazard(id, centre, radius, 1, 1, [.. extra]);
    }

    private nint AddHazard(uint id, (double X, double Y) centre, uint radius, double scale, uint kind, params (int Component, nint Address)[] extra)
    {
        var shape = Alloc(0x100);
        var transform = Alloc(0x100);
        var scaleComponent = Alloc(0x100);
        Memory.Write(shape + Offsets.ShapeKind, kind);
        Memory.Write(shape + Offsets.ShapeRadius, radius);
        Memory.WriteArray(transform + Offsets.TransformPosition, new[] { centre.X, centre.Y });
        Memory.Write(scaleComponent + Offsets.ScaleValue, scale);
        return AddFightEntity(id, [(Offsets.ShapeComponentId, shape), (Offsets.TransformComponentId, transform), (Offsets.ScaleComponentId, scaleComponent), .. extra]);
    }

    public void SetFightFrame(uint frame) => Memory.Write(FightMsg + Offsets.FightMsgFrame, frame);

    public void SetFightMsgResolvable(bool resolvable)
    {
        _ = FightWorld;
        Memory.Write(_fightHolder + Offsets.FightWorldFightMsg, resolvable ? _fightMsg : 0);
    }

    public nint AddFightEntity(long id, params (int Component, nint Address)[] components)
    {
        var entity = Alloc(0x400);
        var index = new byte[160];
        var slots = new nint[components.Length + 1];
        for (var i = 0; i < components.Length; i++)
        {
            index[components[i].Component] = (byte)(i + 1);
            slots[i + 1] = components[i].Address;
        }

        WriteTable(entity + Offsets.FightEntityDirectTable, slots, index);
        _fighters[(uint)id] = new FakeFighter(entity, 0, []);
        PublishFighters();
        return entity;
    }

    public nint AddBuffComponent(params nint[] instances)
    {
        const int bucketCount = 5;
        var component = Alloc(0x400);
        var heads = new nint[bucketCount];
        for (var i = 0; i < instances.Length; i++)
        {
            var node = Alloc(0x20);
            var bucket = i % bucketCount;
            Memory.Write(node, i + 1);
            Memory.Write(node + Offsets.FightNodeValue, instances[i]);
            Memory.Write(node + Offsets.NodeNext, heads[bucket]);
            heads[bucket] = node;
        }

        var buckets = Alloc(bucketCount * 8);
        Memory.WriteArray(buckets, heads);
        Memory.Write(component + Offsets.BuffMap, buckets);
        Memory.Write(component + Offsets.BuffMapCount, (uint)bucketCount);
        return component;
    }

    public nint AddSkillCfgComponent(int cfgId)
    {
        var component = Alloc(0x100);
        var cfg = Alloc(0x20);
        Memory.Write(cfg, cfgId);
        Memory.Write(component + Offsets.SkillCfgPointer, cfg);
        return component;
    }

    public nint AddCooldownComponent(params (uint Skill, int Charges, int MaxCharges, double Remaining, double Total)[] records)
    {
        const int bucketCount = 5;
        var component = Alloc(0x400);
        var heads = new nint[bucketCount];
        for (var i = 0; i < records.Length; i++)
        {
            var node = Alloc(Offsets.CooldownNodeSize);
            var bucket = i % bucketCount;
            Memory.Write(node + Offsets.CooldownSkill, records[i].Skill);
            Memory.Write(node + Offsets.CooldownCharges, records[i].Charges);
            Memory.Write(node + Offsets.CooldownMaxCharges, records[i].MaxCharges);
            Memory.Write(node + Offsets.CooldownRemaining, records[i].Remaining);
            Memory.Write(node + Offsets.CooldownTotal, records[i].Total);
            Memory.Write(node + Offsets.CooldownNodeNext, heads[bucket]);
            heads[bucket] = node;
        }

        var buckets = Alloc(bucketCount * 8);
        Memory.WriteArray(buckets, heads);
        Memory.Write(component + Offsets.CooldownMap, buckets);
        Memory.Write(component + Offsets.CooldownMapCount, (uint)bucketCount);
        return component;
    }

    public nint AddBuff(int id, double remaining, double total, bool withCfgRow = true, int? cfgId = null)
    {
        var buff = Alloc(0x200);
        Memory.Write(buff + Offsets.BuffTotal, total);
        Memory.Write(buff + Offsets.BuffRemaining, remaining);
        Memory.Write(buff + Offsets.BuffId, id);
        if (!withCfgRow) return buff;
        var row = Alloc(0x100);
        Memory.Write(row + Offsets.BuffCfgId, cfgId ?? id);
        Memory.Write(buff + Offsets.BuffCfgRow, row);
        return buff;
    }

    public void WriteMap(int mapOffset, params (int Key, nint Value)?[] slots) => WriteMapAt(FightMgr + mapOffset, slots);

    public void WriteMapAt(nint map, params (int Key, nint Value)?[] slots) =>
        WriteTMap(map, Offsets.TMapElementStride, slots.Length, i => slots[i] is not null, (at, i) =>
        {
            var (key, value) = slots[i] ?? (-1, 0x1111);
            Memory.Write(at, key);
            Memory.Write(at + Offsets.TMapElementValue, value);
        });

    public (nint Buckets, List<nint> Nodes) WriteChains(int bucketCount, IEnumerable<(uint Key, nint Entity)> rows)
    {
        var heads = new nint[bucketCount];
        var nodes = new List<nint>();
        foreach (var (key, entity) in rows)
        {
            var node = Alloc(0x20);
            var bucket = (int)(key % bucketCount);
            Memory.Write(node + Offsets.NodeKey, key);
            Memory.Write(node + Offsets.NodeEntity, entity);
            Memory.Write(node + Offsets.NodeNext, heads[bucket]);
            heads[bucket] = node;
            nodes.Add(node);
        }

        var buckets = Alloc(Math.Max(8, bucketCount * 8));
        Memory.WriteArray(buckets, heads);
        return (buckets, nodes);
    }

    private void WriteTMap(nint map, int stride, int count, Func<int, bool> present, Action<nint, int> writeElement)
    {
        var data = Alloc(Math.Max(stride, count * stride));
        var bits = new uint[Math.Max(Offsets.TMapInlineBitCount / 32, (count + 31) / 32)];
        for (var i = 0; i < count; i++)
        {
            writeElement(data + i * stride, i);
            if (present(i)) bits[i / 32] |= 1u << (i % 32);
        }

        Memory.Write(map, data);
        Memory.Write(map + Offsets.TArrayNum, count);
        if (count <= Offsets.TMapInlineBitCount)
        {
            Memory.WriteArray(map + Offsets.TMapInlineBits, bits[..(Offsets.TMapInlineBitCount / 32)]);
            return;
        }

        var secondary = Alloc(bits.Length * 4);
        Memory.WriteArray(secondary, bits);
        Memory.Write(map + Offsets.TMapSecondaryBits, secondary);
    }

    private readonly Dictionary<long, int> _serials = [];

    public void BindMinimap(FakeMinimap minimap, int serial = 7, int? weakSerial = null)
    {
        var index = _objects.Single(o => o.Value == minimap.Widget).Key;
        _serials[index] = serial;
        PlaceObject(index, minimap.Widget);
        var manager = Alloc(0x100);
        Memory.Write(manager + Offsets.MiniMapMgrWidget, (uint)index);
        Memory.Write(manager + Offsets.MiniMapMgrWidget + Offsets.WeakPtrSerial, weakSerial ?? serial);
        Memory.Write(Module + Offsets.MiniMapMgrGlobalRva, manager);
    }

    public nint PlaceObject(long index, nint obj)
    {
        _objects[index] = obj;
        if (_chunkTable == 0)
        {
            _chunkTable = Alloc(Offsets.MaxGObjectChunks * 8);
            Memory.Write(Module + Offsets.GObjectsGlobalRva, _chunkTable);
        }

        var chunkIndex = (int)(index / Offsets.GObjectsPerChunk);
        if (_chunks[chunkIndex] == 0)
        {
            _chunks[chunkIndex] = Alloc(Offsets.GObjectsPerChunk * Offsets.GObjectsItemSize);
            Memory.Write(_chunkTable + chunkIndex * 8, _chunks[chunkIndex]);
        }

        var item = _chunks[chunkIndex] + (int)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize;
        Memory.Write(item + Offsets.GObjectsItemObject, obj);
        Memory.Write(item + Offsets.GObjectsItemSerial, _serials.GetValueOrDefault(index));
        Memory.Write(Module + Offsets.GObjectsGlobalRva + Offsets.GObjectsNumElements, (int)(_objects.Keys.Max() + 1));
        return _chunkTable;
    }

    public uint ObjectIndex(nint obj) => (uint)_objects.Single(o => o.Value == obj).Key;

    public void SetObjectSerial(nint obj, int serial)
    {
        var index = _objects.Single(o => o.Value == obj).Key;
        _serials[index] = serial;
        PlaceObject(index, obj);
    }

    public (nint Object, nint Chunks) SetLocalPlayer(uint entityId, uint index = 77)
    {
        var manager = Alloc(0x100);
        Memory.Write(Module + Offsets.PlayerMgrGlobalRva, manager);
        Memory.WriteArray(manager + Offsets.PlayerMgrEntity, new[] { index, 1u });
        var obj = Alloc(0x800);
        Memory.Write(obj + Offsets.UObjectInternalIndex, index);
        Memory.Write(obj + Offsets.UObjectEntityId, entityId);
        return (obj, PlaceObject(index, obj));
    }

    public uint AddName(string text, bool wide = false)
    {
        var index = (uint)(_nameCursor / Offsets.FNameEntryStride);
        var header = (ushort)((text.Length << Offsets.FNameLengthShift) | (wide ? Offsets.FNameWideBit : 0));
        Memory.Write(_nameBlock + _nameCursor, header);
        Memory.WriteArray(_nameBlock + _nameCursor + Offsets.FNameHeaderSize, System.Text.Encoding.ASCII.GetBytes(text));
        _nameCursor += (2 + text.Length + 1) & ~1;
        return index;
    }

    public nint AddClass(string name, bool wideName = false)
    {
        var cls = Alloc(0x80);
        Memory.Write(cls + Offsets.UObjectName, AddName(name, wideName));
        return cls;
    }

    public nint AddObject(nint cls, string name, uint nameNumber = 0)
    {
        var obj = Alloc(0x400);
        Memory.Write(obj + Offsets.UObjectClass, cls);
        Memory.Write(obj + Offsets.UObjectName, AddName(name));
        Memory.Write(obj + Offsets.UObjectName + Offsets.FNameNumber, nameNumber);
        PlaceObject(_nextObjectIndex++, obj);
        return obj;
    }

    public nint AddDropMgr(FakeNameTagGroup[] groups, string className = "DropMgr", string objectName = "DropMgr_0")
    {
        var manager = Alloc(Offsets.DropMgrNameTagGroups + Offsets.TMapHeaderSize + 0x10);
        Memory.Write(manager + Offsets.UObjectClass, AddClass(className));
        Memory.Write(manager + Offsets.UObjectName, AddName(objectName));
        PlaceObject(_nextObjectIndex++, manager);
        WriteTMap(manager + Offsets.DropMgrNameTagGroups, Offsets.NameTagGroupStride, groups.Length, _ => true, (at, i) =>
        {
            var group = groups[i];
            var value = at + Offsets.NameTagGroupValue;
            Memory.Write(at, group.Key);
            Memory.WriteArray(value + Offsets.NameTagGroupWorldPos, new[] { group.WorldPos.X, group.WorldPos.Y, group.WorldPos.Z });
            Memory.Write(value + Offsets.NameTagGroupInScreen, (byte)(group.InScreen ? 1 : 0));
            Memory.WriteArray(value + Offsets.NameTagGroupScreenPos, new[] { group.ScreenPos.X, group.ScreenPos.Y });
            WriteTMap(value + Offsets.NameTagGroupScreenRects, Offsets.ScreenRectStride, group.Labels.Length, j => group.Labels[j].Allocated, (rectAt, j) =>
            {
                var label = group.Labels[j];
                Memory.Write(rectAt, label.DropId);
                Memory.WriteArray(rectAt + Offsets.ScreenRectBox, new[] { label.Min.X, label.Min.Y, label.Max.X, label.Max.Y });
                Memory.Write(rectAt + Offsets.ScreenRectBox + 16, (byte)(label.Valid ? 1 : 0));
            });
        });
        return manager;
    }

    public nint[] AddComponents(nint actor, params string[] classNames)
    {
        var components = classNames.Select(name =>
        {
            var component = Alloc(0x400);
            Memory.Write(component + Offsets.UObjectClass, AddClass(name));
            return component;
        }).ToArray();
        WriteMapAt(actor + Offsets.EntityComponents, [.. components.Select((c, i) => ((int, nint)?)(i + 1, c))]);
        return components;
    }

    public FakeMinimap AddMinimap(float? rotate, string className = "MiniMap_C", string objectName = "MiniMap_C", bool placeInTable = true)
    {
        var widget = Alloc(0x900);
        Memory.Write(widget + Offsets.UObjectClass, AddClass(className));
        Memory.Write(widget + Offsets.UObjectName, AddName(objectName));
        if (placeInTable) PlaceObject(_nextObjectIndex++, widget);
        var material = Alloc(0x200);
        Memory.Write(widget + Offsets.MinimapWidgetMaterial, material);
        var parameters = new List<(string Name, float Value)> { ("Zoom", 0.84f) };
        if (rotate is { } turns) parameters.Add((Offsets.MinimapRotateParam, turns));
        parameters.Add(("MapResolution", 485.376f));
        var minimap = new FakeMinimap(widget, material);
        WriteScalarParameters(minimap, [.. parameters]);
        return minimap;
    }

    public void WriteScalarParameters(FakeMinimap minimap, params (string Name, float Value)[] parameters)
    {
        var data = Alloc(Math.Max(Offsets.ScalarParamStride, parameters.Length * Offsets.ScalarParamStride));
        for (var i = 0; i < parameters.Length; i++)
        {
            Memory.Write(data + i * Offsets.ScalarParamStride, AddName(parameters[i].Name));
            Memory.Write(data + i * Offsets.ScalarParamStride + Offsets.ScalarParamValue, parameters[i].Value);
        }

        Memory.Write(minimap.Material + Offsets.MaterialScalarParams, data);
        Memory.Write(minimap.Material + Offsets.MaterialScalarParams + Offsets.TArrayNum, parameters.Length);
    }

    public void SetMapIcons(FakeMinimap minimap, uint area, params FakeIcon[] icons)
    {
        Memory.Write(minimap.Widget + Offsets.MinimapIconArea, area);
        var data = Alloc(Math.Max(1, icons.Length) * Offsets.IconEntryStride);
        for (var i = 0; i < icons.Length; i++)
        {
            var icon = icons[i];
            var at = data + i * Offsets.IconEntryStride;
            Memory.Write(at + Offsets.IconEntryType, icon.Type);
            Memory.Write(at + Offsets.IconEntryX, icon.X);
            Memory.Write(at + Offsets.IconEntryY, icon.Y);
            Memory.Write(at + Offsets.IconEntryZorder, icon.Zorder);
            Memory.Write(at + Offsets.IconEntryKey, unchecked(icon.Type * Offsets.IconKeyStride + icon.EntityId));
            Memory.Write(at + Offsets.IconEntrySize, icon.Size);
            Memory.Write(at + Offsets.IconEntryArea, icon.Area ?? area);
            Memory.Write(at + Offsets.IconEntryRotation, icon.Rotation);
        }

        Memory.Write(minimap.Widget + Offsets.MinimapIconList, data);
        Memory.Write(minimap.Widget + Offsets.MinimapIconList + Offsets.TArrayNum, icons.Length);
    }

    public void SetQuestTargets(FakeMinimap minimap, uint area, params (int Id, float X, float Y)[] targets) =>
        SetQuestTargets(minimap, area, _ => true, [.. targets.Select(t => (t.Id, t.X, t.Y, area))]);

    public void SetQuestTargets(FakeMinimap minimap, uint area, Func<int, bool> present, params (int Id, float X, float Y, uint Area)[] targets)
    {
        Memory.Write(minimap.Widget + Offsets.MinimapIconArea, area);
        WriteTMap(minimap.Widget + Offsets.MinimapQuestTargets, Offsets.QuestTargetStride, targets.Length, present, (at, i) =>
        {
            Memory.Write(at + Offsets.QuestTargetId, targets[i].Id);
            Memory.Write(at + Offsets.QuestTargetX, targets[i].X);
            Memory.Write(at + Offsets.QuestTargetY, targets[i].Y);
            Memory.Write(at + Offsets.QuestTargetArea, targets[i].Area);
        });
    }

    public void SetFollowIcons(FakeMinimap minimap, params (int Id, uint Type, uint Area)[] follows)
    {
        WriteIdMap(minimap.Widget + Offsets.MinimapFollowTypes, [.. follows.Select(f => (f.Id, f.Type))]);
        WriteIdMap(minimap.Widget + Offsets.MinimapFollowAreas, [.. follows.Select(f => (f.Id, f.Area))]);
    }

    private void WriteIdMap(nint map, (int Id, uint Value)[] nodes)
    {
        const int bucketCount = 3;
        var heads = new nint[bucketCount];
        foreach (var (id, value) in nodes)
        {
            var node = Alloc(Offsets.FollowNodeSize);
            var bucket = (int)((uint)id % bucketCount);
            Memory.Write(node + Offsets.FollowNodeId, id);
            Memory.Write(node + Offsets.FollowNodeValue, value);
            Memory.Write(node + Offsets.FollowNodeNext, heads[bucket]);
            heads[bucket] = node;
        }

        var buckets = Alloc(bucketCount * 8);
        Memory.WriteArray(buckets, heads);
        Memory.Write(map, buckets);
        Memory.Write(map + Offsets.HashBucketCount, (uint)bucketCount);
    }

    public nint AddTexture(string name)
    {
        var texture = Alloc(0x40);
        Memory.Write(texture + Offsets.UObjectName, AddName(name));
        return texture;
    }

    public nint SetIconMap(FakeMinimap minimap, params (int Type, nint Texture)[] elements)
    {
        var map = Alloc(0x40);
        WriteTMap(map, Offsets.IconMapElementStride, elements.Length, _ => true, (at, i) =>
        {
            Memory.Write(at + Offsets.IconMapElementKey, elements[i].Type);
            Memory.Write(at + Offsets.IconMapElementTexture, elements[i].Texture);
        });
        Memory.Write(minimap.Widget + Offsets.MinimapActiveIconMap, map);
        return map;
    }

    public void SetLevel(int levelId, int instance)
    {
        if (!Memory.TryReadPtr(Scene + Offsets.SceneGeometry, out var geometry) || geometry == 0)
        {
            geometry = Alloc(0x100);
            Memory.Write(Scene + Offsets.SceneGeometry, geometry);
        }
        Memory.Write(geometry + Offsets.GeometryLevelId, levelId);
        Memory.Write(geometry + Offsets.GeometryLevelInstance, instance);
    }

    public void SetExplored(FakeMinimap minimap, float brushSize, int mapSide, params (string Key, Vector3[] Points)?[] records)
    {
        Memory.Write(minimap.Widget + Offsets.MinimapBrushSize, brushSize);
        Memory.Write(minimap.Widget + Offsets.MinimapMapSide, mapSide);
        var save = Alloc(0x100);
        WriteTMap(save + Offsets.SaveRecords, Offsets.SaveRecordStride, records.Length, i => records[i] is not null, (element, i) =>
        {
            if (records[i] is not { } record) return;
            var text = Alloc((record.Key.Length + 1) * sizeof(char));
            Memory.WriteArray(text, (record.Key + "\0").ToCharArray());
            Memory.Write(element, text);
            Memory.Write(element + Offsets.TArrayNum, record.Key.Length + 1);
            var points = Alloc(Math.Max(1, record.Points.Length) * Offsets.SavePointStride);
            for (var p = 0; p < record.Points.Length; p++)
                Memory.Write(points + p * Offsets.SavePointStride, record.Points[p]);
            Memory.Write(element + Offsets.SaveRecordPos, points);
            Memory.Write(element + Offsets.SaveRecordPos + Offsets.TArrayNum, record.Points.Length);
        });
        Memory.Write(minimap.Widget + Offsets.MinimapSaveObject, save);
    }

    public void SetGameMinimap(FakeMinimap minimap, byte mode, float zoom, int mapSide, float viewportScale = 0.64f)
    {
        Memory.Write(minimap.Widget + Offsets.MinimapMode, mode);
        Memory.Write(minimap.Widget + Offsets.MinimapBigZoom, zoom);
        Memory.Write(minimap.Widget + Offsets.MinimapMapSide, mapSide);
        Memory.Write(minimap.Widget + Offsets.MinimapViewportScale, viewportScale);
    }

    public void MoveEntity(FakeEntity entity, double x, double y, double z)
    {
        Memory.TryReadPtr(entity.Address + Offsets.EntityPosition, out var block);
        Memory.WriteArray(block + Offsets.PositionX, new[] { x, y, z });
    }

    public void UseSecondaryBits(int mapOffset, params bool[] bits)
    {
        var words = new uint[Math.Max(1, (bits.Length + 31) / 32)];
        for (var i = 0; i < bits.Length; i++)
            if (bits[i]) words[i / 32] |= 1u << (i % 32);
        var secondary = Alloc(words.Length * 4);
        Memory.WriteArray(secondary, words);
        Memory.Write(FightMgr + mapOffset + Offsets.TMapSecondaryBits, secondary);
    }

    public List<nint> ConfigNodes { get; } = [];

    public HashSet<uint> FriendlyConfigIds { get; } = [];

    public void SetMonsterConfigTable(int bucketCount, params (uint Id, uint Rarity)[] rows)
    {
        var entries = rows.Select(r =>
        {
            var row = Alloc(0x40);
            Memory.Write(row + Offsets.MonsterCfgRarity, r.Rarity);
            Memory.Write(row + Offsets.MonsterCfgFriendly, FriendlyConfigIds.Contains(r.Id) ? 1u : 0u);
            return (r.Id, row);
        }).ToArray();
        var (array, nodes) = WriteChains(bucketCount, entries);
        ConfigNodes.Clear();
        ConfigNodes.AddRange(nodes);
        Memory.Write(Module + Offsets.MonsterCfgBucketsRva, array);
        Memory.Write(Module + Offsets.MonsterCfgBucketCountRva, (uint)bucketCount);
    }

    private readonly Dictionary<uint, nint> _mapIconInfoRows = [];

    public void SetMapIconInfoTable(int bucketCount, params (uint Type, string? Enum)[] rows)
    {
        _mapIconInfoRows.Clear();
        var entries = rows.Select(r =>
        {
            if (r.Enum is null) return (r.Type, (nint)0);
            var row = Alloc(0x40);
            var bytes = System.Text.Encoding.UTF8.GetBytes(r.Enum);
            var data = Alloc(Math.Max(8, bytes.Length));
            Memory.WriteArray(data, bytes);
            Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringData, data);
            Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringLength, (long)bytes.Length);
            _mapIconInfoRows[r.Type] = row;
            return (r.Type, row);
        }).ToArray();
        var (array, _) = WriteChains(bucketCount, entries);
        Memory.Write(Module + Offsets.MapIconInfoBucketsRva, array);
        Memory.Write(Module + Offsets.MapIconInfoBucketCountRva, (uint)bucketCount);
    }

    public void SetMapIconInfoRaw(uint type, nint data, long length)
    {
        var row = _mapIconInfoRows[type];
        Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringData, data);
        Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringLength, length);
    }

    public void BreakMonsterConfigTable() => Memory.Write(Module + Offsets.MonsterCfgBucketsRva, 0L);

    public void BreakPlayerMgr() => Memory.Write(Module + Offsets.PlayerMgrGlobalRva, 0L);

    public void PublishEntities(params FakeEntity[] entities) => PublishEntities(3, entities);

    public void PublishEntities(int bucketCount, params FakeEntity[] entities)
    {
        foreach (var group in entities.Where(e => e.MapOffset != 0).GroupBy(e => e.MapOffset))
            WriteMap(group.Key, group.Select(e => ((int, nint)?)((int)e.Id, e.Value)).ToArray());
        var (array, nodes) = WriteChains(bucketCount, entities.Select(e => (e.Id, e.Address)));
        Nodes.Clear();
        Nodes.AddRange(nodes);
        Memory.Write(Scene + Offsets.SceneEntityBuckets, array);
        Memory.Write(Scene + Offsets.SceneEntityBuckets + Offsets.HashBucketCount, (uint)bucketCount);
    }

    public nint GridAddress { get; private set; }

    public void SetGrid(int cols, int rows, byte[] clearance, int cellSize = 32, double originX = 0, double originY = 0, (int BlockW, int BlockH, int BlocksPerRow, byte[] Mask)? hybrid = null, byte[]? secondClearance = null)
    {
        var geometry = Alloc(0x100);
        var module = Alloc(0x100);
        var dense = Alloc(Math.Max(1, cols * rows * Offsets.CellStride));
        GridAddress = module + Offsets.GridModuleHybrid;
        Memory.Write(Scene + Offsets.SceneGeometry, geometry);
        Memory.Write(Scene + Offsets.SceneGridModule, module);
        Memory.Write(geometry + Offsets.GeometryCellSize, cellSize);
        Memory.Write(geometry + Offsets.GeometryOriginX, originX);
        Memory.Write(geometry + Offsets.GeometryOriginY, originY);
        Memory.Write(GridAddress + Offsets.HybridCols, cols);
        Memory.Write(GridAddress + Offsets.HybridRows, rows);
        Memory.Write(GridAddress + Offsets.HybridCells, dense);
        for (var i = 0; i < clearance.Length; i++)
        {
            Memory.Write(dense + i * Offsets.CellStride + Offsets.CellClearance, clearance[i]);
            Memory.Write(dense + i * Offsets.CellStride + Offsets.CellClearanceSecond, (secondClearance ?? clearance)[i]);
        }
        Memory.Write(GridAddress + Offsets.HybridIsHybrid, (byte)(hybrid is null ? 0 : 1));
        if (hybrid is not { } h) return;
        var mask = Alloc(h.Mask.Length);
        Memory.WriteArray(mask, h.Mask);
        Memory.Write(GridAddress + Offsets.HybridBlockW, h.BlockW);
        Memory.Write(GridAddress + Offsets.HybridBlockH, h.BlockH);
        Memory.Write(GridAddress + Offsets.HybridBlocksPerRow, h.BlocksPerRow);
        Memory.Write(GridAddress + Offsets.HybridMask, mask);
        Memory.Write(GridAddress + Offsets.HybridBlockCount, h.Mask.Length);
    }

    public void BreakWorldPointer() => Memory.Write(Module + Offsets.LogicWorldGlobalRva, 0L);

    public void BreakScenePointer() => Memory.Write(_worldAddress + Offsets.WorldScene, 0L);
}
