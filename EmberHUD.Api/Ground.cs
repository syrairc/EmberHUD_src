using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;

namespace EmberHUD;

public static class Ground
{
    internal const float Near = 2;

    const int CircleSegments = 48;
    const int DotSegments = 12;

    static readonly List<Vector3> WorldPoints = [];
    static readonly List<Vector3> ClippedPoints = [];
    static readonly List<Vector2> ScreenPoints = [];

    public static void Circle(Vector2 centre, float radius, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null)
    {
        Span<Vector2> ring = stackalloc Vector2[CircleSegments];
        Draw.RegularPoints(ring, centre, radius, 0);
        Polyline(ring, color, thickness, true, z, list);
    }

    public static void CircleFilled(Vector2 centre, float radius, Color color, float? z = null, ImDrawListPtr? list = null)
    {
        Span<Vector2> ring = stackalloc Vector2[CircleSegments];
        Draw.RegularPoints(ring, centre, radius, 0);
        PolygonFilled(ring, color, z, list);
    }

    public static void Dot(Vector2 centre, float radius, Color color, float? z = null, ImDrawListPtr? list = null)
    {
        if (!TryFrame(out var camera, out var viewport)) return;
        var height = HeightOrPlayer(z);
        var at = new Vector3(centre, height);
        if (camera.Depth(at) < Near || camera.ToScreen(at, viewport) is not { } screen) return;
        if (camera.ToScreen(at + new Vector3(radius, 0, 0), viewport) is not { } east || camera.ToScreen(at + new Vector3(0, radius, 0), viewport) is not { } north) return;
        var pixels = (Vector2.Distance(screen, east) + Vector2.Distance(screen, north)) / 2;
        if (pixels > 0) Draw.CircleFilled(screen, pixels, color, DotSegments, list);
    }

    public static void Rect(Vector2 centre, Vector2 size, float rotation, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        RectCorners(centre, size, rotation, corners);
        Polyline(corners, color, thickness, true, z, list);
    }

    public static void RectFilled(Vector2 centre, Vector2 size, float rotation, Color color, float? z = null, ImDrawListPtr? list = null)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        RectCorners(centre, size, rotation, corners);
        PolygonFilled(corners, color, z, list);
    }

    public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null) =>
        Polyline([a, b], color, thickness, false, z, list);

    public static void Polyline(ReadOnlySpan<Vector2> points, Color color, float thickness = 1, bool closed = false, float? z = null, ImDrawListPtr? list = null)
    {
        if (points.Length < 2 || !TryFrame(out var camera, out var viewport)) return;
        Lift(points, z);
        ProjectRuns(camera, viewport, CollectionsMarshal.AsSpan(WorldPoints), closed, run => Draw.Polyline(run, color, thickness, false, list));
    }

    public static void PolygonFilled(ReadOnlySpan<Vector2> points, Color color, float? z = null, ImDrawListPtr? list = null)
    {
        if (points.Length < 3 || !TryFrame(out var camera, out var viewport)) return;
        Lift(points, z);
        ClipPolygon(camera, CollectionsMarshal.AsSpan(WorldPoints), ClippedPoints);
        ScreenPoints.Clear();
        foreach (var point in ClippedPoints)
            if (camera.ToScreen(point, viewport) is { } at) ScreenPoints.Add(at);
        Draw.ConvexPolyFilled(CollectionsMarshal.AsSpan(ScreenPoints), color, list);
    }

    internal static float HeightOrPlayer(float? z) => z ?? Game.State.Player?.Position.Z ?? 0;

    internal static void RectCorners(Vector2 centre, Vector2 size, float degrees, Span<Vector2> into)
    {
        var half = size / 2;
        var (sin, cos) = MathF.SinCos(degrees * MathF.PI / 180);
        Span<Vector2> local = [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)];
        for (var i = 0; i < 4; i++)
            into[i] = centre + new Vector2(local[i].X * cos - local[i].Y * sin, local[i].X * sin + local[i].Y * cos);
    }

    internal static bool ClipSegment(Camera camera, ref Vector3 a, ref Vector3 b)
    {
        var depthA = camera.Depth(a);
        var depthB = camera.Depth(b);
        if (depthA < Near && depthB < Near) return false;
        if (depthA < Near) a = Vector3.Lerp(a, b, (Near - depthA) / (depthB - depthA));
        else if (depthB < Near) b = Vector3.Lerp(b, a, (Near - depthB) / (depthA - depthB));
        return true;
    }

    internal static void ClipPolygon(Camera camera, ReadOnlySpan<Vector3> world, List<Vector3> into)
    {
        into.Clear();
        for (var i = 0; i < world.Length; i++)
        {
            var current = world[i];
            var previous = world[(i + world.Length - 1) % world.Length];
            var currentIn = camera.Depth(current) >= Near;
            var previousIn = camera.Depth(previous) >= Near;
            if (currentIn != previousIn)
            {
                var (from, to) = previousIn ? (previous, current) : (current, previous);
                ClipSegment(camera, ref from, ref to);
                into.Add(to);
            }

            if (currentIn) into.Add(current);
        }
    }

    internal static void ProjectRuns(Camera camera, Vector2 viewport, ReadOnlySpan<Vector3> world, bool closed, Action<ReadOnlySpan<Vector2>> emit)
    {
        ScreenPoints.Clear();
        var segments = closed ? world.Length : world.Length - 1;
        for (var i = 0; i < segments; i++)
        {
            var originalA = world[i];
            var originalB = world[(i + 1) % world.Length];
            var (a, b) = (originalA, originalB);
            if (!ClipSegment(camera, ref a, ref b))
            {
                Flush(emit);
                continue;
            }

            if (a != originalA) Flush(emit);
            if (ScreenPoints.Count == 0 && camera.ToScreen(a, viewport) is { } start) ScreenPoints.Add(start);
            if (camera.ToScreen(b, viewport) is { } end) ScreenPoints.Add(end);
            if (b != originalB) Flush(emit);
        }

        Flush(emit);
    }

    static void Flush(Action<ReadOnlySpan<Vector2>> emit)
    {
        if (ScreenPoints.Count > 1) emit(CollectionsMarshal.AsSpan(ScreenPoints));
        ScreenPoints.Clear();
    }

    static void Lift(ReadOnlySpan<Vector2> points, float? z)
    {
        var height = HeightOrPlayer(z);
        WorldPoints.Clear();
        foreach (var point in points) WorldPoints.Add(new Vector3(point, height));
    }

    static bool TryFrame([NotNullWhen(true)] out Camera? camera, out Vector2 viewport)
    {
        camera = Game.State.Camera;
        viewport = ImGui.GetIO().DisplaySize;
        return camera is not null;
    }
}
