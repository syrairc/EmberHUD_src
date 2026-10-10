using System.Collections;
using System.Numerics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class TerrainReader(IReadBackend memory, int firstGeneration = 0)
{
    private readonly record struct GridKey(nint Cells, int Cols, int Rows);

    private readonly record struct Header(GridKey Key, nint Grid, bool IsHybrid, int CellSize, Vector2 Origin, bool OddRowShift);

    private GridKey _key;
    private Terrain? _terrain;

    public int Generation { get; private set; } = firstGeneration;

    public Terrain? Read(nint scene)
    {
        if (!TryReadHeader(scene, out var header))
            return Forget();

        if (_terrain is not null && header.Key == _key)
            return _terrain;

        var terrain = Build(header);
        if (terrain is null)
            return Forget();

        _key = header.Key;
        _terrain = terrain;
        Generation++;
        return terrain;
    }

    private Terrain? Forget()
    {
        _key = default;
        _terrain = null;
        return null;
    }

    private bool TryReadHeader(nint scene, out Header header)
    {
        header = default;
        if (!memory.TryReadPtr(scene + Offsets.SceneGeometry, out var geometry) ||
            !memory.TryReadPtr(scene + Offsets.SceneGridModule, out var module) ||
            !memory.TryRead(geometry + Offsets.GeometryCellSize, out int cellSize) ||
            !memory.TryRead(geometry + Offsets.GeometryOddRowShift, out double shift) ||
            !memory.TryRead(geometry + Offsets.GeometryOriginX, out double originX) ||
            !memory.TryRead(geometry + Offsets.GeometryOriginY, out double originY))
            return false;

        var grid = module + Offsets.GridModuleHybrid;
        if (!memory.TryRead(grid + Offsets.HybridCols, out int cols) ||
            !memory.TryRead(grid + Offsets.HybridRows, out int rows) ||
            !memory.TryReadPtr(grid + Offsets.HybridCells, out var cells) ||
            !memory.TryRead(grid + Offsets.HybridIsHybrid, out byte hybrid))
            return false;

        if (cols <= 0 || rows <= 0 || (long)cols * rows > Offsets.MaxCells || cellSize <= 0 ||
            !double.IsFinite(shift) || !double.IsFinite(originX) || !double.IsFinite(originY))
            return false;

        header = new Header(new GridKey(cells, cols, rows), grid, hybrid != 0, cellSize, new Vector2((float)originX, (float)originY), shift != 0);
        return true;
    }

    private Terrain? Build(Header header)
    {
        var (cells, cols, rows) = header.Key;
        var count = cols * rows;
        if (!memory.TryReadArray(cells, count * Offsets.CellStride, out byte[] dense))
            return null;

        var walkable = new BitArray(count);
        for (var i = 0; i < count; i++)
            walkable[i] = dense[i * Offsets.CellStride + Offsets.CellClearance] >= 1;

        if (header.IsHybrid && !ClearUniformBlocks(header, walkable))
            return null;

        return new Terrain(cols, rows, header.CellSize, header.Origin, header.OddRowShift, walkable);
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

        var (_, cols, rows) = header.Key;
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
