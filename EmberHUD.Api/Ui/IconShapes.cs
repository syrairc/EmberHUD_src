using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public enum IconShape { Square, Rounded, Circle, Hexagon }

public static class IconShapes
{
    const int ArcSegments = 6;
    const int CirclePoints = 32;

    public static Vector2[] Outline(IconShape shape, Vector2 min, Vector2 max, float cornerRadius = 0) => shape switch
    {
        IconShape.Rounded => Rounded(min, max, cornerRadius),
        IconShape.Circle => Circle(min, max),
        IconShape.Hexagon => Hexagon(min, max),
        _ => Square(min, max),
    };

    static Vector2[] Square(Vector2 min, Vector2 max) => [min, new(max.X, min.Y), max, new(min.X, max.Y)];

    static Vector2[] Rounded(Vector2 min, Vector2 max, float radius)
    {
        radius = Math.Clamp(radius, 0, Math.Max(0, Math.Min(max.X - min.X, max.Y - min.Y) / 2));
        if (radius <= 0) return Square(min, max);
        var points = new Vector2[4 * (ArcSegments + 1)];
        Vector2[] centres = [new(min.X + radius, min.Y + radius), new(max.X - radius, min.Y + radius), new(max.X - radius, max.Y - radius), new(min.X + radius, max.Y - radius)];
        for (var corner = 0; corner < 4; corner++)
            for (var i = 0; i <= ArcSegments; i++)
            {
                var angle = MathF.PI * (1 + corner + (float)i / ArcSegments) / 2;
                points[corner * (ArcSegments + 1) + i] = centres[corner] + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        return points;
    }

    static Vector2[] Circle(Vector2 min, Vector2 max)
    {
        var centre = (min + max) / 2;
        var half = (max - min) / 2;
        var points = new Vector2[CirclePoints];
        for (var i = 0; i < CirclePoints; i++)
        {
            var angle = MathF.Tau * i / CirclePoints;
            points[i] = centre + half * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return points;
    }

    static Vector2[] Hexagon(Vector2 min, Vector2 max)
    {
        var quarter = (max.X - min.X) / 4;
        var middle = (min.Y + max.Y) / 2;
        return [new(min.X, middle), new(min.X + quarter, min.Y), new(max.X - quarter, min.Y), new(max.X, middle), new(max.X - quarter, max.Y), new(min.X + quarter, max.Y)];
    }

    public static Vector2[] ClipTop(ReadOnlySpan<Vector2> polygon, float cutY)
    {
        var result = new List<Vector2>(polygon.Length + 2);
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            var aIn = a.Y <= cutY;
            if (aIn) result.Add(a);
            if (aIn != (b.Y <= cutY) && a.Y != cutY && b.Y != cutY) result.Add(a + (b - a) * ((cutY - a.Y) / (b.Y - a.Y)));
        }
        return result.Count < 3 || polygon.Length > 0 && cutY <= TopOf(polygon) ? [] : [.. result];
    }

    static float TopOf(ReadOnlySpan<Vector2> polygon)
    {
        var top = float.MaxValue;
        foreach (var p in polygon) top = Math.Min(top, p.Y);
        return top;
    }

    public static void FillPolygon(ReadOnlySpan<Vector2> polygon, Color colour, ImDrawListPtr? list = null)
    {
        if (polygon.Length < 3 || colour.A == 0) return;
        Draw.ConvexPolyFilled(polygon, colour, list);
    }

    public static void StrokePolygon(ReadOnlySpan<Vector2> polygon, Color colour, float thickness, ImDrawListPtr? list = null)
    {
        if (polygon.Length < 3 || colour.A == 0 || thickness <= 0) return;
        Draw.Polyline(polygon, colour, thickness, true, list);
    }

    public static void DrawImage(Sprite sprite, ReadOnlySpan<Vector2> polygon, Vector2 min, Vector2 max, Color tint, ImDrawListPtr? list = null)
    {
        if (polygon.Length < 3 || tint.A == 0) return;
        var target = list ?? Draw.Background;
        var colour = EColor.U32(tint);
        var size = Vector2.Max(max - min, new Vector2(1e-6f));
        target.PushTexture(sprite.Texture);
        target.PrimReserve((polygon.Length - 2) * 3, polygon.Length);
        var first = target.VtxCurrentIdx;
        foreach (var point in polygon)
            target.PrimWriteVtx(point, sprite.Uv0 + (sprite.Uv1 - sprite.Uv0) * ((point - min) / size), colour);
        for (var i = 1; i < polygon.Length - 1; i++)
        {
            target.PrimWriteIdx((ushort)first);
            target.PrimWriteIdx((ushort)(first + i));
            target.PrimWriteIdx((ushort)(first + i + 1));
        }
        target.PopTexture();
    }
}
