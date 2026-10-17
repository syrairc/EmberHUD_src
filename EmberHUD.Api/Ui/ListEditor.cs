using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public sealed class AddSource<T>
{
    internal Func<T>? Factory;
    internal IEnumerable<(string key, string label)> Candidates = [];
    internal Func<string, T>? Make;
    internal string Label = "";

    public static AddSource<T> Button(Func<T> factory, string label = "+ Add") => new() { Factory = factory, Label = label };

    public static AddSource<T> Combo(IEnumerable<(string key, string label)> candidates, Func<string, T> make, string label = "Add...") =>
        new() { Candidates = candidates, Make = make, Label = label };
}

public sealed class ListOpts
{
    public int PinnedHead;
    public int PinnedTail;
    public bool Reorderable = true;
    public bool ShowIndex = true;
    public float RowHeight;
    public bool CtrlHeld;
    public Action<int>? Menu;
    public Func<int, string>? FilterText;
}

public static class ListEditor
{
    const string DeletePopup = "Delete?##eidel";

    static readonly Dictionary<uint, string> _filters = [];
    static readonly Dictionary<uint, int> _pendingDelete = [];
    static readonly Dictionary<uint, string> _rowFilters = [];
    static uint _dragKey;
    static int _dragFrom = -1, _dragTo = -1;
    static uint _rowDragKey;
    static int _rowDragFrom = -1;

    public static bool Compact<T>(string id, List<T> list, Func<T, int, bool> drawRow, AddSource<T>? add = null, bool reorderable = false, bool confirmDelete = false, Func<T, string>? filterText = null)
    {
        var key = ImGui.GetID(id);
        var d = false;
        ImGui.PushID(id);
        d |= DrawAdd(key, list, add);
        var filter = DrawRowFilter(key, filterText is not null);

        var toRemove = -1;
        for (var i = 0; i < list.Count; i++)
        {
            ImGui.PushID(i);
            using (Layout.Dim(filterText is not null && !Text.Matches(filter, filterText(list[i]))))
            {
                if (reorderable)
                {
                    ImGui.Button(":::");
                    DragSourceTarget(key, i, null);
                    ImGui.SameLine();
                }
                d |= drawRow(list[i], i);
                ImGui.SameLine();
                if (ImGui.SmallButton("x")) toRemove = i;
            }
            ImGui.PopID();
        }

        d |= ApplyDelete(key, list, toRemove, confirmDelete);
        if (reorderable) d |= ApplyReorder(key, list);
        ImGui.PopID();
        return d;
    }

    public static bool ReorderableList<T>(string id, IList<T> items, ref int selected, Action<T, int> drawRow, ListOpts? opts = null)
    {
        opts ??= new ListOpts();
        var key = ImGui.GetID(id);
        var changed = false;
        var rowH = opts.RowHeight > 0 ? opts.RowHeight : ImGui.GetFrameHeight();

        ImGui.PushID(id);
        var filter = DrawRowFilter(key, opts.FilterText is not null);
        var head = opts.Reorderable ? Math.Clamp(opts.PinnedHead, 0, items.Count) : items.Count;
        var movable = Math.Max(head, items.Count - opts.PinnedTail);

        var mouse = ImGui.GetMousePos();
        var dl = ImGui.GetWindowDrawList();
        var drop = -1;
        float firstTop = 0f, lastBottom = 0f;

        for (var i = 0; i < items.Count; i++)
        {
            ImGui.PushID(i);
            using var dim = Layout.Dim(opts.FilterText is not null && !Text.Matches(filter, opts.FilterText(i)));
            var rowTop = ImGui.GetCursorPosY();

            ImGui.PushStyleColor(ImGuiCol.Header, 0u);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(ImGuiCol.Button));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Selectable("##row", selected == i, ImGuiSelectableFlags.AllowOverlap, new Vector2(0, rowH))) selected = i;
            ImGui.PopStyleColor(3);
            if (selected == i)
            {
                var top = ImGui.GetItemRectMin();
                var bottom = ImGui.GetItemRectMax();
                dl.AddRectFilled(top, bottom, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                dl.AddRectFilled(top, new Vector2(top.X + 3, bottom.Y), ImGui.GetColorU32(ImGuiCol.CheckMark));
            }

            var movableRow = i >= head && i < movable;
            if (ImGui.IsItemActivated() && movableRow)
            {
                _rowDragKey = key;
                _rowDragFrom = i;
                selected = i;
            }
            var rmin = ImGui.GetItemRectMin();
            var rmax = ImGui.GetItemRectMax();
            if (_rowDragKey == key && movableRow)
            {
                if (i == head) firstTop = rmin.Y;
                lastBottom = rmax.Y;
                if (mouse.Y >= rmin.Y && mouse.Y < rmax.Y) drop = i;
                if (i == _rowDragFrom) dl.AddRect(rmin, rmax, ImGui.GetColorU32(ImGuiCol.DragDropTarget));
            }

            if (opts.Menu is not null)
            {
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) selected = i;
                var was = items.Count;
                if (ImGui.BeginPopupContextItem("##ctx"))
                {
                    opts.Menu(i);
                    ImGui.EndPopup();
                }
                if (items.Count != was)
                {
                    ImGui.PopID();
                    if (selected >= items.Count) selected = items.Count - 1;
                    changed = true;
                    break;
                }
            }

            ImGui.SameLine(6f);
            ImGui.SetCursorPosY(rowTop + (rowH - ImGui.GetTextLineHeight()) * 0.5f);
            if (opts.ShowIndex)
            {
                ImGui.TextDisabled(movableRow ? (i + 1).ToString("00") : "--");
                ImGui.SameLine();
            }
            drawRow(items[i], i);
            ImGui.PopID();
        }

        if (_rowDragKey == key)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (drop < 0 && movable > head) drop = mouse.Y < firstTop ? head : mouse.Y > lastBottom ? movable - 1 : -1;
                if (drop >= 0 && drop != _rowDragFrom && _rowDragFrom >= head && _rowDragFrom < movable)
                {
                    Move(items, _rowDragFrom, drop);
                    _rowDragFrom = drop;
                    selected = drop;
                    changed = true;
                }
            }
            else
            {
                _rowDragKey = 0;
                _rowDragFrom = -1;
            }
        }

        if (opts.CtrlHeld && selected >= 0)
        {
            var dir = ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? -1 : ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? 1 : 0;
            var target = SwapTarget(selected, dir, items.Count, opts.PinnedTail, opts.PinnedHead);
            if (target >= 0)
            {
                (items[selected], items[target]) = (items[target], items[selected]);
                selected = target;
                changed = true;
            }
        }

        ImGui.PopID();
        return changed;
    }

    internal static int SwapTarget(int from, int dir, int count, int pinnedTail, int pinnedHead = 0)
    {
        if (dir == 0 || from < 0 || from >= count) return -1;
        var head = Math.Clamp(pinnedHead, 0, count);
        var movable = Math.Max(head, count - pinnedTail);
        if (from < head || from >= movable) return -1;
        var to = from + dir;
        return to < head || to >= movable ? -1 : to;
    }

    public static void Move<T>(IList<T> list, int from, int to)
    {
        if (from < 0 || to < 0 || from >= list.Count || to >= list.Count || from == to) return;
        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(to, moved);
    }

    internal static string DrawRowFilter(uint key, bool enabled)
    {
        if (!enabled) return "";
        var filter = _rowFilters.GetValueOrDefault(key, "");
        Controls.Filter(ref filter);
        _rowFilters[key] = filter;
        return filter;
    }

    internal static bool DrawAdd<T>(uint key, List<T> list, AddSource<T>? add)
    {
        if (add is null) return false;
        if (add.Factory is { } factory)
        {
            if (!ImGui.Button(add.Label)) return false;
            list.Add(factory());
            return true;
        }
        var filter = _filters.GetValueOrDefault(key, "");
        var picked = Combo.SearchCombo("add", add.Label, add.Candidates, ref filter, out var pick);
        _filters[key] = filter;
        if (!picked || add.Make is null) return false;
        list.Add(add.Make(pick!));
        return true;
    }

    internal static void DragSourceTarget(uint key, int index, string? label)
    {
        if (ImGui.BeginDragDropSource())
        {
            _dragKey = key;
            _dragFrom = index;
            unsafe { ImGui.SetDragDropPayload("EILIST", null, 0); }
            if (label is not null) ImGui.TextUnformatted(label);
            ImGui.EndDragDropSource();
        }
        if (ImGui.BeginDragDropTarget())
        {
            if (_dragKey == key) _dragTo = index;
            ImGui.EndDragDropTarget();
        }
    }

    internal static bool ApplyDelete<T>(uint key, List<T> list, int requested, bool confirm)
    {
        if (!confirm)
        {
            if (requested < 0 || requested >= list.Count) return false;
            list.RemoveAt(requested);
            return true;
        }

        if (requested >= 0)
        {
            _pendingDelete[key] = requested;
            ImGui.OpenPopup(DeletePopup);
        }
        if (!Layout.ConfirmModal(DeletePopup, "Delete this item?", out var ok)) return false;
        var removed = false;
        if (ok && _pendingDelete.TryGetValue(key, out var at) && at >= 0 && at < list.Count)
        {
            list.RemoveAt(at);
            removed = true;
        }
        _pendingDelete.Remove(key);
        return removed;
    }

    internal static bool ApplyReorder<T>(uint key, List<T> list)
    {
        if (_dragKey != key) return false;
        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _dragTo = -1;
            return false;
        }
        var moved = false;
        if (_dragFrom >= 0 && _dragTo >= 0 && _dragFrom != _dragTo && _dragFrom < list.Count && _dragTo < list.Count)
        {
            Move(list, _dragFrom, _dragTo);
            moved = true;
        }
        _dragFrom = _dragTo = -1;
        _dragKey = 0;
        return moved;
    }
}
