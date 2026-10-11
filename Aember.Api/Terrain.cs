using System.Collections;
using System.Numerics;

namespace Aember;

public sealed record Terrain(int Cols, int Rows, float CellSize, Vector2 Origin, bool OddRowShift, BitArray Walkable)
{
    public bool IsWalkable(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows) return false;
        var index = row * Cols + col;
        return index < Walkable.Length && Walkable[index];
    }

    public Vector2 CellCenter(int col, int row)
    {
        var shift = OddRowShift && (row & 1) == 1 ? 0.5f : 0f;
        return new Vector2(Origin.X + (col + 0.5f + shift) * CellSize, Origin.Y + (row + 0.5f) * CellSize);
    }
}
