using System.Collections;
using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class TerrainReader(IReadBackend memory, int firstGeneration = 0)
{
    internal const int RetryEveryReads = 30;

    private readonly record struct GridKey(int Cols, int Rows, int CellSize, Vector2 Origin);

    private readonly record struct Header(GridKey Key, nint Grid, nint Cells, bool IsHybrid);

    private GridKey _key;
    private GridKey? _failedKey;
    private int _skips;
    private Terrain? _terrain;

    public int Generation { get; private set; } = firstGeneration;

    public Terrain? Read(nint geometry, nint gridModule)
    {
        if (!TryReadHeader(geometry, gridModule, out var header))
            return Forget();

        if (_terrain is not null && header.Key == _key)
            return _terrain;

        if (_failedKey == header.Key && ++_skips < RetryEveryReads)
            return null;

        var terrain = Build(header);
        if (terrain is null)
        {
            Forget();
            _failedKey = header.Key;
            _skips = 0;
            return null;
        }

        _key = header.Key;
        _terrain = terrain;
        Generation++;
        return terrain;
    }

    private Terrain? Forget()
    {
        _key = default;
        _terrain = null;
        _failedKey = null;
        return null;
    }

    private bool TryReadHeader(nint geometry, nint gridModule, out Header header)
    {
        header = default;
        if (geometry == 0 || gridModule == 0)
            return false;

        var grid = gridModule + Offsets.GridModuleHybrid;
        Span<byte> geometryBlock = stackalloc byte[Offsets.GeometryHeaderSize];
        Span<byte> gridBlock = stackalloc byte[Offsets.HybridHeaderSize];
        if (!memory.TryRead(geometry, geometryBlock) || !memory.TryRead(grid, gridBlock))
            return false;

        var cellSize = BitConverter.ToInt32(geometryBlock[Offsets.GeometryCellSize..]);
        var originX = BitConverter.ToDouble(geometryBlock[Offsets.GeometryOriginX..]);
        var originY = BitConverter.ToDouble(geometryBlock[Offsets.GeometryOriginY..]);
        var cols = BitConverter.ToInt32(gridBlock[Offsets.HybridCols..]);
        var rows = BitConverter.ToInt32(gridBlock[Offsets.HybridRows..]);
        var cells = (nint)BitConverter.ToInt64(gridBlock[Offsets.HybridCells..]);
        if (cells == 0 || cols <= 0 || rows <= 0 || (long)cols * rows > Offsets.MaxCells || cellSize <= 0 ||
            !double.IsFinite(originX) || !double.IsFinite(originY))
            return false;

        header = new Header(new GridKey(cols, rows, cellSize, new Vector2((float)originX, (float)originY)), grid, cells, gridBlock[Offsets.HybridIsHybrid] != 0);
        return true;
    }

    private Terrain? Build(Header header)
    {
        var (cols, rows, cellSize, origin) = header.Key;
        var count = cols * rows;
        if (!memory.TryReadArray(header.Cells, count * Offsets.CellStride, out byte[] dense))
            return null;

        var walkable = new BitArray(count);
        for (var i = 0; i < count; i++)
            walkable[i] = Math.Min(dense[i * Offsets.CellStride + Offsets.CellClearance], dense[i * Offsets.CellStride + Offsets.CellClearanceSecond]) >= 1;

        if (header.IsHybrid && !ClearUniformBlocks(header, walkable))
            return null;

        return new Terrain(cols, rows, cellSize, origin, walkable);
    }

    private bool ClearUniformBlocks(Header header, BitArray walkable)
    {
        var grid = header.Grid;
        if (!memory.TryRead(grid + Offsets.HybridBlockW, out int blockW) ||
            !memory.TryRead(grid + Offsets.HybridBlockH, out int blockH) ||
            !memory.TryRead(grid + Offsets.HybridBlocksPerRow, out int blocksPerRow) ||
            !memory.TryRead(grid + Offsets.HybridBlockCount, out int blockCount) ||
            !memory.TryReadPtr(grid + Offsets.HybridMask, out var maskAddress))
            return false;

        if (blockW <= 0 || blockH <= 0 || blocksPerRow <= 0 || blockCount <= 0 || blockCount > Offsets.MaxCells ||
            !memory.TryReadArray(maskAddress, blockCount, out byte[] mask))
            return false;

        var (cols, rows, _, _) = header.Key;
        for (var row = 0; row < rows; row++)
            for (var col = 0; col < cols; col++)
            {
                var block = (long)(row / blockH) * blocksPerRow + col / blockW;
                if (block >= blockCount) return false;
                if (mask[block] != 0) walkable[row * cols + col] = false;
            }

        return true;
    }
}
