using TorchlightSDK.Host;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Tests;

internal readonly record struct FakeEntity(uint Id, long Address, int MapOffset = 0, long Value = 0);

internal sealed class FakeWorld
{
    public const long ModuleBase = 0x1_4000_0000;
    public const int Unregistered = 0;

    private readonly long _worldAddress;
    private long _next = 0x2_0000_0000;

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
        Memory.Map(ModuleBase + Offsets.GObjectsGlobalRva, 8);
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

    public (long Object, long Chunks) SetLocalPlayer(uint entityId, uint index = 77)
    {
        var manager = Alloc(0x100);
        Memory.Write(ModuleBase + Offsets.PlayerMgrGlobalRva, manager);
        Memory.WriteArray(manager + Offsets.PlayerMgrEntity, new[] { index, 1u });
        var chunkIndex = (int)(index / Offsets.GObjectsPerChunk);
        var slot = (int)(index % Offsets.GObjectsPerChunk);
        var chunks = Alloc((chunkIndex + 1) * 8);
        var chunk = Alloc((slot + 1) * Offsets.GObjectsItemSize);
        var obj = Alloc(0x800);
        Memory.Write(ModuleBase + Offsets.GObjectsGlobalRva, chunks);
        Memory.Write(chunks + chunkIndex * 8, chunk);
        Memory.Write(chunk + slot * Offsets.GObjectsItemSize, obj);
        Memory.Write(obj + Offsets.UObjectInternalIndex, index);
        Memory.Write(obj + Offsets.UObjectEntityId, entityId);
        return (obj, chunks);
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

    public void SetGrid(int cols, int rows, byte[] clearance, int cellSize = 32, double oddRowShift = 16, double originX = 0, double originY = 0, (int BlockW, int BlockH, int BlocksPerRow, byte[] Mask)? hybrid = null)
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
        for (var i = 0; i < clearance.Length; i++) Memory.Write(dense + i * Offsets.CellStride + Offsets.CellClearance, clearance[i]);
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
