using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public enum ToastLevel { Info, Success, Warning, Error }

public enum ToastGrow { Up, Down, Default }

public sealed class ToastStyle
{
    public float TextSize = 16f;
    public float Padding = 6f;
    public float MaxWidth = 320f;
    public double Duration = 4.0;
    public Color Background = Color.FromArgb(230, 20, 22, 28);
    public Color Border = Color.FromArgb(255, 60, 64, 74);
    public float BorderThickness = 1f;
    public Color TextColor = Color.FromArgb(255, 230, 232, 238);
    public int Max = 5;
    public float AccentWidth = 4f;
    public double FadeSeconds = 0.5;
    public ToastGrow Grow = ToastGrow.Default;
}

public sealed class ToastHost
{
    sealed record Toast(string Text, ToastLevel Level, long ShownAt, double Duration);

    readonly List<Toast> _toasts = [];

    public int Count
    {
        get
        {
            var now = Stopwatch.GetTimestamp();
            return _toasts.Count(t => !Expired(t, now));
        }
    }

    static bool Expired(Toast t, long now) => Stopwatch.GetElapsedTime(t.ShownAt, now).TotalSeconds >= t.Duration;

    public void Clear() => _toasts.Clear();

    public void Show(string? text, ToastStyle style, ToastLevel level = ToastLevel.Info, double? seconds = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var duration = seconds ?? style.Duration;
        if (!(duration >= 0)) return;
        _toasts.Add(new Toast(text, level, Stopwatch.GetTimestamp(), duration));
        var max = Math.Max(style.Max, 0);
        if (_toasts.Count > max) _toasts.RemoveRange(0, _toasts.Count - max);
    }

    public static Color Accent(ToastLevel l) => l switch
    {
        ToastLevel.Success => Color.FromArgb(255, 120, 210, 120),
        ToastLevel.Warning => Color.FromArgb(255, 230, 180, 90),
        ToastLevel.Error => Color.FromArgb(255, 230, 110, 100),
        _ => Color.FromArgb(255, 150, 190, 240),
    };

    public static float FadeAlpha(double remaining, double fade) => remaining >= fade ? 1f : (float)Math.Clamp(remaining / fade, 0, 1);

    public static bool GrowsUp(ToastGrow grow, float topY, float screenH) => grow == ToastGrow.Up || (grow == ToastGrow.Default && topY > screenH * 0.5f);

    public void Draw(float anchorX, float topY, ToastStyle st)
    {
        var now = Stopwatch.GetTimestamp();
        _toasts.RemoveAll(x => Expired(x, now));
        if (_toasts.Count == 0) return;

        var dl = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var baseSize = ImGui.GetFontSize();
        if (baseSize <= 0) baseSize = 16f;
        var size = st.TextSize;
        var scale = size / baseSize;
        var pad = st.Padding;
        var lineH = MathF.Ceiling(size) + 4f;
        const float gap = 6f;

        (Vector2 size, List<string> rows) Measure(string text)
        {
            var maxTextW = st.MaxWidth > 0 ? Math.Max(10f, st.MaxWidth - pad * 2 - st.AccentWidth) : float.MaxValue;
            List<string> rows = st.MaxWidth > 0 && ImGui.CalcTextSize(text).X * scale > maxTextW ? Text.Wrap(text, maxTextW, scale) : [text];
            var contentW = rows.Max(r => ImGui.CalcTextSize(r).X * scale);
            return (new Vector2(contentW + pad * 2 + st.AccentWidth, rows.Count * lineH + pad * 2), rows);
        }

        var boxes = _toasts.Select(t => Measure(t.Text)).ToArray();
        var up = GrowsUp(st.Grow, topY, ImGui.GetIO().DisplaySize.Y);

        var cursor = topY;
        for (var idx = 0; idx < _toasts.Count; idx++)
        {
            var i = up ? _toasts.Count - 1 - idx : idx;
            var t = _toasts[i];
            var (boxSize, rows) = boxes[i];
            var top = up ? cursor - boxSize.Y : cursor;
            cursor = up ? top - gap : top + boxSize.Y + gap;

            var alpha = FadeAlpha(t.Duration - Stopwatch.GetElapsedTime(t.ShownAt, now).TotalSeconds, st.FadeSeconds);
            var min = new Vector2(anchorX - boxSize.X / 2f, top);
            var max = min + boxSize;

            var bg = EColor.Fade(st.Background, alpha * st.Background.A / 255f);
            if (bg.A > 0) dl.AddRectFilled(min, max, EColor.U32(bg));
            dl.AddRectFilled(min, new Vector2(min.X + st.AccentWidth, max.Y), EColor.U32(EColor.Fade(Accent(t.Level), alpha)));
            if (st.BorderThickness > 0) dl.AddRect(min, max, EColor.U32(EColor.Fade(st.Border, alpha * st.Border.A / 255f)), 0f, ImDrawFlags.None, st.BorderThickness);

            var textCol = EColor.U32(EColor.Fade(st.TextColor, alpha * st.TextColor.A / 255f));
            var p = new Vector2(min.X + st.AccentWidth + pad, min.Y + pad);
            foreach (var r in rows)
            {
                ImGui.AddText(dl, font, size, p, textCol, r);
                p.Y += lineH;
            }
        }
    }
}

public static class MessageBar
{
    public static float WrapWidth(float total, float accent, float pad, float reserved) => Math.Max(total - accent - pad * 2 - reserved, 1f);

    public static bool Draw(string id, ToastLevel level, string? text, string? action = null, float width = 0f)
    {
        var open = true;
        return Core(id, level, text, ref open, false, action, width);
    }

    public static bool Draw(string id, ToastLevel level, string? text, ref bool open, string? action = null, float width = 0f) => Core(id, level, text, ref open, true, action, width);

    static bool Core(string id, ToastLevel level, string? text, ref bool open, bool dismissable, string? action, float width)
    {
        if (!open) return false;
        var style = ImGui.GetStyle();
        var body = text ?? "";
        var act = string.IsNullOrEmpty(action) ? null : action;
        var pad = style.FramePadding.X + 2f;
        const float accent = 3f;
        var total = width > 0 ? width : ImGui.GetContentRegionAvail().X;

        var actW = act is not null ? ImGui.CalcTextSize(act).X + style.FramePadding.X * 2 : 0f;
        var xW = dismissable ? ImGui.GetFrameHeight() : 0f;
        var reserved = actW + xW + (actW > 0f ? style.ItemSpacing.X : 0f) + (xW > 0f ? style.ItemSpacing.X : 0f);
        var wrap = WrapWidth(total, accent, pad, reserved);

        var ts = ImGui.CalcTextSize(body, false, wrap);
        var rowH = ImGui.GetFrameHeight();
        var h = Math.Max(ts.Y, reserved > 0 ? rowH : 0f) + style.FramePadding.Y * 2;

        var org = ImGui.GetCursorScreenPos();
        var size = new Vector2(total, h);
        var dl = ImGui.GetWindowDrawList();
        var col = ToastHost.Accent(level);
        var r = style.FrameRounding;
        dl.AddRectFilled(org, org + size, EColor.U32(EColor.Fade(col, 0.14f)), r);
        dl.AddRect(org, org + size, EColor.U32(EColor.Fade(col, 0.5f)), r);
        dl.AddRectFilled(org, new Vector2(org.X + accent + r, org.Y + h), EColor.U32(col), r);
        dl.AddRectFilled(new Vector2(org.X + accent, org.Y), new Vector2(org.X + accent + r, org.Y + h), EColor.U32(EColor.Fade(col, 0.14f)));
        ImGui.AddText(dl, ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(org.X + accent + pad, org.Y + style.FramePadding.Y), ImGui.GetColorU32(ImGuiCol.Text), body, wrap);

        var clicked = false;
        ImGui.PushID(id);
        var bx = org.X + total - pad;
        var by = org.Y + (h - rowH) * 0.5f;
        if (dismissable)
        {
            bx -= xW;
            ImGui.SetCursorScreenPos(new Vector2(bx, by));
            if (ImGui.Button("x##close", new Vector2(xW, rowH))) open = false;
            bx -= style.ItemSpacing.X;
        }
        if (act is not null)
        {
            bx -= actW;
            ImGui.SetCursorScreenPos(new Vector2(bx, by));
            clicked = ImGui.Button(act + "##act", new Vector2(actW, rowH));
        }
        ImGui.PopID();

        ImGui.SetCursorScreenPos(org);
        ImGui.Dummy(size);
        return clicked;
    }
}
