using System.Drawing;
using System.Numerics;

namespace EmberHUD.Ui;

public readonly record struct IconBarRects(Vector2 IconMin, Vector2 IconMax, Vector2 BarMin, Vector2 BarMax, Vector2 Min, Vector2 Max);

public static class IconBar
{
    public static IconBarRects Layout(Vector2 origin, Vector2 barSize, float iconSize, Controls.IconSide side, float gap = 4f)
    {
        var spacing = iconSize > 0 ? gap : 0f;
        var left = side == Controls.IconSide.Left;
        var barMin = new Vector2(left ? origin.X + iconSize + spacing : origin.X, origin.Y);
        var barMax = barMin + barSize;
        var iconMin = new Vector2(left ? origin.X : barMax.X + spacing, origin.Y + (barSize.Y - iconSize) / 2f);
        var iconMax = iconSize > 0 ? iconMin + new Vector2(iconSize) : iconMin;
        return new IconBarRects(iconMin, iconMax, barMin, barMax, Vector2.Min(iconMin, barMin), Vector2.Max(iconMax, barMax));
    }

    public static void DrawBar(IconBarRects rects, float fill, Color fillColor, Color back, float opacity)
    {
        var fraction = float.IsNaN(fill) ? 0f : Math.Clamp(fill, 0f, 1f);
        var fillMax = new Vector2(rects.BarMin.X + (rects.BarMax.X - rects.BarMin.X) * fraction, rects.BarMax.Y);
        Draw.RectFilled(rects.BarMin, rects.BarMax, Faded(back, opacity));
        Draw.RectFilled(rects.BarMin, fillMax, Faded(fillColor, opacity));
    }

    static Color Faded(Color colour, float opacity) => Color.FromArgb((int)(colour.A * opacity), colour);
}
