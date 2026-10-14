using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Layout
{
    static readonly Dictionary<uint, float> TrailWidths = [];
    static readonly Dictionary<uint, int> SideTab = [];

    public static bool TabBar(string id, params ReadOnlySpan<(string label, Func<bool> body)> tabs)
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

    public static void TabBar(string id, params ReadOnlySpan<(string label, Action body)> tabs)
    {
        if (!ImGui.BeginTabBar("##" + id)) return;
        try
        {
            foreach (var (label, body) in tabs)
            {
                if (!ImGui.BeginTabItem(label)) continue;
                try { body(); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
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
            var trail = BeginTrailing("count");
            ImGui.TextDisabled(count.ToString());
            EndTrailing(trail);
            Controls.Filter(ref filter);
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

    internal static int PickTab((string Label, Func<bool>? Body)[]? tabs, int want)
    {
        if (tabs is not { Length: > 0 }) return -1;
        if (want >= 0 && want < tabs.Length && tabs[want].Body is not null) return want;
        return Array.FindIndex(tabs, t => t.Body is not null);
    }

    public static bool SideTabs(string id, float leftWidth, params (string Label, Func<bool>? Body)[] tabs)
    {
        var key = ImGui.GetID(id);
        var current = PickTab(tabs, SideTab.GetValueOrDefault(key, -1));
        if (current < 0) return false;
        var changed = false;
        MasterDetail(id, leftWidth,
            () =>
            {
                for (var i = 0; i < tabs.Length; i++)
                {
                    if (tabs[i].Body is null) ImGui.SeparatorText(tabs[i].Label);
                    else if (ImGui.Selectable(tabs[i].Label + "##t" + i, i == current)) current = i;
                }
            },
            () => changed = tabs[current].Body!());
        SideTab[key] = current;
        return changed;
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

    public static uint BeginTrailing(string id, float pad = 0f)
    {
        var trailId = ImGui.GetID(id);
        var width = TrailWidths.GetValueOrDefault(trailId);
        var left = ImGui.GetCursorPosX();
        var right = left + ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(Math.Max(left, right - width - pad));
        ImGui.BeginGroup();
        return trailId;
    }

    public static void EndTrailing(uint trailId)
    {
        ImGui.EndGroup();
        TrailWidths[trailId] = ImGui.GetItemRectSize().X;
    }

    public static DimScope Dim(bool on) => new(on);

    public readonly struct DimScope : IDisposable
    {
        readonly bool _on;

        public DimScope(bool on)
        {
            _on = on;
            if (on) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.55f);
        }

        public void Dispose()
        {
            if (_on) ImGui.PopStyleVar();
        }
    }
}
