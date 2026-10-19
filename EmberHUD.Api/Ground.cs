using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Clipper2Lib;
using Hexa.NET.ImGui;

namespace EmberHUD;

public static class Ground
{
    internal const float Near = 2;

    const int CircleSegments = 48;
    const int DotSegments = 12;
    const int GradientSegments = 24;
    const int MaxDashes = 2000;

    static readonly List<Vector3> WorldPoints = [];
    static readonly List<Vector3> ClippedPoints = [];
    static readonly List<Vector2> ScreenPoints = [];
    static readonly List<Vector2> Triangles = [];
    static readonly List<Vector2> RingPoints = [];

    public static void Circle(Vector2 centre, float radius, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        Span<Vector2> ring = stackalloc Vector2[CircleSegments];
        Draw.RegularPoints(ring, centre, radius, 0);
        if (group is not null && GroundGroups.TryAddArea(group, ring, HeightOrPlayer(z))) return;
        Polyline(ring, color, thickness, true, z, list);
    }

    public static void CircleFilled(Vector2 centre, float radius, Color color, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        Span<Vector2> ring = stackalloc Vector2[CircleSegments];
        Draw.RegularPoints(ring, centre, radius, 0);
        if (group is not null && GroundGroups.TryAddArea(group, ring, HeightOrPlayer(z))) return;
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

    public static void Rect(Vector2 centre, Vector2 size, float rotation, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        RectCorners(centre, size, rotation, corners);
        if (group is not null && GroundGroups.TryAddArea(group, corners, HeightOrPlayer(z))) return;
        Polyline(corners, color, thickness, true, z, list);
    }

    public static void RectFilled(Vector2 centre, Vector2 size, float rotation, Color color, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        RectCorners(centre, size, rotation, corners);
        if (group is not null && GroundGroups.TryAddArea(group, corners, HeightOrPlayer(z))) return;
        PolygonFilled(corners, color, z, list);
    }

    public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1, float? z = null, ImDrawListPtr? list = null, string? group = null) =>
        Polyline([a, b], color, thickness, false, z, list, group);

    public static void GradientLine(Vector2 from, Vector2 to, Color start, Color end, float thickness = 1, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        if (group is not null && AddStrip(group, [from, to], false, thickness, z)) return;
        for (var i = 0; i < GradientSegments; i++)
        {
            var t = (i + 0.5f) / GradientSegments;
            var color = Color.FromArgb(Mix(start.A, end.A, t), Mix(start.R, end.R, t), Mix(start.G, end.G, t), Mix(start.B, end.B, t));
            Line(Vector2.Lerp(from, to, i / (float)GradientSegments), Vector2.Lerp(from, to, (i + 1) / (float)GradientSegments), color, thickness, z, list);
        }
    }

    public static void DashedLine(Vector2 from, Vector2 to, Color color, float thickness, float dash, float gap, float phase = 0, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        if (group is not null && AddStrip(group, [from, to], false, thickness, z)) return;
        var length = Vector2.Distance(from, to);
        var period = dash + gap;
        if (length <= 0 || dash <= 0 || gap < 0 || length / period > MaxDashes || !float.IsFinite(phase)) return;
        var direction = (to - from) / length;
        var offset = (phase % period + period) % period;
        for (var start = offset - period; start < length; start += period)
        {
            var (a, b) = (MathF.Max(start, 0), MathF.Min(start + dash, length));
            if (b > a) Line(from + direction * a, from + direction * b, color, thickness, z, list);
        }
    }

    public static unsafe bool Text(string text, Vector2 centre, float worldHeight, float rotationDegrees, Color color, float? z = null, ImDrawListPtr? list = null)
    {
        if (text.Length == 0 || worldHeight <= 0 || !TryFrame(out var camera, out var viewport)) return false;
        var size = Draw.MeasureText(text);
        if (size.Y <= 0) return false;
        var target = list ?? Draw.Background;
        var height = HeightOrPlayer(z);
        var scale = worldHeight / size.Y;
        var (sin, cos) = MathF.SinCos(rotationDegrees * MathF.PI / 180);
        bool TryProject(Vector2 pos, out Vector2 screen)
        {
            var local = (pos - size / 2) * scale;
            var world = new Vector3(centre + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos), height);
            screen = default;
            if (camera.Depth(world) < Near || camera.ToScreen(world, viewport) is not { } projected) return false;
            screen = projected;
            return true;
        }

        if (!TryProject(Vector2.Zero, out var top) || !TryProject(new Vector2(size.X, 0), out _) || !TryProject(size, out _) || !TryProject(new Vector2(0, size.Y), out var bottom)) return false;
        var px = Math.Clamp(MathF.Round(Vector2.Distance(top, bottom)), 8, 256);
        size = Draw.MeasureText(text, px);
        if (size.Y <= 0) return false;
        scale = worldHeight / size.Y;
        var vertices = target.VtxBuffer.Size;
        Draw.Text(text, Vector2.Zero, color, Align.TopLeft, px, list: target);
        var raw = target.Handle;
        for (var i = vertices; i < raw->VtxBuffer.Size; i++)
        {
            if (TryProject(raw->VtxBuffer.Data[i].Pos, out var screen)) raw->VtxBuffer.Data[i].Pos = screen;
            else raw->VtxBuffer.Data[i].Col = 0;
        }

        return raw->VtxBuffer.Size > vertices;
    }

    public static void Polyline(ReadOnlySpan<Vector2> points, Color color, float thickness = 1, bool closed = false, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        if (group is not null && AddStrip(group, points, closed, thickness, z)) return;
        if (points.Length < 2 || !TryFrame(out var camera, out var viewport)) return;
        Lift(points, z);
        ProjectRuns(camera, viewport, CollectionsMarshal.AsSpan(WorldPoints), closed, run => Draw.Polyline(run, color, thickness, false, list));
    }

    public static void PolygonFilled(ReadOnlySpan<Vector2> points, Color color, float? z = null, ImDrawListPtr? list = null, string? group = null)
    {
        if (group is not null && GroundGroups.TryAddArea(group, points, HeightOrPlayer(z))) return;
        if (points.Length < 3 || !TryFrame(out var camera, out var viewport)) return;
        Lift(points, z);
        ClipPolygon(camera, CollectionsMarshal.AsSpan(WorldPoints), ClippedPoints);
        ScreenPoints.Clear();
        foreach (var point in ClippedPoints)
            if (camera.ToScreen(point, viewport) is { } at) ScreenPoints.Add(at);
        Draw.ConvexPolyFilled(CollectionsMarshal.AsSpan(ScreenPoints), color, list);
    }

    public static void Group(string id, Color border, Color fill, float thickness, string? under = null)
    {
        var split = !ImGui.GetCurrentContext().IsNull && Draw.Background.Splitter.Count >= Draw.Layers;
        GroundGroups.Declare(id, new GroupStyle(border, fill, thickness, under, split ? Draw.Background.Splitter.Current : -1));
    }

    internal static void FlushGroups()
    {
        if (!GroundGroups.Any) return;
        try
        {
            if (TryFrame(out var camera, out var viewport)) DrawGroups(camera, viewport);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
        finally
        {
            GroundGroups.Clear();
        }
    }

    static void DrawGroups(Camera camera, Vector2 viewport)
    {
        var list = Draw.Background;
        var splitting = list.Splitter.Count >= Draw.Layers;
        var previousChannel = list.Splitter.Current;
        try
        {
            foreach (var (_, style, z, merged) in GroundGroups.Merge())
            {
                if (splitting) list.ChannelsSetCurrent(style.Layer != -1 ? style.Layer : previousChannel);
                if (style.Fill.A > 0) FillMerged(camera, viewport, merged, style.Fill, z, list);
                if (style.Border.A > 0 && style.Thickness > 0)
                    foreach (var path in merged)
                    {
                        RingPoints.Clear();
                        foreach (var point in path) RingPoints.Add(new Vector2((float)point.x, (float)point.y));
                        Polyline(CollectionsMarshal.AsSpan(RingPoints), style.Border, style.Thickness, true, z);
                    }
            }
        }
        finally
        {
            if (splitting) list.ChannelsSetCurrent(previousChannel);
        }
    }

    static void FillMerged(Camera camera, Vector2 viewport, PathsD merged, Color fill, float z, ImDrawListPtr list)
    {
        var flags = list.Flags;
        list.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        try
        {
            Triangles.Clear();
            if (!GroundGroups.Triangulate(merged, Triangles))
            {
                foreach (var path in merged)
                {
                    if (!Clipper.IsPositive(path)) continue;
                    RingPoints.Clear();
                    foreach (var point in path) RingPoints.Add(new Vector2((float)point.x, (float)point.y));
                    FillWorld(camera, viewport, CollectionsMarshal.AsSpan(RingPoints), fill, z, list, concave: true);
                }

                return;
            }

            var triangles = CollectionsMarshal.AsSpan(Triangles);
            for (var i = 0; i + 2 < triangles.Length; i += 3) FillWorld(camera, viewport, triangles.Slice(i, 3), fill, z, list, concave: false);
        }
        finally
        {
            list.Flags = flags;
        }
    }

    static void FillWorld(Camera camera, Vector2 viewport, ReadOnlySpan<Vector2> polygon, Color fill, float z, ImDrawListPtr list, bool concave)
    {
        Lift(polygon, z);
        ClipPolygon(camera, CollectionsMarshal.AsSpan(WorldPoints), ClippedPoints);
        ScreenPoints.Clear();
        foreach (var point in ClippedPoints)
            if (camera.ToScreen(point, viewport) is { } at) ScreenPoints.Add(at);
        if (concave) Draw.ConcavePolyFilled(CollectionsMarshal.AsSpan(ScreenPoints), fill, list);
        else Draw.ConvexPolyFilled(CollectionsMarshal.AsSpan(ScreenPoints), fill, list);
    }

    static bool AddStrip(string? group, ReadOnlySpan<Vector2> points, bool closed, float thickness, float? z)
    {
        if (!GroundGroups.IsDeclared(group)) return false;
        if (points.Length == 0 || !TryFrame(out var camera, out var viewport)) return true;
        var height = HeightOrPlayer(z);
        var width = GroundGroups.WorldPerPixel(camera, viewport, new Vector3(points[0], height)) * thickness;
        GroundGroups.TryAddStrip(group, points, closed, width, height);
        return true;
    }

    static int Mix(int a, int b, float t) => (int)MathF.Round(a + (b - a) * t);

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
