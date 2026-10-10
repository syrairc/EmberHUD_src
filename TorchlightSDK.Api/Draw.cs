using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK;

[Flags]
public enum Align { TopLeft = 0, Center = 1, Right = 2, Middle = 4, Bottom = 8 }

public static class Draw
{
    static readonly Color MarkerRim = Color.FromArgb(0x08, 0x09, 0x0B);
    static readonly Color BadgeFill = Color.FromArgb(0x14, 0x16, 0x1A);
    static readonly Vector2[] Stroke = [new(-1, -1), new(0, -1), new(1, -1), new(-1, 0), new(1, 0), new(-1, 1), new(0, 1), new(1, 1)];

    public static ImDrawListPtr Background => ImGui.GetBackgroundDrawList();

    public static ImDrawListPtr Foreground => ImGui.GetForegroundDrawList();

    public static Vector2 Text(string text, Vector2 pos, Color color, Align align = Align.TopLeft, float size = 0, bool mono = false, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, mono);
        ImGui.AddText(list ?? Background, font, px, origin, EColor.U32(color), text);
        return measured;
    }

    static float BaseSize => Fonts.RegularSize > 0 ? Fonts.RegularSize : ImGui.GetFontSize();

    public static Vector2 Text(string text, Vector2 pos, Ui.TextStyle style, Align align = Align.TopLeft, ImDrawListPtr? list = null)
    {
        var size = BaseSize * style.Scale;
        var box = MeasureText(text, size);
        var min = pos - AlignOffset(box, align);
        var pad = new Vector2(5f, 2f);
        if (style.BgEnabled) RectFilled(min - pad, min + box + pad, style.BgColor, style.BorderRounding, list);
        if (style.BorderEnabled) Rect(min - pad, min + box + pad, style.BorderColor, style.BorderThickness, style.BorderRounding, list);
        if (style.StrokeEnabled) TextOutlined(text, min, style.Color, style.StrokeColor, 1f, Align.TopLeft, size, list: list);
        else Text(text, min, style.Color, Align.TopLeft, size, list: list);
        return box;
    }

    public static Vector2 TextOutlined(string text, Vector2 pos, Color color, Color outline, float thickness = 1, Align align = Align.TopLeft, float size = 0, bool mono = false, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, mono);
        var target = list ?? Background;
        var ring = EColor.U32(outline);
        foreach (var offset in Stroke) ImGui.AddText(target, font, px, origin + offset * thickness, ring, text);
        ImGui.AddText(target, font, px, origin, EColor.U32(color), text);
        return measured;
    }

    public static Vector2 TextShadow(string text, Vector2 pos, Color color, Color shadow, Vector2? offset = null, Align align = Align.TopLeft, float size = 0, bool mono = false, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, mono);
        var target = list ?? Background;
        ImGui.AddText(target, font, px, origin + (offset ?? Vector2.One), EColor.U32(shadow), text);
        ImGui.AddText(target, font, px, origin, EColor.U32(color), text);
        return measured;
    }

    public static Vector2 TextBackground(string text, Vector2 pos, Color color, Color background, Vector2? padding = null, float rounding = 0, Align align = Align.TopLeft, float size = 0, bool mono = false, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, mono);
        var target = list ?? Background;
        var pad = padding ?? new Vector2(4, 2);
        if (background.A > 0) target.AddRectFilled(origin - pad, origin + measured + pad, EColor.U32(background), rounding);
        ImGui.AddText(target, font, px, origin, EColor.U32(color), text);
        return measured + pad * 2;
    }

    public static Vector2 MeasureText(string text, float size = 0, bool mono = false) => Place(text, Vector2.Zero, Align.TopLeft, size, mono).Measured;

    public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1, ImDrawListPtr? list = null) => (list ?? Background).AddLine(a, b, EColor.U32(color), thickness);

    public static void Polyline(ReadOnlySpan<Vector2> points, Color color, float thickness = 1, bool closed = false, ImDrawListPtr? list = null)
    {
        if (points.Length < 2) return;
        var target = list ?? Background;
        target.AddPolyline(ref MemoryMarshal.GetReference(points), points.Length, EColor.U32(color), closed ? ImDrawFlags.Closed : ImDrawFlags.None, thickness);
    }

    public static void Arrow(Vector2 from, Vector2 to, Color color, float thickness = 1, float head = 8, ImDrawListPtr? list = null)
    {
        if (ArrowHead(from, to, head) is not { } wings) return;
        var target = list ?? Background;
        var col = EColor.U32(color);
        target.AddLine(from, to, col, thickness);
        target.AddTriangleFilled(to, wings.Left, wings.Right, col);
    }

    public static void Rect(Vector2 min, Vector2 max, Color color, float thickness = 1, float rounding = 0, ImDrawListPtr? list = null) => (list ?? Background).AddRect(min, max, EColor.U32(color), rounding, ImDrawFlags.None, thickness);

    public static void RectFilled(Vector2 min, Vector2 max, Color color, float rounding = 0, ImDrawListPtr? list = null) => (list ?? Background).AddRectFilled(min, max, EColor.U32(color), rounding);

    public static void Circle(Vector2 centre, float radius, Color color, float thickness = 1, int segments = 0, ImDrawListPtr? list = null) => (list ?? Background).AddCircle(centre, radius, EColor.U32(color), segments, thickness);

    public static void CircleFilled(Vector2 centre, float radius, Color color, int segments = 0, ImDrawListPtr? list = null) => (list ?? Background).AddCircleFilled(centre, radius, EColor.U32(color), segments);

    public static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color, float thickness = 1, ImDrawListPtr? list = null) => (list ?? Background).AddTriangle(a, b, c, EColor.U32(color), thickness);

    public static void TriangleFilled(Vector2 a, Vector2 b, Vector2 c, Color color, ImDrawListPtr? list = null) => (list ?? Background).AddTriangleFilled(a, b, c, EColor.U32(color));

    public static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float thickness = 1, ImDrawListPtr? list = null) => (list ?? Background).AddQuad(a, b, c, d, EColor.U32(color), thickness);

    public static void QuadFilled(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, ImDrawListPtr? list = null) => (list ?? Background).AddQuadFilled(a, b, c, d, EColor.U32(color));

    public static void ConvexPolyFilled(ReadOnlySpan<Vector2> points, Color color, ImDrawListPtr? list = null)
    {
        if (points.Length < 3) return;
        var target = list ?? Background;
        target.AddConvexPolyFilled(ref MemoryMarshal.GetReference(points), points.Length, EColor.U32(color));
    }

    public static void Glow(Vector2 centre, float radius, Color color, float inner = 0, ImDrawListPtr? list = null)
    {
        if (radius <= 0 || color.A == 0) return;
        const int segments = 32;
        var target = list ?? Background;
        var solid = EColor.U32(color);
        var clear = EColor.U32(Color.FromArgb(0, color));
        var uv = ImGui.GetFontTexUvWhitePixel();
        target.PrimReserve(segments * 9, 1 + segments * 2);
        var first = target.VtxCurrentIdx;
        target.PrimWriteVtx(centre, uv, solid);
        for (var i = 0; i < segments; i++)
        {
            var direction = new Vector2(MathF.Cos(i * MathF.Tau / segments), MathF.Sin(i * MathF.Tau / segments));
            target.PrimWriteVtx(centre + direction * MathF.Min(inner, radius), uv, solid);
            target.PrimWriteVtx(centre + direction * radius, uv, clear);
        }
        for (var i = 0; i < segments; i++)
        {
            var near = (ushort)(first + 1 + i * 2);
            var far = (ushort)(near + 1);
            var nextNear = (ushort)(first + 1 + (i + 1) % segments * 2);
            var nextFar = (ushort)(nextNear + 1);
            foreach (var index in (ReadOnlySpan<ushort>)[(ushort)first, near, nextNear, near, far, nextFar, near, nextFar, nextNear]) target.PrimWriteIdx(index);
        }
    }

    public const string MarkerPrefix = "marker:";

    public static void Marker(Vector2 centre, float radius, Color color, MarkerShape shape = MarkerShape.Dot, float glow = 0, ImDrawListPtr? list = null)
    {
        if (radius <= 0) return;
        var target = list ?? Background;
        var badge = shape is MarkerShape.Star or MarkerShape.Sparkle;
        if (glow > 0) Glow(centre, MathF.Max(radius * glow * (badge ? 1.9f : 2.2f), radius), Color.FromArgb((int)(color.A * (badge ? 0.4f : 0.2f)), color), radius, target);
        var fill = EColor.U32(color);
        var rim = MathF.Max(1, radius * 0.3f);
        if (shape == MarkerShape.Dot)
        {
            target.AddCircleFilled(centre, radius, fill);
            target.AddCircle(centre, radius, EColor.U32(MarkerRim), 0, rim);
            return;
        }

        if (!badge)
        {
            var (sides, start) = shape switch
            {
                MarkerShape.Diamond => (4, -MathF.PI / 2),
                MarkerShape.Square => (4, -MathF.PI / 4),
                MarkerShape.Triangle => (3, -MathF.PI / 2),
                _ => (6, 0f),
            };
            Span<Vector2> corners = stackalloc Vector2[sides];
            RegularPoints(corners, centre, radius, start);
            ConvexPolyFilled(corners, color, target);
            Polyline(corners, MarkerRim, rim, true, target);
            return;
        }

        var ring = MathF.Max(1, radius * 0.22f);
        target.AddCircleFilled(centre, radius, EColor.U32(BadgeFill));
        target.AddCircle(centre, radius - ring / 2, fill, 0, ring);
        Span<Vector2> star = stackalloc Vector2[shape == MarkerShape.Star ? 10 : 8];
        StarPoints(star, centre, radius * 0.62f, radius * (shape == MarkerShape.Star ? 0.26f : 0.22f));
        for (var i = 0; i < star.Length; i += 2) target.AddQuadFilled(centre, star[(i + star.Length - 1) % star.Length], star[i], star[i + 1], fill);
    }

    public static void Icon(string? icon, Vector2 centre, float radius, Color color, IconAtlas? atlas, float glow = 0, ImDrawListPtr? list = null)
    {
        if (TryMarker(icon, out var shape) || icon is null || atlas is null || !atlas.Sprites.TryGetValue(icon, out var sprite))
        {
            Marker(centre, radius, color, shape, glow, list);
            return;
        }

        if (glow > 0) Glow(centre, radius * glow * 1.9f, Color.FromArgb((int)(color.A * 0.3f), color), radius * 0.5f, list);
        Image(sprite, centre - new Vector2(radius), centre + new Vector2(radius), color, list);
    }

    public static bool TryMarker(string? icon, out MarkerShape shape)
    {
        if (icon is not null && icon.StartsWith(MarkerPrefix, StringComparison.Ordinal) && Enum.TryParse(icon.AsSpan(MarkerPrefix.Length), out shape) && Enum.IsDefined(shape)) return true;
        shape = MarkerShape.Dot;
        return false;
    }

    public static void Image(Sprite sprite, Vector2 min, Vector2 max, Color? tint = null, ImDrawListPtr? list = null) =>
        (list ?? Background).AddImage(sprite.Texture, min, max, sprite.Uv0, sprite.Uv1, EColor.U32(tint ?? Color.White));

    public static void ImageRotated(Sprite sprite, Vector2 centre, Vector2 size, Vector2 facing, Color? tint = null, ImDrawListPtr? list = null)
    {
        if (RotatedQuad(centre, size, facing) is not { } quad) return;
        (list ?? Background).AddImageQuad(sprite.Texture, quad.A, quad.B, quad.C, quad.D, sprite.Uv0, new Vector2(sprite.Uv1.X, sprite.Uv0.Y), sprite.Uv1, new Vector2(sprite.Uv0.X, sprite.Uv1.Y), EColor.U32(tint ?? Color.White));
    }

    public static ClipScope Clip(Vector2 min, Vector2 max, ImDrawListPtr? list = null)
    {
        var target = list ?? Background;
        target.PushClipRect(min, max, true);
        return new ClipScope(target);
    }

    public static LayerScope Layer(DrawLayer layer)
    {
        var list = Background;
        if (list.Splitter.Count < 3) return default;
        var previous = list.Splitter.Current;
        list.ChannelsSetCurrent((int)layer);
        return new LayerScope(previous);
    }

    internal static void SplitLayers()
    {
        var list = Background;
        if (list.Splitter.Count > 1) return;
        list.ChannelsSplit(3);
        list.ChannelsSetCurrent((int)DrawLayer.Middle);
    }

    internal static void ResetLayer()
    {
        if (Background.Splitter.Count >= 3) Background.ChannelsSetCurrent((int)DrawLayer.Middle);
    }

    internal static void MergeLayers() => Background.ChannelsMerge();

    internal static void RegularPoints(Span<Vector2> into, Vector2 centre, float radius, float start)
    {
        for (var i = 0; i < into.Length; i++)
        {
            var angle = start + i * MathF.Tau / into.Length;
            into[i] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }

    internal static void StarPoints(Span<Vector2> into, Vector2 centre, float outer, float inner)
    {
        for (var i = 0; i < into.Length; i++)
        {
            var angle = -MathF.PI / 2 + i * MathF.Tau / into.Length;
            into[i] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (i % 2 == 0 ? outer : inner);
        }
    }

    internal static Vector2 AlignOffset(Vector2 size, Align align) => new(
        align.HasFlag(Align.Right) ? size.X : align.HasFlag(Align.Center) ? size.X / 2 : 0,
        align.HasFlag(Align.Bottom) ? size.Y : align.HasFlag(Align.Middle) ? size.Y / 2 : 0);

    internal static (Vector2 Left, Vector2 Right)? ArrowHead(Vector2 from, Vector2 to, float head)
    {
        var along = to - from;
        if (along.LengthSquared() < 1e-6f) return null;
        along = Vector2.Normalize(along);
        var side = new Vector2(-along.Y, along.X) * head / 2;
        var back = to - along * head;
        return (back + side, back - side);
    }

    internal static (Vector2 A, Vector2 B, Vector2 C, Vector2 D)? RotatedQuad(Vector2 centre, Vector2 size, Vector2 facing)
    {
        if (facing.LengthSquared() < 1e-6f) return null;
        var up = Vector2.Normalize(new Vector2(-facing.Y, facing.X));
        var right = new Vector2(-up.Y, up.X);
        var h = size / 2;
        return (centre - right * h.X + up * h.Y, centre + right * h.X + up * h.Y, centre + right * h.X - up * h.Y, centre - right * h.X - up * h.Y);
    }

    static (ImFontPtr Font, float Size, Vector2 Origin, Vector2 Measured) Place(string text, Vector2 pos, Align align, float size, bool mono)
    {
        var font = mono ? Fonts.Mono : Fonts.Regular;
        if (font.IsNull) font = ImGui.GetFont();
        var px = size > 0 ? size : mono ? Fonts.MonoSize : BaseSize;
        if (px <= 0) px = ImGui.GetFontSize();
        var measured = ImGui.CalcTextSizeA(font, px, float.MaxValue, 0, text);
        return (font, px, pos - AlignOffset(measured, align), measured);
    }
}

public readonly struct ClipScope : IDisposable
{
    readonly ImDrawListPtr _list;

    internal ClipScope(ImDrawListPtr list) => _list = list;

    public void Dispose() => _list.PopClipRect();
}

public enum DrawLayer { Back, Middle, Front }

public enum MarkerShape { Dot, Diamond, Square, Triangle, Hexagon, Star, Sparkle }

public readonly struct LayerScope : IDisposable
{
    readonly int _previous;

    internal LayerScope(int previous) => _previous = previous;

    public void Dispose()
    {
        var list = Draw.Background;
        if (list.Splitter.Count >= 3) list.ChannelsSetCurrent(_previous);
    }
}
