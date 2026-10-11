using Hexa.NET.ImGui;

namespace Aember.Ui;

public static class Layout
{
    static readonly Dictionary<uint, float> TrailWidths = [];
    static uint _trailId;

    public static void BeginTrailing(string id, float pad = 0f)
    {
        _trailId = ImGui.GetID(id);
        var width = TrailWidths.GetValueOrDefault(_trailId);
        var left = ImGui.GetCursorPosX();
        var right = left + ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(Math.Max(left, right - width - pad));
        ImGui.BeginGroup();
    }

    public static void EndTrailing()
    {
        ImGui.EndGroup();
        TrailWidths[_trailId] = ImGui.GetItemRectSize().X;
    }
}
