using System.Collections;
using System.Numerics;

namespace EmberHUD;

public readonly record struct WallSegment(float Ax, float Ay, float Bx, float By);

public sealed record Terrain(int Cols, int Rows, float CellSize, Vector2 Origin, BitArray Walkable)
{
    public bool IsWalkable(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows) return false;
        var index = row * Cols + col;
        return index < Walkable.Length && Walkable[index];
    }

    public Vector2 CellCenter(int col, int row) => new(Origin.X + (col + 0.5f) * CellSize, Origin.Y + (row + 0.5f) * CellSize);

    public List<WallSegment> Walls(Func<int, int, bool>? inside = null)
    {
        inside ??= IsWalkable;
        var walls = new List<WallSegment>();
        if (!float.IsFinite(CellSize) || CellSize <= 0) return walls;

        bool In(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows && inside(col, row);

        for (var row = 0; row < Rows; row++)
        {
            var y0 = Origin.Y + row * CellSize;
            var y1 = y0 + CellSize;
            for (var col = 0; col < Cols; col++)
            {
                if (!In(col, row)) continue;
                var x0 = Origin.X + col * CellSize;
                var x1 = x0 + CellSize;
                if (!In(col - 1, row)) walls.Add(new(x0, y0, x0, y1));
                if (!In(col + 1, row)) walls.Add(new(x1, y0, x1, y1));
                if (!In(col, row - 1)) walls.Add(new(x0, y0, x1, y0));
                if (!In(col, row + 1)) walls.Add(new(x0, y1, x1, y1));
            }
        }
        return walls;
    }
}
