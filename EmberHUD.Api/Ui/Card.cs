using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public enum CardLook { Outline, Filled, Subtle }

public static class Card
{
    internal const float BodyIndent = 8f;

    static readonly Stack<bool> _open = new();

    static float Luma(Vector4 c) => c.X * 0.299f + c.Y * 0.587f + c.Z * 0.114f;

    static Vector4 Shift(Vector4 c, float d) => new(Math.Clamp(c.X + d, 0f, 1f), Math.Clamp(c.Y + d, 0f, 1f), Math.Clamp(c.Z + d, 0f, 1f), c.W);

    public static Vector4 SurfaceFill(Vector4 window, Vector4 frame, float step = 0.055f, float margin = 0.05f)
    {
        var dir = Luma(window) > 0.5f ? -1f : 1f;
        var c = Shift(window, dir * step);
        var gap = (Luma(c) - Luma(frame)) * dir;
        return gap >= margin ? c : Shift(c, dir * (margin - gap));
    }

    public readonly struct CardScope : IDisposable
    {
        readonly int _colors;

        internal CardScope(int colors) => _colors = colors;

        public void Dispose()
        {
            ImGui.EndChild();
            ImGui.PopStyleColor(_colors);
            ImGui.PopStyleVar(2);
            ImGui.PopID();
        }
    }

    public static CardScope Surface(string id, CardLook look = CardLook.Filled, float width = 0f, float padding = 10f)
    {
        ImGui.PushID(id);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        var filled = look == CardLook.Filled;
        if (filled)
        {
            var c = ImGui.GetStyle().Colors;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, SurfaceFill(c[(int)ImGuiCol.WindowBg], c[(int)ImGuiCol.FrameBg]));
        }
        var flags = look == CardLook.Subtle ? ImGuiChildFlags.AlwaysUseWindowPadding : ImGuiChildFlags.Borders;
        ImGui.BeginChild("##surface", new Vector2(width, 0f), flags | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysAutoResize);
        return new CardScope(filled ? 1 : 0);
    }

    public static void Header(string? title, string? description = null, Action? action = null)
    {
        ImGui.TextUnformatted(title ?? "");
        if (action is not null)
        {
            var headTrail = Layout.BeginTrailing("##cardhead");
            action();
            Layout.EndTrailing(headTrail);
        }
        if (string.IsNullOrEmpty(description)) return;
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(description);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
    }

    public static void Footer(Action? body)
    {
        if (body is null) return;
        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, 2f));
        body();
    }

    public static bool List<T>(string id, List<T> list, Func<T, string> label, Func<T, int, bool> drawBody, AddSource<T>? add = null, bool reorderable = false, bool confirmDelete = false, Func<T, string>? filterText = null)
    {
        var key = ImGui.GetID(id);
        var d = false;
        ImGui.PushID(id);
        d |= ListEditor.DrawAdd(key, list, add);
        var filter = ListEditor.DrawRowFilter(key, filterText is not null);

        var toRemove = -1;
        for (var i = 0; i < list.Count; i++)
        {
            ImGui.PushID(i);
            using (Layout.Dim(filterText is not null && !Text.Matches(filter, filterText(list[i]))))
            {
                var keep = true;
                var open = ImGui.CollapsingHeader(label(list[i]) + "###h", ref keep, ImGuiTreeNodeFlags.None);
                if (reorderable) ListEditor.DragSourceTarget(key, i, label(list[i]));
                if (open)
                {
                    ImGui.Indent();
                    d |= drawBody(list[i], i);
                    ImGui.Unindent();
                }
                if (!keep) toRemove = i;
            }
            ImGui.PopID();
        }

        d |= ListEditor.ApplyDelete(key, list, toRemove, confirmDelete);
        if (reorderable) d |= ListEditor.ApplyReorder(key, list);
        ImGui.PopID();
        return d;
    }

    public static bool BeginCard(string id, string title, Action? trailing = null, Action? menu = null, bool? setOpen = null, bool defaultOpen = false)
    {
        ImGui.PushID(id);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.BeginChild("##card", Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysAutoResize);

        var flags = ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.AllowOverlap | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (defaultOpen) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (setOpen.HasValue) ImGui.SetNextItemOpen(setOpen.Value);

        var open = ImGui.TreeNodeEx(title + "###h", flags);
        if (menu is not null && ImGui.BeginPopupContextItem("##ctx"))
        {
            menu();
            ImGui.EndPopup();
        }
        if (trailing is not null)
        {
            var trail = Layout.BeginTrailing("##trail", 6f);
            trailing();
            Layout.EndTrailing(trail);
        }

        _open.Push(open);
        if (open)
        {
            ImGui.Indent(BodyIndent);
            ImGui.Dummy(new Vector2(0, 2f));
        }
        return open;
    }

    public static void EndCard()
    {
        if (_open.Count > 0 && _open.Pop())
        {
            ImGui.Dummy(new Vector2(0, 4f));
            ImGui.Unindent(BodyIndent);
        }
        ImGui.EndChild();
        ImGui.PopStyleVar(1);
        ImGui.PopID();
    }
}
