using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace TorchlightSDK.Ui;

public static partial class Controls
{
    public readonly record struct ToggleItem(string Label, Func<bool> Get, Action<bool> Set, string? Tip = null);

    public readonly record struct ColorItem(string Label, Func<SColor> Get, Action<SColor> Set);

    public static float LabelStride(params string[] labels)
    {
        float w = 0f;
        foreach (var l in labels) w = Math.Max(w, ImGui.CalcTextSize(l).X);
        return w + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X * 2;
    }

    public static int FitColumns(float avail, float stride, int max = 0)
    {
        if (stride <= 0f) return 1;
        int n = (int)Math.Floor(avail / stride);
        if (n < 1) n = 1;
        if (max > 0 && n > max) n = max;
        return n;
    }

    public static bool Grid(string id, int count, float stride, Func<int, bool>? cell, int columns = 0)
    {
        if (count < 1 || cell == null) return false;
        if (columns < 1) columns = FitColumns(ImGui.GetContentRegionAvail().X, stride);

        bool d = false;
        float x0 = ImGui.GetCursorPosX();
        ImGui.PushID(id);
        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            if (col > 0)
            {
                ImGui.SameLine(0, 0);
                ImGui.SetCursorPosX(x0 + col * stride);
            }
            ImGui.PushID(i);
            d |= cell(i);
            ImGui.PopID();
        }
        ImGui.PopID();
        return d;
    }

    static float ItemStride<T>(T[] items, Func<T, string> label)
    {
        var labels = new string[items.Length];
        for (int i = 0; i < items.Length; i++) labels[i] = label(items[i]);
        return LabelStride(labels);
    }

    public static bool ToggleGrid(string id, ToggleItem[]? items, int columns = 2)
    {
        if (items == null || items.Length == 0) return false;
        return Grid(id, items.Length, ItemStride(items, it => it.Label), i =>
        {
            bool v = items[i].Get();
            bool changed = ImGui.Checkbox(items[i].Label + "##t", ref v);
            if (changed) items[i].Set(v);
            Text.Tip(items[i].Tip);
            return changed;
        }, columns);
    }

    public static bool ToggleGrid(string id, string[]? labels, bool[]? values, int columns = 2, string[]? tips = null)
    {
        if (labels == null || values == null) return false;
        int n = Math.Min(labels.Length, values.Length);
        if (n == 0) return false;
        return Grid(id, n, LabelStride(labels), i =>
        {
            bool changed = ImGui.Checkbox(labels[i] + "##t", ref values[i]);
            if (tips != null && i < tips.Length) Text.Tip(tips[i]);
            return changed;
        }, columns);
    }

    public static bool ColorGrid(string id, ColorItem[]? items, int columns = 0, bool alpha = true)
    {
        if (items == null || items.Length == 0) return false;
        return Grid(id, items.Length, ItemStride(items, it => it.Label), i =>
        {
            SColor c = items[i].Get();
            if (Color(items[i].Label, ref c, "c", alpha)) { items[i].Set(c); return true; }
            return false;
        }, columns);
    }

    public static bool ColorGrid(string id, string[]? labels, SColor[]? values, int columns = 0, bool alpha = true)
    {
        if (labels == null || values == null) return false;
        int n = Math.Min(labels.Length, values.Length);
        if (n == 0) return false;
        return Grid(id, n, LabelStride(labels), i => Color(labels[i], ref values[i], "c", alpha), columns);
    }
}
