using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Bars
{
    public static void Progress(float fraction, string? overlay = null, float height = 0f)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (width <= 0f) return;
        if (height <= 0f) height = ImGui.GetTextLineHeight();

        var min = ImGui.GetCursorScreenPos();
        var max = new Vector2(min.X + width, min.Y + height);
        var dl = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        var fill = ImGui.GetColorU32(ImGuiCol.PlotHistogram);

        dl.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.FrameBg), rounding);
        if (fraction < 0f)
        {
            var span = width * 0.2f;
            var t = (float)(Math.Sin(ImGui.GetTime() * 1.6) * 0.5 + 0.5);
            var x = min.X + (width - span) * t;
            dl.AddRectFilled(new Vector2(x, min.Y), new Vector2(x + span, max.Y), fill, rounding);
        }
        else
        {
            var filled = width * Math.Clamp(fraction, 0f, 1f);
            if (filled > 0f) dl.AddRectFilled(min, new Vector2(min.X + filled, max.Y), fill, rounding);
        }

        ImGui.Dummy(new Vector2(width, height));
        if (string.IsNullOrEmpty(overlay)) return;
        var size = ImGui.CalcTextSize(overlay);
        dl.AddText(new Vector2(min.X + (width - size.X) * 0.5f, min.Y + (height - size.Y) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), overlay);
    }

    public static void Strip(ReadOnlySpan<uint> colors, float height = 0f)
    {
        if (colors.Length == 0) return;
        var width = ImGui.GetContentRegionAvail().X;
        if (width <= 0f) return;
        if (height <= 0f) height = ImGui.GetTextLineHeight();

        var min = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var step = width / colors.Length;
        for (var i = 0; i < colors.Length; i++)
        {
            var x0 = min.X + step * i;
            var x1 = Math.Min(min.X + width, Math.Max(x0 + 1f, x0 + step - 1f));
            dl.AddRectFilled(new Vector2(x0, min.Y), new Vector2(x1, min.Y + height), colors[i]);
        }
        ImGui.Dummy(new Vector2(width, height));
    }
}
