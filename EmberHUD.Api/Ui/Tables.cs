using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Tables
{
    public static bool Begin(string id, (string name, float width)[] cols, bool showHeader = true, ImGuiTableFlags flags = ImGuiTableFlags.RowBg)
    {
        if (!ImGui.BeginTable(id, cols.Length, flags)) return false;
        foreach (var (name, width) in cols) ImGui.TableSetupColumn(name, width > 0 ? ImGuiTableColumnFlags.WidthFixed : ImGuiTableColumnFlags.WidthStretch, width > 0 ? width : 0f);
        if (showHeader) ImGui.TableHeadersRow();
        return true;
    }

    public static void End() => ImGui.EndTable();

    public static RowScope Row(int id, bool selected = false) => new(id, selected);

    public static DimScope Dim(bool on) => new(on);

    public readonly struct RowScope : IDisposable
    {
        public RowScope(int id, bool selected)
        {
            ImGui.PushID(id);
            ImGui.TableNextRow();
            if (selected) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.Header));
        }

        public void Dispose() => ImGui.PopID();
    }

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
