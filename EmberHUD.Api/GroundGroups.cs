using System.Drawing;
using System.Numerics;
using Clipper2Lib;

namespace EmberHUD;

internal sealed record GroupStyle(Color Border, Color Fill, float Thickness, string? Under, int Layer);

internal static class GroundGroups
{
    const int Precision = 2;
    const float MaxCoordinate = 1e7f;

    sealed class Shapes
    {
        public GroupStyle Style = null!;
        public float Z;
        public bool HasZ;
        public readonly PathsD Paths = [];
    }

    static readonly Dictionary<string, Shapes> Groups = [];
    static readonly List<string> Order = [];
    static readonly Stack<Shapes> Pool = [];
    static readonly Dictionary<string, PathsD> Unions = [];
    static readonly List<string> Remaining = [];
    static readonly List<(string Id, GroupStyle Style, float Z, PathsD Merged)> Result = [];

    internal static bool IsDeclared(string? id) => id is not null && Groups.ContainsKey(id);

    internal static void Declare(string id, GroupStyle style)
    {
        if (!Groups.TryGetValue(id, out var shapes))
        {
            shapes = Pool.Count > 0 ? Pool.Pop() : new Shapes();
            Groups[id] = shapes;
            Order.Add(id);
        }

        shapes.Style = style;
    }

    internal static bool TryAddArea(string? id, ReadOnlySpan<Vector2> polygon, float z)
    {
        if (id is null || !Groups.TryGetValue(id, out var shapes)) return false;
        if (polygon.Length < 3 || !AllSane(polygon) || !float.IsFinite(z)) return true;
        var path = ToPath(polygon);
        if (!Clipper.IsPositive(path)) path.Reverse();
        Add(shapes, path, z);
        return true;
    }

    internal static bool TryAddStrip(string? id, ReadOnlySpan<Vector2> points, bool closed, float worldWidth, float z)
    {
        if (id is null || !Groups.TryGetValue(id, out var shapes)) return false;
        if (points.Length < 2 || !AllSane(points) || !float.IsFinite(z) || !float.IsFinite(worldWidth) || worldWidth <= 0 || worldWidth > MaxCoordinate) return true;
        var inflated = Clipper.InflatePaths([ToPath(points)], worldWidth / 2, JoinType.Round, closed ? EndType.Joined : EndType.Butt, 2, Precision);
        if (inflated.Count == 0) return true;
        shapes.Paths.AddRange(inflated);
        if (!shapes.HasZ) (shapes.Z, shapes.HasZ) = (z, true);
        return true;
    }

    internal static IReadOnlyList<(string Id, GroupStyle Style, float Z, PathsD Merged)> Merge()
    {
        Result.Clear();
        Unions.Clear();
        Remaining.Clear();
        foreach (var id in Order)
        {
            if (Groups[id].Paths.Count == 0) continue;
            Unions[id] = Union(Groups[id].Paths);
            Remaining.Add(id);
        }

        while (Remaining.Count > 0)
        {
            var pick = Remaining[NextIndex()];
            Remaining.Remove(pick);
            var shapes = Groups[pick];
            var merged = Unions[pick];
            if (shapes.Style.Under is { } under && under != pick && Unions.TryGetValue(under, out var cut))
                merged = Clipper.Difference(merged, cut, FillRule.NonZero, Precision);
            Result.Add((pick, shapes.Style, shapes.Z, merged));
        }

        return Result;
    }

    internal static void Clear()
    {
        foreach (var shapes in Groups.Values)
        {
            shapes.Paths.Clear();
            shapes.HasZ = false;
            Pool.Push(shapes);
        }

        Groups.Clear();
        Order.Clear();
    }

    internal static float WorldPerPixel(Camera camera, Vector2 viewport, Vector3 at)
    {
        if (camera.Depth(at) < Ground.Near || camera.ToScreen(at, viewport) is not { } screen) return float.NaN;
        if (camera.ToScreen(at + new Vector3(1, 0, 0), viewport) is not { } east || camera.ToScreen(at + new Vector3(0, 1, 0), viewport) is not { } north) return float.NaN;
        var pixels = (Vector2.Distance(screen, east) + Vector2.Distance(screen, north)) / 2;
        return pixels > 0 ? 1 / pixels : float.NaN;
    }

    static int NextIndex()
    {
        for (var i = 0; i < Remaining.Count; i++)
        {
            var blocked = false;
            for (var j = 0; j < Remaining.Count && !blocked; j++)
                blocked = j != i && Groups[Remaining[j]].Style.Under == Remaining[i];
            if (!blocked) return i;
        }

        return 0;
    }

    static PathsD Union(PathsD paths)
    {
        var solution = new PathsD();
        var clip = new ClipperD(Precision) { PreserveCollinear = false };
        clip.AddSubject(paths);
        clip.Execute(ClipType.Union, FillRule.NonZero, solution);
        return solution;
    }

    static void Add(Shapes shapes, PathD path, float z)
    {
        shapes.Paths.Add(path);
        if (!shapes.HasZ) (shapes.Z, shapes.HasZ) = (z, true);
    }

    static bool AllSane(ReadOnlySpan<Vector2> points)
    {
        foreach (var point in points)
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || MathF.Abs(point.X) > MaxCoordinate || MathF.Abs(point.Y) > MaxCoordinate) return false;
        return true;
    }

    static PathD ToPath(ReadOnlySpan<Vector2> points)
    {
        var path = new PathD(points.Length);
        foreach (var point in points) path.Add(new PointD(point.X, point.Y));
        return path;
    }
}
