using System.Collections;
using System.Numerics;

namespace EmberHUD;

public sealed record Terrain(int Cols, int Rows, float CellSize, Vector2 Origin, BitArray Walkable)
{
    public bool IsWalkable(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows) return false;
        var index = row * Cols + col;
        return index < Walkable.Length && Walkable[index];
    }

    public Vector2 CellCenter(int col, int row) => new(Origin.X + (col + 0.5f) * CellSize, Origin.Y + (row + 0.5f) * CellSize);
}
