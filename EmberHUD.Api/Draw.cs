using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD;

[Flags]
public enum Align { TopLeft = 0, Center = 1, Right = 2, Middle = 4, Bottom = 8 }

public static class Draw
{
    static readonly Color MarkerRim = Color.FromArgb(0x08, 0x09, 0x0B);
    static readonly Color BadgeFill = Color.FromArgb(0x14, 0x16, 0x1A);
    static readonly Vector2[] Stroke = [new(-1, -1), new(0, -1), new(1, -1), new(-1, 0), new(1, 0), new(-1, 1), new(0, 1), new(1, 1)];

    static readonly Vector2[] UnitCircle = [.. Enumerable.Range(0, GlowSegments).Select(i => new Vector2(MathF.Cos(i * MathF.Tau / GlowSegments), MathF.Sin(i * MathF.Tau / GlowSegments)))];

    internal static readonly Dictionary<string, ParsedIcon> ParsedIcons = [];

    public const string MarkerPrefix = "marker:";

    public const string GameIconPrefix = "game:";

    public const string SvgIconPrefix = "svg:";

    const int MaxPolylinePoints = 8000;

    const float RowGap = 4;

    const float DimmedOpacity = 0.45f;

    internal const int GlowSegments = 32;

    internal const int Layers = 3;

    public static ImDrawListPtr Background => ImGui.GetBackgroundDrawList();

    public static ImDrawListPtr Foreground => ImGui.GetForegroundDrawList();

    public static Vector2 Text(string text, Vector2 pos, Color color, Align align = Align.TopLeft, float size = 0, FontFace face = FontFace.Regular, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, face);
        Glyphs(list ?? Background, font, px, origin, text, color);
        return measured;
    }

    public static Vector2 Text(string text, Vector2 pos, Ui.TextStyle style, Align align = Align.TopLeft, ImDrawListPtr? list = null)
    {
        var (font, px, origin, box) = Place(text, pos, align, SizeOf(style), style.Face);
        var target = list ?? Background;
        var pad = TextPad(px);
        if (style.BgEnabled) target.AddRectFilled(origin - pad, origin + box + pad, EColor.U32(style.BgColor), style.BorderRounding);
        if (style.BorderEnabled) target.AddRect(origin - pad, origin + box + pad, EColor.U32(style.BorderColor), style.BorderRounding, ImDrawFlags.None, style.BorderThickness);
        if (style.ShadowEnabled) ImGui.AddText(target, font, px, origin + Vector2.One, EColor.U32(style.ShadowColor), text);
        Glyphs(target, font, px, origin, text, style.Color, style.StrokeEnabled ? style.StrokeColor : null);
        return box;
    }

    public static Vector2 TextOutlined(string text, Vector2 pos, Color color, Color outline, float thickness = 1, Align align = Align.TopLeft, float size = 0, FontFace face = FontFace.Regular, ImDrawListPtr? list = null)
    {
        var (font, px, origin, measured) = Place(text, pos, align, size, face);
        Glyphs(list ?? Background, font, px, origin, text, color, outline, thickness);
        return measured;
    }

    public static Vector2 MeasureText(string text, float size = 0, FontFace face = FontFace.Regular) => Place(text, Vector2.Zero, Align.TopLeft, size, face).Measured;

    public static Vector2 MeasureText(string text, Ui.TextStyle style) => MeasureText(text, SizeOf(style), style.Face);

    public static float RowHeight(Ui.TextStyle style) => MeasureText("Ag", style).Y + RowGap;

    public static float Row(Vector2 pos, float width, string left, string right, Ui.TextStyle style, Color? swatch = null, bool dimmed = false, ImDrawListPtr? list = null)
    {
        var rowHeight = RowHeight(style);
        var fade = dimmed ? DimmedOpacity : 1f;
        var text = style.Clone();
        text.Color = Color.FromArgb((int)(style.Color.A * fade), style.Color);
        var textX = pos.X;
        if (swatch is { } colour)
        {
            var side = rowHeight - RowGap;
            RectFilled(pos, pos + new Vector2(side, side), Color.FromArgb((int)(colour.A * fade), colour), list: list);
            textX += rowHeight;
        }
        Text(left, new Vector2(textX, pos.Y), text, list: list);
        Text(right, new Vector2(pos.X + width, pos.Y), text, Align.Right, list);
        return rowHeight;
    }

    public static void Line(Vector2 a, Vector2 b, Color color, float thickness = 1, ImDrawListPtr? list = null) => (list ?? Background).AddLine(a, b, EColor.U32(color), thickness);

    public static void Polyline(ReadOnlySpan<Vector2> points, Color color, float thickness = 1, bool closed = false, ImDrawListPtr? list = null)
    {
        if (points.Length < 2) return;
        var target = list ?? Background;
        var col = EColor.U32(color);
        if (points.Length <= MaxPolylinePoints)
        {
            target.AddPolyline(ref MemoryMarshal.GetReference(points), points.Length, col, closed ? ImDrawFlags.Closed : ImDrawFlags.None, thickness);
            return;
        }
        for (var start = 0; start < points.Length - 1; start += MaxPolylinePoints - 1)
        {
            var chunk = points.Slice(start, Math.Min(MaxPolylinePoints, points.Length - start));
            target.AddPolyline(ref MemoryMarshal.GetReference(chunk), chunk.Length, col, ImDrawFlags.None, thickness);
        }
        if (closed) target.AddLine(points[^1], points[0], col, thickness);
    }

    public static void Rect(Vector2 min, Vector2 max, Color color, float thickness = 1, float rounding = 0, ImDrawListPtr? list = null) => (list ?? Background).AddRect(min, max, EColor.U32(color), rounding, ImDrawFlags.None, thickness);

    public static void RectFilled(Vector2 min, Vector2 max, Color color, float rounding = 0, ImDrawListPtr? list = null) => (list ?? Background).AddRectFilled(min, max, EColor.U32(color), rounding);

    public static void RectGradient(Vector2 min, Vector2 max, Color left, Color right, ImDrawListPtr? list = null) =>
        (list ?? Background).AddRectFilledMultiColor(min, max, EColor.U32(left), EColor.U32(right), EColor.U32(right), EColor.U32(left));

    public static void Bar(Vector2 min, Vector2 max, float fraction, Color fill, Color track, ImDrawListPtr? list = null)
    {
        RectFilled(min, max, track, 0, list);
        RectFilled(min, new Vector2(min.X + (max.X - min.X) * (float.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1)), max.Y), fill, 0, list);
    }

    public static void Circle(Vector2 centre, float radius, Color color, float thickness = 1, int segments = 0, ImDrawListPtr? list = null) => (list ?? Background).AddCircle(centre, radius, EColor.U32(color), segments, thickness);

    public static void CircleFilled(Vector2 centre, float radius, Color color, int segments = 0, ImDrawListPtr? list = null) => (list ?? Background).AddCircleFilled(centre, radius, EColor.U32(color), segments);

    public static void TriangleFilled(Vector2 a, Vector2 b, Vector2 c, Color color, ImDrawListPtr? list = null) => (list ?? Background).AddTriangleFilled(a, b, c, EColor.U32(color));

    public static void ConvexPolyFilled(ReadOnlySpan<Vector2> points, Color color, ImDrawListPtr? list = null)
    {
        if (points.Length < 3) return;
        var target = list ?? Background;
        target.AddConvexPolyFilled(ref MemoryMarshal.GetReference(points), points.Length, EColor.U32(color));
    }

    public static void Glow(Vector2 centre, float radius, Color color, float inner = 0, ImDrawListPtr? list = null)
    {
        if (radius <= 0 || color.A == 0) return;
        var target = list ?? Background;
        var solid = EColor.U32(color);
        var clear = EColor.U32(Color.FromArgb(0, color));
        var uv = ImGui.GetFontTexUvWhitePixel();
        target.PrimReserve(GlowSegments * 9, 1 + GlowSegments * 2);
        var first = target.VtxCurrentIdx;
        target.PrimWriteVtx(centre, uv, solid);
        for (var i = 0; i < GlowSegments; i++)
        {
            var direction = UnitCircle[i];
            target.PrimWriteVtx(centre + direction * MathF.Min(inner, radius), uv, solid);
            target.PrimWriteVtx(centre + direction * radius, uv, clear);
        }
        for (var i = 0; i < GlowSegments; i++)
        {
            var near = (ushort)(first + 1 + i * 2);
            var far = (ushort)(near + 1);
            var nextNear = (ushort)(first + 1 + (i + 1) % GlowSegments * 2);
            var nextFar = (ushort)(nextNear + 1);
            foreach (var index in (ReadOnlySpan<ushort>)[(ushort)first, near, nextNear, near, far, nextFar, near, nextFar, nextNear]) target.PrimWriteIdx(index);
        }
    }

    public static void GlowRect(Vector2 min, Vector2 max, Color color, float thickness = 2, float spread = 8, ImDrawListPtr? list = null)
    {
        if (color.A == 0 || thickness <= 0) return;
        var target = list ?? Background;
        var rings = (int)MathF.Ceiling(spread / 2);
        for (var ring = rings; ring >= 1; ring--)
        {
            var grow = new Vector2(thickness / 2 + ring * 2);
            var alpha = (int)(color.A * 0.5f * (1 - ring / (float)(rings + 1)));
            target.AddRect(min - grow, max + grow, EColor.U32(Color.FromArgb(alpha, color)), 0, ImDrawFlags.None, 2);
        }
        target.AddRect(min, max, EColor.U32(color), 0, ImDrawFlags.None, thickness);
    }

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

    internal static bool TryMarker(string? icon, out MarkerShape shape)
    {
        if (icon is not null && icon.StartsWith(MarkerPrefix, StringComparison.Ordinal) && Enum.TryParse(icon.AsSpan(MarkerPrefix.Length), out shape) && Enum.IsDefined(shape)) return true;
        shape = MarkerShape.Dot;
        return false;
    }

    internal delegate Sprite? GameIconResolver(ReadOnlySpan<char> texture);

    internal static GameIconResolver? GameIcons { get; set; }

    internal static IReadOnlyList<string> GameIconNames { get; set; } = [];

    public static bool TryGameIcon(ReadOnlySpan<char> texture, out Sprite sprite)
    {
        var found = texture.IsEmpty ? null : GameIcons?.Invoke(texture);
        sprite = found ?? default;
        return found is not null;
    }

    internal static Func<string, int, Color, Color, Sprite?>? SvgIcons { get; set; }

    internal static IReadOnlyList<string> SvgIconNames { get; set; } = [];

    internal readonly record struct ParsedIcon(MarkerShape Shape, string? GameTexture, bool IsSvg);

    static ParsedIcon Parse(string icon)
    {
        if (ParsedIcons.TryGetValue(icon, out var parsed)) return parsed;
        if (ParsedIcons.Count >= 256) ParsedIcons.Clear();
        parsed = TryMarker(icon, out var shape) ? new ParsedIcon(shape, null, false)
            : icon.StartsWith(GameIconPrefix, StringComparison.Ordinal) ? new ParsedIcon(MarkerShape.Dot, icon[GameIconPrefix.Length..], false)
            : new ParsedIcon(MarkerShape.Dot, null, icon.StartsWith(SvgIconPrefix, StringComparison.Ordinal));
        return ParsedIcons[icon] = parsed;
    }

    public static void Icon(string? icon, Vector2 centre, float radius, Color color, float glow = 0, Color? accent = null, Vector2? facing = null, ImDrawListPtr? list = null)
    {
        var sprite = default(Sprite);
        var tint = color;
        var found = false;
        var parsed = icon is null ? default : Parse(icon);
        var shape = parsed.Shape;
        if (parsed.GameTexture is { } texture) found = TryGameIcon(texture, out sprite);
        else if (parsed.IsSvg)
        {
            tint = Color.FromArgb(color.A, Color.White);
            if (radius > 0 && float.IsFinite(radius) && SvgIcons?.Invoke(icon!, (int)MathF.Ceiling(radius * 2), color, accent ?? GameColors.MinimapIconAccent) is { } svg)
            {
                sprite = svg;
                found = true;
            }
        }

        if (!found)
        {
            Marker(centre, radius, color, shape, glow, list);
            return;
        }

        if (glow > 0) Glow(centre, radius * glow * 1.9f, Color.FromArgb((int)(color.A * 0.3f), color), radius * 0.5f, list);
        if (facing is { } turn && float.IsFinite(turn.X) && float.IsFinite(turn.Y) && turn.LengthSquared() >= 1e-6f)
            ImageRotated(sprite, centre, new Vector2(radius * 2), new Vector2(turn.Y, -turn.X), tint, list);
        else
            Image(sprite, centre - new Vector2(radius), centre + new Vector2(radius), tint, list);
    }

    public static void Image(Sprite sprite, Vector2 min, Vector2 max, Color? tint = null, ImDrawListPtr? list = null) =>
        (list ?? Background).AddImage(sprite.Texture, min, max, sprite.Uv0, sprite.Uv1, EColor.U32(tint ?? Color.White));

    public static void ImageQuad(Sprite sprite, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color? tint = null, ImDrawListPtr? list = null) =>
        (list ?? Background).AddImageQuad(sprite.Texture, a, b, c, d, sprite.Uv0, new Vector2(sprite.Uv1.X, sprite.Uv0.Y), sprite.Uv1, new Vector2(sprite.Uv0.X, sprite.Uv1.Y), EColor.U32(tint ?? Color.White));

    public static void ImageRotated(Sprite sprite, Vector2 centre, Vector2 size, Vector2 facing, Color? tint = null, ImDrawListPtr? list = null)
    {
        if (RotatedQuad(centre, size, facing) is not { } quad) return;
        ImageQuad(sprite, quad.A, quad.B, quad.C, quad.D, tint, list);
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
        if (list.Splitter.Count < Layers) return default;
        var previous = list.Splitter.Current;
        list.ChannelsSetCurrent((int)layer);
        return new LayerScope(previous);
    }

    internal static void SplitLayers()
    {
        var list = Background;
        if (list.Splitter.Count > 1) return;
        list.ChannelsSplit(Layers);
        list.ChannelsSetCurrent((int)DrawLayer.Middle);
    }

    internal static void ResetLayer()
    {
        if (Background.Splitter.Count >= Layers) Background.ChannelsSetCurrent((int)DrawLayer.Middle);
    }

    internal static void MergeLayers() => Background.ChannelsMerge();

    internal static Vector2 TextPad(float size) => new(MathF.Round(size * 0.5f), MathF.Round(size * 0.2f));

    static float SizeOf(Ui.TextStyle style)
    {
        var faceSize = Fonts.Of(style.Face).Size;
        return (faceSize > 0 ? faceSize : ImGui.GetFontSize()) * (style.Scale > 0 ? style.Scale : 1);
    }

    static void Glyphs(ImDrawListPtr target, ImFontPtr font, float px, Vector2 origin, string text, Color color, Color? outline = null, float thickness = 1)
    {
        if (outline is { } ring)
            foreach (var offset in Stroke) ImGui.AddText(target, font, px, origin + offset * thickness, EColor.U32(ring), text);
        ImGui.AddText(target, font, px, origin, EColor.U32(color), text);
    }

    static (ImFontPtr Font, float Size, Vector2 Origin, Vector2 Measured) Place(string text, Vector2 pos, Align align, float size, FontFace face)
    {
        var (font, faceSize) = Fonts.Of(face);
        if (font.IsNull) font = ImGui.GetFont();
        var px = size > 0 ? size : faceSize > 0 ? faceSize : ImGui.GetFontSize();
        var measured = ImGui.CalcTextSizeA(font, px, float.MaxValue, 0, text);
        return (font, px, pos - AlignOffset(measured, align), measured);
    }

    internal static Vector2 AlignOffset(Vector2 size, Align align) => new(
        align.HasFlag(Align.Right) ? size.X : align.HasFlag(Align.Center) ? size.X / 2 : 0,
        align.HasFlag(Align.Bottom) ? size.Y : align.HasFlag(Align.Middle) ? size.Y / 2 : 0);

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

    internal static (Vector2 A, Vector2 B, Vector2 C, Vector2 D)? RotatedQuad(Vector2 centre, Vector2 size, Vector2 facing)
    {
        if (facing.LengthSquared() < 1e-6f) return null;
        var up = Vector2.Normalize(new Vector2(-facing.Y, facing.X));
        var right = new Vector2(-up.Y, up.X);
        var h = size / 2;
        return (centre - right * h.X + up * h.Y, centre + right * h.X + up * h.Y, centre + right * h.X - up * h.Y, centre - right * h.X - up * h.Y);
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
        if (list.Splitter.Count >= Draw.Layers) list.ChannelsSetCurrent(_previous);
    }
}
