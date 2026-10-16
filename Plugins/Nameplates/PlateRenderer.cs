using System;
using System.Collections.Generic;
using System.Numerics;
using EmberHUD;

namespace NameplatesPlugin;

public static class PlateRenderer
{
    const float TextGap = 2;
    const int MaxCachedTemplates = 128;

    static readonly Dictionary<string, Template> Templates = new();

    public static bool DrawPlate(in PlateData data, PlateStyle style, Vector2 anchor, Vector2 viewport)
    {
        var bar = style.Bar;
        var centre = new Vector2(anchor.X, anchor.Y - style.OffsetY);
        var half = new Vector2(bar.Width, bar.Height) / 2;
        var min = centre - half;
        var max = centre + half;
        if (max.X < 0 || max.Y < 0 || min.X > viewport.X || min.Y > viewport.Y) return false;

        if (data.MaxLife > 0 && !double.IsNaN(data.Life)) DrawBar(bar, min, max, (float)Math.Clamp(data.Life / data.MaxLife, 0, 1));
        DrawLine(style.Above, data, min, max, new Vector2(0, min.Y - TextGap), Align.Bottom);
        DrawLine(style.OnBar, data, min, max, new Vector2(0, centre.Y), Align.Middle);
        DrawLine(style.Below, data, min, max, new Vector2(0, max.Y + TextGap), Align.TopLeft);
        return true;
    }

    static void DrawBar(BarStyle bar, Vector2 min, Vector2 max, float fraction)
    {
        Draw.RectFilled(min, max, bar.Back);
        if (fraction > 0)
        {
            var fillMax = new Vector2(min.X + (max.X - min.X) * fraction, max.Y);
            if (BarTextures.Get(bar.Texture) is { } sprite)
                Draw.Image(sprite with { Uv1 = new Vector2(sprite.Uv0.X + (sprite.Uv1.X - sprite.Uv0.X) * fraction, sprite.Uv1.Y) }, min, fillMax, bar.Fill);
            else
                Draw.RectFilled(min, fillMax, bar.Fill);
        }
        if (bar.BorderWidth > 0) Draw.Rect(min, max, bar.Border, bar.BorderWidth);
    }

    static void DrawLine(TextLine line, in PlateData data, Vector2 min, Vector2 max, Vector2 at, Align vertical)
    {
        if (!line.Enabled) return;
        var text = TemplateFor(line.Template).Render(data);
        if (text.Length == 0) return;
        var horizontal = line.Align & (Align.Center | Align.Right);
        at.X = horizontal == Align.Right ? max.X : horizontal == Align.Center ? (min.X + max.X) / 2 : min.X;
        Draw.Text(text, at, line.Style, horizontal | vertical);
    }

    static Template TemplateFor(string text)
    {
        if (Templates.TryGetValue(text, out var cached)) return cached;
        if (Templates.Count >= MaxCachedTemplates) Templates.Clear();
        return Templates[text] = Template.Parse(text);
    }
}
