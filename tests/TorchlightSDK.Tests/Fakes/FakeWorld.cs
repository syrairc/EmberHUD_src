using TorchlightSDK.Host;

namespace TorchlightSDK.Tests;

internal readonly record struct FakeEntity(uint Id, long Address);

internal sealed class FakeWorld
{
    public const long ModuleBase = 0x1_4000_0000;

    private readonly long _worldAddress;
    private long _next = 0x2_0000_0000;

    public FakeWorld()
    {
        Memory.Map(ModuleBase + Offsets.LogicWorldGlobalRva, 8);
        _worldAddress = Alloc(0x100);
        Scene = Alloc(0x800);
        Memory.Write(ModuleBase + Offsets.LogicWorldGlobalRva, _worldAddress);
        Memory.Write(_worldAddress + Offsets.WorldScene, Scene);
    }

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

    public FakeEntity AddEntity(uint id, uint cfgId, bool alive, double x, double y, double z, bool hasPosition = true, bool hasRole = true)
    {
        var entity = Alloc(0x400);
        var block = Alloc(0x80);
        Memory.Write(entity + Offsets.EntityCfgId, cfgId);
        Memory.Write(entity + Offsets.EntityAlive, (byte)(alive ? 1 : 0));
        Memory.Write(entity + Offsets.EntityHasRole, (byte)(hasRole ? 1 : 0));
        Memory.Write(entity + Offsets.EntityHasPosition, (byte)(hasPosition ? 1 : 0));
        Memory.Write(entity + Offsets.EntityPosition, block);
        Memory.WriteArray(block + Offsets.PositionX, new[] { x, y, z });
        return new FakeEntity(id, entity);
    }

    public void PublishEntities(params FakeEntity[] entities) => PublishEntities(3, entities);

    public void PublishEntities(int bucketCount, params FakeEntity[] entities)
    {
        var buckets = new long[bucketCount];
        Nodes.Clear();
        foreach (var (id, address) in entities)
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
