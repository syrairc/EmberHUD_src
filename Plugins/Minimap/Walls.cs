using System.Collections.Generic;
using System.Numerics;
using TorchlightSDK;

namespace Minimap;

public sealed record Walls(int AreaId, Terrain? Source, Vector2[] Lines, Vector2 Min, Vector2 Max, Polyline[] RegionOutline)
{
    public static readonly Walls Empty = new(-1, null, [], default, default, []);

    public bool Matches(int areaId, Terrain? terrain) => AreaId == areaId && ReferenceEquals(Source, terrain);

    public static Walls Build(int areaId, Terrain? terrain)
    {
        if (terrain is null) return new Walls(areaId, null, [], default, default, []);

        var edges = WallExtractor.Extract(terrain);
        var lines = new List<Vector2>();
        foreach (var wall in edges)
        {
            lines.Add(Map.Project(new Vector3(wall.Ax, wall.Ay, 0)));
            lines.Add(Map.Project(new Vector3(wall.Bx, wall.By, 0)));
        }
        if (lines.Count == 0) return new Walls(areaId, terrain, [], default, default, []);

        var min = lines[0];
        var max = lines[0];
        foreach (var point in lines)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return new Walls(areaId, terrain, [.. lines], min, max, Outline.Join(edges, terrain));
    }
}
