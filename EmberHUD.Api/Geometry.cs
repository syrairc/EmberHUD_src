using System.Numerics;

namespace EmberHUD;

public static class Geometry
{
    public static int ClipConvex(ReadOnlySpan<Vector2> subject, ReadOnlySpan<Vector2> clip, Span<Vector2> into)
    {
        if (subject.Length < 3 || clip.Length < 3 || subject.Length > into.Length) return 0;
        Span<Vector2> other = into.Length <= 256 ? stackalloc Vector2[into.Length] : new Vector2[into.Length];
        var area = 0f;
        for (var i = 0; i < clip.Length; i++) area += Cross(clip[i], clip[(i + 1) % clip.Length]);
        var side = area < 0 ? -1f : 1f;
        subject.CopyTo(into);
        var count = subject.Length;
        for (var edge = 0; edge < clip.Length && count > 0; edge++)
        {
            var a = clip[edge];
            var b = clip[(edge + 1) % clip.Length];
            var written = 0;
            for (var i = 0; i < count; i++)
            {
                var current = into[i];
                var previous = into[(i + count - 1) % count];
                var currentSide = Cross(b - a, current - a) * side;
                var previousSide = Cross(b - a, previous - a) * side;
                if (written + 2 > other.Length) return 0;
                if ((currentSide >= 0) != (previousSide >= 0))
                    other[written++] = previous + (current - previous) * (previousSide / (previousSide - currentSide));
                if (currentSide >= 0) other[written++] = current;
            }

            other[..written].CopyTo(into);
            count = written;
        }

        return count;
    }

    public static void RegularPolygon(Vector2 centre, float radius, Span<Vector2> into) => Draw.RegularPoints(into, centre, radius, 0);

    public static void OrientedRect(Vector2 centre, Vector2 direction, float width, float length, Span<Vector2> into)
    {
        var along = direction * (length / 2);
        var across = new Vector2(-direction.Y, direction.X) * (width / 2);
        into[0] = centre - along - across;
        into[1] = centre + along - across;
        into[2] = centre + along + across;
        into[3] = centre - along + across;
    }

    public static bool Contains(ReadOnlySpan<Vector2> convex, Vector2 point)
    {
        if (convex.Length < 3 || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
        var area = 0f;
        for (var i = 0; i < convex.Length; i++) area += Cross(convex[i], convex[(i + 1) % convex.Length]);
        if (area == 0) return false;
        var side = area < 0 ? -1f : 1f;
        for (var i = 0; i < convex.Length; i++)
        {
            var a = convex[i];
            if (Cross(convex[(i + 1) % convex.Length] - a, point - a) * side < 0) return false;
        }

        return true;
    }

    public static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        var along = b - a;
        var length = along.LengthSquared();
        var t = length == 0 ? 0 : Math.Clamp(Vector2.Dot(point - a, along) / length, 0, 1);
        return Vector2.Distance(point, a + along * t);
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
