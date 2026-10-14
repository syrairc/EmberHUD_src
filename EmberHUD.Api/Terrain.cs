using System.Collections;
using System.Numerics;

namespace EmberHUD;

public readonly record struct Polyline(Vector2[] Points, bool Closed);

public sealed record Terrain(int Cols, int Rows, float CellSize, Vector2 Origin, BitArray Walkable)
{
    public bool IsWalkable(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows) return false;
        var index = row * Cols + col;
        return index < Walkable.Length && Walkable[index];
    }

    public Vector2 CellCenter(int col, int row) => new(Origin.X + (col + 0.5f) * CellSize, Origin.Y + (row + 0.5f) * CellSize);

    readonly record struct Edge(float Ax, float Ay, float Bx, float By);

    List<Edge> Edges(Func<int, int, bool>? inside)
    {
        inside ??= IsWalkable;
        var walls = new List<Edge>();
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

    public Polyline[] Outline(Func<int, int, bool>? inside = null)
    {
        var edges = Edges(inside);
        var step = CellSize / 2;
        var origin = Origin;
        (long X, long Y) Key(float x, float y) => ((long)MathF.Round((x - origin.X) / step), (long)MathF.Round((y - origin.Y) / step));
        Vector2 Point((long X, long Y) key) => new Vector2(origin.X + key.X * step, origin.Y + key.Y * step);

        var ends = new (long X, long Y)[edges.Count * 2];
        var touching = new Dictionary<(long X, long Y), List<int>>();
        for (var i = 0; i < edges.Count; i++)
        {
            ends[2 * i] = Key(edges[i].Ax, edges[i].Ay);
            ends[2 * i + 1] = Key(edges[i].Bx, edges[i].By);
            for (var side = 0; side < 2; side++)
            {
                if (!touching.TryGetValue(ends[2 * i + side], out var list)) touching[ends[2 * i + side]] = list = [];
                list.Add(i);
            }
        }

        var used = new bool[edges.Count];
        var lines = new List<Polyline>();

        int? Unused((long X, long Y) at)
        {
            foreach (var edge in touching[at])
                if (!used[edge]) return edge;
            return null;
        }

        Polyline Line(List<(long X, long Y)> keys, bool closed)
        {
            var points = new List<Vector2>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                var openEnd = !closed && (i == 0 || i == keys.Count - 1);
                if (!openEnd && Straight(keys[(i + keys.Count - 1) % keys.Count], keys[i], keys[(i + 1) % keys.Count])) continue;
                points.Add(Point(keys[i]));
            }
            return new Polyline([.. points], closed);
        }

        void Walk((long X, long Y) start)
        {
            while (Unused(start) is { } first)
            {
                var keys = new List<(long X, long Y)> { start };
                var at = start;
                var edge = first;
                while (true)
                {
                    used[edge] = true;
                    at = ends[2 * edge] == at ? ends[2 * edge + 1] : ends[2 * edge];
                    if (at == start)
                    {
                        lines.Add(Line(keys, true));
                        break;
                    }
                    keys.Add(at);
                    if (Unused(at) is not { } next)
                    {
                        lines.Add(Line(keys, false));
                        break;
                    }
                    edge = next;
                }
            }
        }

        foreach (var (at, list) in touching)
            if (list.Count % 2 == 1) Walk(at);
        foreach (var at in touching.Keys) Walk(at);
        return [.. lines];
    }

    static bool Straight((long X, long Y) a, (long X, long Y) b, (long X, long Y) c)
    {
        var (ux, uy) = (b.X - a.X, b.Y - a.Y);
        var (vx, vy) = (c.X - b.X, c.Y - b.Y);
        return ux * vy - uy * vx == 0 && ux * vx + uy * vy > 0;
    }
}
