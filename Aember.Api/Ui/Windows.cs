using System.Numerics;
using Hexa.NET.ImGui;

namespace Aember.Ui;

public static class Windows
{
    public static bool TabBar(string id, params (string label, Func<bool> body)[] tabs)
    {
        if (!ImGui.BeginTabBar("##" + id)) return false;
        var changed = false;
        try
        {
            foreach (var (label, body) in tabs)
            {
                if (!ImGui.BeginTabItem(label)) continue;
                try { changed |= body(); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
        return changed;
    }

    public static void MasterDetail(string id, float leftWidth, Action left, Action right)
    {
        ImGui.PushID(id);
        try
        {
            ImGui.BeginChild("##l", new Vector2(leftWidth, 0), ImGuiChildFlags.Borders);
            try { left(); }
            finally { ImGui.EndChild(); }
            ImGui.SameLine();
            ImGui.BeginChild("##r", Vector2.Zero, ImGuiChildFlags.Borders);
            try { right(); }
            finally { ImGui.EndChild(); }
        }
        finally { ImGui.PopID(); }
    }

    public static bool TwoColumn(string id, float minWidth, Func<bool> left, Func<bool> right)
    {
        if (ImGui.GetContentRegionAvail().X < minWidth)
        {
            var stacked = left();
            ImGui.Separator();
            return right() || stacked;
        }
        if (!ImGui.BeginTable("##" + id, 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit)) return false;
        try
        {
            ImGui.TableSetupColumn("l", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("r", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var done = left();
            ImGui.TableNextColumn();
            return right() || done;
        }
        finally { ImGui.EndTable(); }
    }

    public static void FilterPane(string id, string title, int count, ref string filter, string hint, Action body)
    {
        ImGui.PushID(id);
        try
        {
            ImGui.TextDisabled(Text.EscPct(title));
            Layout.BeginTrailing("count");
            ImGui.TextDisabled(count.ToString());
            Layout.EndTrailing();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##filter", "Filter", ref filter, 64);
            var hintHeight = string.IsNullOrEmpty(hint) ? 0f : ImGui.CalcTextSize(hint, false, ImGui.GetContentRegionAvail().X).Y + ImGui.GetStyle().ItemSpacing.Y;
            ImGui.BeginChild("##rows", new Vector2(0, -hintHeight));
            try { body(); }
            finally { ImGui.EndChild(); }
            if (string.IsNullOrEmpty(hint)) return;
            ImGui.PushTextWrapPos(0f);
            ImGui.TextDisabled(Text.EscPct(hint));
            ImGui.PopTextWrapPos();
        }
        finally { ImGui.PopID(); }
    }

    public static bool ConfirmModal(string id, string message, out bool confirmed)
    {
        confirmed = false;
        if (!ImGui.BeginPopupModal(id, ImGuiWindowFlags.AlwaysAutoResize)) return false;
        var acted = false;
        ImGui.TextUnformatted(message);
        if (ImGui.Button("Confirm"))
        {
            confirmed = acted = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            acted = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        return acted;
    }
}
