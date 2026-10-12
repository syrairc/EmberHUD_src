using System.Numerics;
using Aember.Host;
using Aember.Host.Memory;

namespace Aember.Tests;

internal readonly record struct FakeEntity(uint Id, long Address, int MapOffset = 0, long Value = 0);

internal readonly record struct FakeMinimap(long Widget, long Material);

internal readonly record struct FakeIcon(int Type, float X, float Y, int Zorder = 0, int EntityId = 0, float Size = 1, uint? Area = null, float Rotation = 0);

internal sealed class FakeWorld
{
    public const long ModuleBase = 0x1_4000_0000;
    public const int Unregistered = 0;

    private readonly long _worldAddress;
    private long _next = 0x2_0000_0000;
    private readonly long _nameBlock;
    private int _nameCursor = 2;
    private readonly Dictionary<long, long> _objects = [];
    private long _nextObjectIndex = 1000;

    public FakeWorld()
    {
        Memory.Map(ModuleBase + Offsets.LogicWorldGlobalRva, 8);
        _worldAddress = Alloc(0x100);
        Scene = Alloc(0x800);
        Memory.Write(ModuleBase + Offsets.LogicWorldGlobalRva, _worldAddress);
        Memory.Write(_worldAddress + Offsets.WorldScene, Scene);
        Memory.Map(ModuleBase + Offsets.FightMgrGlobalRva, 8);
        FightMgr = Alloc(0x1000);
        Memory.Write(ModuleBase + Offsets.FightMgrGlobalRva, FightMgr);
        Memory.Map(ModuleBase + Offsets.PlayerMgrGlobalRva, 8);
        Memory.Map(ModuleBase + Offsets.MiniMapMgrGlobalRva, 8);
        Memory.Map(ModuleBase + Offsets.GObjectsGlobalRva, 0x20);
        Memory.Map(ModuleBase + Offsets.FNamePoolRva, Offsets.FNameBlocks + 8 * 4);
        _nameBlock = Alloc(Offsets.FNameBlockSize * Offsets.FNameEntryStride);
        Memory.Write(ModuleBase + Offsets.FNamePoolRva + Offsets.FNameBlocks, _nameBlock);
    }

    public long FightMgr { get; }

    public FakeMemory Memory { get; } = new();

    public long Scene { get; }

    public long Alloc(int size)
    {
        var address = _next;
        _next += (size + 0xFFF) & ~0xFFFL;
        Memory.Map(address, size);
        return address;
    }

    public List<long> Nodes { get; } = [];

    public FakeEntity AddEntity(uint id, uint cfgId, bool alive, double x, double y, double z, bool hasPosition = true, int? mapOffset = null, uint? monsterCfgId = null, int? role = null)
    {
        var map = mapOffset ?? Offsets.FightMapMonster;
        var entity = Alloc(0x400);
        var block = Alloc(0x80);
        Memory.Write(entity + Offsets.EntityCfgId, cfgId);
        Memory.Write(entity + Offsets.EntityAlive, (byte)(alive ? 1 : 0));
        Memory.Write(entity + Offsets.EntityHasPosition, (byte)(hasPosition ? 1 : 0));
        Memory.Write(entity + Offsets.EntityPosition, block);
        Memory.WriteArray(block + Offsets.PositionX, new[] { x, y, z });
        if (role is { } roleValue)
        {
            var roleBlock = Alloc(0x80);
            Memory.Write(entity + Offsets.EntityRole, roleBlock);
            Memory.Write(roleBlock + Offsets.RoleRarity, roleValue);
        }

        var value = map == Unregistered ? 0 : Alloc(0x400);
        if (value != 0 && monsterCfgId is { } monsterCfg)
        {
            var record = Alloc(0x200);
            Memory.Write(value + Offsets.MonsterRecord, record);
            Memory.Write(record + Offsets.MonsterRecordCfgId, monsterCfg);
        }

        return new FakeEntity(id, entity, map, value);
    }

    public void WriteMap(int mapOffset, params (int Key, long Value)?[] slots)
    {
        var map = FightMgr + mapOffset;
        var data = Alloc(Math.Max(Offsets.TMapElementStride, slots.Length * Offsets.TMapElementStride));
        var bits = new uint[Math.Max(4, (slots.Length + 31) / 32)];
        for (var i = 0; i < slots.Length; i++)
        {
            var (key, value) = slots[i] ?? (-1, 0x1111);
            Memory.Write(data + i * Offsets.TMapElementStride, key);
            Memory.Write(data + i * Offsets.TMapElementStride + Offsets.TMapElementValue, value);
            if (slots[i] is not null) bits[i / 32] |= 1u << (i % 32);
        }

        Memory.Write(map, data);
        Memory.Write(map + Offsets.TMapNum, slots.Length);
        if (slots.Length <= Offsets.TMapInlineBitCount)
        {
            Memory.WriteArray(map + Offsets.TMapInlineBits, bits[..4]);
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
        Memory.Write(manager + Offsets.MiniMapMgrWidget + 4, weakSerial ?? serial);
        Memory.Write(ModuleBase + Offsets.MiniMapMgrGlobalRva, manager);
    }

    public long PlaceObject(long index, long obj)
    {
        _objects[index] = obj;
        var count = _objects.Keys.Max() + 1;
        var chunkCount = (int)((count - 1) / Offsets.GObjectsPerChunk) + 1;
        var chunks = Alloc(chunkCount * 8);
        for (var c = 0; c < chunkCount; c++)
        {
            var inChunk = _objects.Where(o => o.Key / Offsets.GObjectsPerChunk == c).ToArray();
            if (inChunk.Length == 0) continue;
            var size = c < chunkCount - 1 ? Offsets.GObjectsPerChunk : (int)(count - (long)c * Offsets.GObjectsPerChunk);
            var chunk = Alloc(size * Offsets.GObjectsItemSize);
            foreach (var (i, o) in inChunk)
            {
                Memory.Write(chunk + (i % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize, o);
                Memory.Write(chunk + (i % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize + Offsets.GObjectsItemSerial, _serials.GetValueOrDefault(i));
            }
            Memory.Write(chunks + c * 8, chunk);
        }

        Memory.Write(ModuleBase + Offsets.GObjectsGlobalRva, chunks);
        Memory.Write(ModuleBase + Offsets.GObjectsGlobalRva + Offsets.GObjectsNumElements, (int)count);
        return chunks;
    }

    public (long Object, long Chunks) SetLocalPlayer(uint entityId, uint index = 77)
    {
        var manager = Alloc(0x100);
        Memory.Write(ModuleBase + Offsets.PlayerMgrGlobalRva, manager);
        Memory.WriteArray(manager + Offsets.PlayerMgrEntity, new[] { index, 1u });
        var obj = Alloc(0x800);
        Memory.Write(obj + Offsets.UObjectInternalIndex, index);
        Memory.Write(obj + Offsets.UObjectEntityId, entityId);
        return (obj, PlaceObject(index, obj));
    }

    public uint AddName(string text, bool wide = false)
    {
        var index = (uint)(_nameCursor / Offsets.FNameEntryStride);
        var header = (ushort)((text.Length << Offsets.FNameLengthShift) | (wide ? 1 : 0));
        Memory.Write(_nameBlock + _nameCursor, header);
        Memory.WriteArray(_nameBlock + _nameCursor + 2, System.Text.Encoding.ASCII.GetBytes(text));
        _nameCursor += (2 + text.Length + 1) & ~1;
        return index;
    }

    public long AddClass(string name, bool wideName = false)
    {
        var cls = Alloc(0x80);
        Memory.Write(cls + Offsets.UObjectName, AddName(name, wideName));
        return cls;
    }

    public long AddObject(long cls, string name, uint nameNumber = 0)
    {
        var obj = Alloc(0x400);
        Memory.Write(obj + Offsets.UObjectClass, cls);
        Memory.Write(obj + Offsets.UObjectName, AddName(name));
        Memory.Write(obj + Offsets.UObjectName + Offsets.FNameNumber, nameNumber);
        PlaceObject(_nextObjectIndex++, obj);
        return obj;
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

    public void SetTaskTypes(FakeMinimap minimap, int[] a, int[] b, int[] c)
    {
        foreach (var (offset, types) in new[] { (Offsets.MinimapTaskTypesA, a), (Offsets.MinimapTaskTypesB, b), (Offsets.MinimapTaskTypesC, c) })
        {
            var data = Alloc(Math.Max(1, types.Length) * sizeof(int));
            if (types.Length > 0) Memory.WriteArray(data, types);
            Memory.Write(minimap.Widget + offset, data);
            Memory.Write(minimap.Widget + offset + Offsets.TArrayNum, types.Length);
        }
    }

    public void SetFollowIcons(FakeMinimap minimap, params (int Id, uint Type, uint Area)[] follows)
    {
        WriteIdMap(minimap.Widget + Offsets.MinimapFollowTypes, [.. follows.Select(f => (f.Id, f.Type))]);
        WriteIdMap(minimap.Widget + Offsets.MinimapFollowAreas, [.. follows.Select(f => (f.Id, f.Area))]);
    }

    private void WriteIdMap(long map, (int Id, uint Value)[] nodes)
    {
        const int bucketCount = 3;
        var heads = new long[bucketCount];
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
        Memory.Write(map + Offsets.FollowMapBucketCount, (uint)bucketCount);
    }

    public long AddTexture(string name)
    {
        var texture = Alloc(0x40);
        Memory.Write(texture + Offsets.UObjectName, AddName(name));
        return texture;
    }

    public long SetIconMap(FakeMinimap minimap, params (int Type, long Texture)[] elements)
    {
        var map = Alloc(0x40);
        var data = Alloc(Math.Max(1, elements.Length) * Offsets.IconMapElementStride);
        for (var i = 0; i < elements.Length; i++)
        {
            Memory.Write(data + i * Offsets.IconMapElementStride + Offsets.IconMapElementKey, elements[i].Type);
            Memory.Write(data + i * Offsets.IconMapElementStride + Offsets.IconMapElementTexture, elements[i].Texture);
        }

        Memory.Write(map, data);
        Memory.Write(map + Offsets.TMapNum, elements.Length);
        Memory.Write(minimap.Widget + Offsets.MinimapActiveIconMap, map);
        return map;
    }

    public void SetLevel(int levelId, int instance)
    {
        if (!Memory.TryReadPtr((nint)(Scene + Offsets.SceneGeometry), out var geometry) || geometry == 0)
        {
            geometry = (nint)Alloc(0x100);
            Memory.Write(Scene + Offsets.SceneGeometry, (long)geometry);
        }
        Memory.Write(geometry + Offsets.GeometryLevelId, levelId);
        Memory.Write(geometry + Offsets.GeometryLevelInstance, instance);
    }

    public void SetExplored(FakeMinimap minimap, float brushSize, int mapSide, params (string Key, Vector3[] Points)?[] records)
    {
        Memory.Write(minimap.Widget + Offsets.MinimapBrushSize, brushSize);
        Memory.Write(minimap.Widget + Offsets.MinimapMapSide, mapSide);
        var save = Alloc(0x100);
        var map = save + Offsets.SaveRecords;
        var data = Alloc(Math.Max(1, records.Length) * Offsets.SaveRecordStride);
        var bits = new uint[Offsets.TMapInlineBitCount / 32];
        for (var i = 0; i < records.Length; i++)
        {
            if (records[i] is not { } record) continue;
            bits[i / 32] |= 1u << (i % 32);
            var element = data + i * Offsets.SaveRecordStride;
            var text = Alloc((record.Key.Length + 1) * sizeof(char));
            Memory.WriteArray(text, (record.Key + "\0").ToCharArray());
            Memory.Write(element, text);
            Memory.Write(element + Offsets.TArrayNum, record.Key.Length + 1);
            var points = Alloc(Math.Max(1, record.Points.Length) * Offsets.SavePointStride);
            Memory.WriteArray(points, record.Points);
            Memory.Write(element + Offsets.SaveRecordPos, points);
            Memory.Write(element + Offsets.SaveRecordPos + Offsets.TArrayNum, record.Points.Length);
        }

        Memory.Write(map, data);
        Memory.Write(map + Offsets.TMapNum, records.Length);
        Memory.WriteArray(map + Offsets.TMapInlineBits, bits);
        Memory.Write(minimap.Widget + Offsets.MinimapSaveObject, save);
    }

    public void MoveEntity(FakeEntity entity, double x, double y, double z)
    {
        Memory.TryReadPtr(unchecked((nint)(entity.Address + Offsets.EntityPosition)), out var block);
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

    private bool _configTableMapped;

    public List<long> ConfigNodes { get; } = [];

    public void SetMonsterConfigTable(int bucketCount, params (uint Id, uint Rarity)[] rows)
    {
        if (!_configTableMapped)
        {
            Memory.Map(ModuleBase + Offsets.MonsterCfgBucketsRva, 8);
            Memory.Map(ModuleBase + Offsets.MonsterCfgBucketCountRva, 4);
            _configTableMapped = true;
        }

        var buckets = new long[bucketCount];
        ConfigNodes.Clear();
        foreach (var (id, rarity) in rows)
        {
            var row = Alloc(0x40);
            Memory.Write(row + Offsets.MonsterCfgRarity, rarity);
            var node = Alloc(0x20);
            var bucket = (int)(id % bucketCount);
            Memory.Write(node + Offsets.NodeKey, id);
            Memory.Write(node + Offsets.NodeEntity, row);
            Memory.Write(node + Offsets.NodeNext, buckets[bucket]);
            buckets[bucket] = node;
            ConfigNodes.Add(node);
        }

        var array = Alloc(Math.Max(8, bucketCount * 8));
        Memory.WriteArray(array, buckets);
        Memory.Write(ModuleBase + Offsets.MonsterCfgBucketsRva, array);
        Memory.Write(ModuleBase + Offsets.MonsterCfgBucketCountRva, (uint)bucketCount);
    }

    private bool _mapIconInfoMapped;
    private readonly Dictionary<uint, long> _mapIconInfoRows = [];

    public void SetMapIconInfoTable(int bucketCount, params (uint Type, string? Enum)[] rows)
    {
        if (!_mapIconInfoMapped)
        {
            Memory.Map(ModuleBase + Offsets.MapIconInfoBucketsRva, 8);
            Memory.Map(ModuleBase + Offsets.MapIconInfoBucketCountRva, 4);
            _mapIconInfoMapped = true;
        }

        var buckets = new long[bucketCount];
        _mapIconInfoRows.Clear();
        foreach (var (type, name) in rows)
        {
            var row = 0L;
            if (name is not null)
            {
                row = Alloc(0x40);
                var bytes = System.Text.Encoding.UTF8.GetBytes(name);
                var data = Alloc(Math.Max(8, bytes.Length));
                Memory.WriteArray(data, bytes);
                Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringData, data);
                Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringLength, (long)bytes.Length);
                _mapIconInfoRows[type] = row;
            }

            var node = Alloc(0x20);
            var bucket = (int)(type % bucketCount);
            Memory.Write(node + Offsets.NodeKey, type);
            Memory.Write(node + Offsets.NodeEntity, row);
            Memory.Write(node + Offsets.NodeNext, buckets[bucket]);
            buckets[bucket] = node;
        }

        var array = Alloc(Math.Max(8, bucketCount * 8));
        Memory.WriteArray(array, buckets);
        Memory.Write(ModuleBase + Offsets.MapIconInfoBucketsRva, array);
        Memory.Write(ModuleBase + Offsets.MapIconInfoBucketCountRva, (uint)bucketCount);
    }

    public void SetMapIconInfoRaw(uint type, long data, long length)
    {
        var row = _mapIconInfoRows[type];
        Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringData, data);
        Memory.Write(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringLength, length);
    }

    public void BreakMonsterConfigTable() => Memory.Write(ModuleBase + Offsets.MonsterCfgBucketsRva, 0L);

    public void BreakPlayerMgr() => Memory.Write(ModuleBase + Offsets.PlayerMgrGlobalRva, 0L);

    public void PublishEntities(params FakeEntity[] entities) => PublishEntities(3, entities);

    public void PublishEntities(int bucketCount, params FakeEntity[] entities)
    {
        foreach (var group in entities.Where(e => e.MapOffset != 0).GroupBy(e => e.MapOffset))
            WriteMap(group.Key, group.Select(e => ((int, long)?)((int)e.Id, e.Value)).ToArray());
        var buckets = new long[bucketCount];
        Nodes.Clear();
        foreach (var (id, address, _, _) in entities)
        {
            var node = Alloc(0x20);
            var bucket = (int)(id % bucketCount);
            Memory.Write(node + Offsets.NodeKey, id);
            Memory.Write(node + Offsets.NodeEntity, address);
            Memory.Write(node + 16, (long)id);
            Memory.Write(node + Offsets.NodeNext, buckets[bucket]);
            buckets[bucket] = node;
            Nodes.Add(node);
        }

        var array = Alloc(Math.Max(8, bucketCount * 8));
        Memory.WriteArray(array, buckets);
        Memory.Write(Scene + Offsets.SceneEntityBuckets, array);
        Memory.Write(Scene + Offsets.SceneEntityBucketCount, (uint)bucketCount);
    }

    public long GridAddress { get; private set; }

    public void SetGrid(int cols, int rows, byte[] clearance, int cellSize = 32, double oddRowShift = 16, double originX = 0, double originY = 0, (int BlockW, int BlockH, int BlocksPerRow, byte[] Mask)? hybrid = null, byte[]? secondClearance = null)
    {
        var geometry = Alloc(0x100);
        var module = Alloc(0x100);
        var dense = Alloc(Math.Max(1, cols * rows * Offsets.CellStride));
        GridAddress = module + Offsets.GridModuleHybrid;
        Memory.Write(Scene + Offsets.SceneGeometry, geometry);
        Memory.Write(Scene + Offsets.SceneGridModule, module);
        Memory.Write(geometry + Offsets.GeometryCellSize, cellSize);
        Memory.Write(geometry + Offsets.GeometryOddRowShift, oddRowShift);
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

    public void BreakWorldPointer() => Memory.Write(ModuleBase + Offsets.LogicWorldGlobalRva, 0L);

    public void BreakScenePointer() => Memory.Write(_worldAddress + Offsets.WorldScene, 0L);
}
