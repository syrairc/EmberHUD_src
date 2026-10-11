using System;
using System.Collections.Generic;
using System.Numerics;
using TorchlightSDK;

namespace Minimap;

public readonly record struct Polyline(Vector2[] Points, bool Closed);

public static class Outline
{
    public static Polyline[] Join(List<WallSegment> edges, Terrain terrain)
    {
        var step = terrain.CellSize / 2;
        var origin = terrain.Origin;
        (long X, long Y) Key(float x, float y) => ((long)MathF.Round((x - origin.X) / step), (long)MathF.Round((y - origin.Y) / step));
        Vector2 Point((long X, long Y) key) => Map.Project(new Vector3(origin.X + key.X * step, origin.Y + key.Y * step, 0));

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

        void Walk((long X, long Y) start)
        {
            while (Unused(start) is { } first)
            {
                var points = new List<Vector2> { Point(start) };
                var at = start;
                var edge = first;
                while (true)
                {
                    used[edge] = true;
                    at = ends[2 * edge] == at ? ends[2 * edge + 1] : ends[2 * edge];
                    if (at == start)
                    {
                        lines.Add(new Polyline([.. points], true));
                        break;
                    }
                    points.Add(Point(at));
                    if (Unused(at) is not { } next)
                    {
                        lines.Add(new Polyline([.. points], false));
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
}
