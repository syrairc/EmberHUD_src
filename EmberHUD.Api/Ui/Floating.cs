using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Floating
{
    static readonly HashSet<uint> _dragging = [];

    internal static Vector2 ButtonSize(Vector2 size, Vector2 labelSize, Vector2 framePadding) => size.X <= 0f || size.Y <= 0f ? labelSize + framePadding * 2f : size;

    public static bool Button(string id, string label, ref Vector2 pos, Vector2 size = default)
    {
        var key = ImGui.GetID(id);
        var style = ImGui.GetStyle();
        size = ButtonSize(size, ImGui.CalcTextSize(label), style.FramePadding);

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

        var clicked = false;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin($"##float_{key:X8}", flags))
        {
            ImGui.InvisibleButton(id, size);
            var hovered = ImGui.IsItemHovered();
            var held = ImGui.IsItemActive();

            if (held && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                _dragging.Add(key);
                pos += ImGui.GetIO().MouseDelta;
            }
            if (ImGui.IsItemDeactivated()) clicked = !_dragging.Remove(key);

            if (hovered) ImGui.SetMouseCursor(held ? ImGuiMouseCursor.ResizeAll : ImGuiMouseCursor.Hand);

            var col = held ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button;
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(min, max, ImGui.GetColorU32(col), style.FrameRounding);
            dl.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), style.FrameRounding);
            dl.AddText(min + (max - min - ImGui.CalcTextSize(label)) * 0.5f, ImGui.GetColorU32(ImGuiCol.Text), label);
        }
        ImGui.End();
        ImGui.PopStyleVar();

        var vp = ImGui.GetMainViewport();
        pos.X = Math.Clamp(pos.X, vp.Pos.X, Math.Max(vp.Pos.X, vp.Pos.X + vp.Size.X - size.X));
        pos.Y = Math.Clamp(pos.Y, vp.Pos.Y, Math.Max(vp.Pos.Y, vp.Pos.Y + vp.Size.Y - size.Y));
        return clicked;
    }
}
