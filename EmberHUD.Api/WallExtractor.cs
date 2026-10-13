namespace EmberHUD;

public readonly record struct WallSegment(float Ax, float Ay, float Bx, float By);

public static class WallExtractor
{
    public static List<WallSegment> Extract(Terrain terrain) => Extract(terrain, terrain.IsWalkable);

    public static List<WallSegment> Extract(Terrain terrain, Func<int, int, bool> inside)
    {
        var walls = new List<WallSegment>();
        var size = terrain.CellSize;
        if (!float.IsFinite(size) || size <= 0) return walls;

        bool In(int col, int row) => col >= 0 && row >= 0 && col < terrain.Cols && row < terrain.Rows && inside(col, row);

        for (var row = 0; row < terrain.Rows; row++)
        {
            var odd = terrain.OddRowShift && (row & 1) == 1;
            var y0 = terrain.Origin.Y + row * size;
            var y1 = y0 + size;
            for (var col = 0; col < terrain.Cols; col++)
            {
                if (!In(col, row)) continue;
                var x0 = terrain.Origin.X + (col + (odd ? 0.5f : 0f)) * size;
                var x1 = x0 + size;
                var mid = x0 + size / 2;
                if (!In(col - 1, row)) walls.Add(new(x0, y0, x0, y1));
                if (!In(col + 1, row)) walls.Add(new(x1, y0, x1, y1));
                if (!terrain.OddRowShift)
                {
                    if (!In(col, row - 1)) walls.Add(new(x0, y0, x1, y0));
                    if (!In(col, row + 1)) walls.Add(new(x0, y1, x1, y1));
                    continue;
                }

                var (left, right) = odd ? (col, col + 1) : (col - 1, col);
                if (!In(left, row - 1)) walls.Add(new(x0, y0, mid, y0));
                if (!In(right, row - 1)) walls.Add(new(mid, y0, x1, y0));
                if (!In(left, row + 1)) walls.Add(new(x0, y1, mid, y1));
                if (!In(right, row + 1)) walls.Add(new(mid, y1, x1, y1));
            }
        }
        return walls;
    }
}
