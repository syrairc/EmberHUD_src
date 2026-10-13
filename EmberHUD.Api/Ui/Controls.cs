using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static partial class Controls
{
    static uint _capturing;

    public static bool Hotkey(string label, ref Hotkey value, string? id = null)
    {
        var key = ImGui.GetID(id ?? label);
        var changed = false;
        if (_capturing == key)
        {
            if (Input.TryCapture(out var captured))
            {
                value = captured;
                changed = true;
                _capturing = 0;
            }
            else if (!Input.Capturing) _capturing = 0;
        }
        var text = _capturing == key ? "press a key... (Esc cancels)" : value.ToString();
        ImGui.PushItemFlag(ImGuiItemFlags.NoNav, true);
        if (ImGui.Button($"{text}###{id ?? label}", new Vector2(ImGui.CalcItemWidth(), 0)) && !Input.Capturing)
        {
            _capturing = key;
            Input.BeginCapture();
        }
        ImGui.PopItemFlag();
        if (label.Length > 0)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.TextUnformatted(label);
        }
        return changed;
    }

    public static void LogView(string id, IReadOnlyList<string> lines, float height = 200)
    {
        if (ImGui.BeginChild(id, new Vector2(0, height), ImGuiChildFlags.Borders))
        {
            ImGui.PushTextWrapPos(0);
            foreach (var line in lines) ImGui.TextUnformatted(line);
            ImGui.PopTextWrapPos();
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1f);
        }
        ImGui.EndChild();
    }
}
