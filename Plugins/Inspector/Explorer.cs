using System;
using System.Collections.Generic;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed class Explorer
{
    readonly Snapshots _snapshots;
    readonly Dictionary<nint, (nint PointerAt, nint Component, nint Location)> _locations = [];
    TableColumn<ObjectRow>[]? _columns;
    ObjectRow[] _rows = [];
    (ObjectIndex, nint, string) _rowsKey;
    (nint, string, ObjectIndex) _crumbKey;
    (string Name, string Label, nint Address)[] _crumb = [];
    string _filter = "";
    int _area = -1;

    public Explorer(Snapshots snapshots, Action<object> pin)
    {
        _snapshots = snapshots;
        View = new RawView(snapshots, pin) { Navigate = History.Push };
    }

    public RawView View { get; }

    public ObjectIndex Index { get; private set; } = ObjectIndex.Empty;

    public BackgroundJob<ObjectIndex> Job = new();

    public History History { get; } = new();

    public nint SelectedClass;

    public string Search = "";

    public void Draw(int areaId)
    {
        if (!RawView.Attached()) return;
        if (Job.TakeFinished() is { } done)
        {
            if (done.IsFaulted) Log.Warn($"Inspector objects read failed - {done.Exception?.GetBaseException().Message}");
            Index = done.IsCompletedSuccessfully ? done.Result : ObjectIndex.Empty;
            _locations.Clear();
        }
        var refresh = ImGui.Button("Refresh");
        if (!Job.Running && (refresh || areaId != _area))
        {
            _area = areaId;
            Job.Start(ObjectIndex.Scan);
        }
        ImGui.SameLine();
        ImGui.TextDisabled(Job.Running ? "reading..." : $"{Index.Rows.Length} objects");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(260f);
        ImGui.InputTextWithHint("##search", "Name, class or 0x address...", ref Search, 128);
        if (ImGui.IsItemDeactivatedAfterEdit() && Search.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase) && Index.Search(Search) is { Length: 1 } hit) History.Push(hit[0].Address);

        var height = Math.Max(240f, ImGui.GetContentRegionAvail().Y - 4f);
        if (!ImGui.BeginTable("explorer", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings)) return;
        ImGui.TableSetupColumn("Classes", ImGuiTableColumnFlags.WidthStretch, 0.22f);
        ImGui.TableSetupColumn("Instances", ImGuiTableColumnFlags.WidthStretch, 0.34f);
        ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthStretch, 0.44f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        Pane("classes", height, Classes);
        ImGui.TableNextColumn();
        Pane("instances", height, () => Instances(height));
        ImGui.TableNextColumn();
        Pane("object", height, ObjectPane);
        ImGui.EndTable();
    }

    static void Pane(string id, float height, Action draw)
    {
        if (ImGui.BeginChild(id, new Vector2(-1f, height)))
            draw();
        ImGui.EndChild();
    }

    void Classes()
    {
        foreach (var root in Index.Roots) ClassRow(root);
    }

    void ClassRow(nint classAt)
    {
        if (Index.Node(classAt) is not { } node) return;
        var kids = Index.Children(classAt);
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (kids.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (classAt == SelectedClass) flags |= ImGuiTreeNodeFlags.Selected;
        var open = ImGui.TreeNodeEx(node.Label, flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            SelectedClass = classAt;
            Search = "";
        }
        if (!open || kids.Count == 0) return;
        foreach (var kid in kids) ClassRow(kid);
        ImGui.TreePop();
    }

    void Instances(float height)
    {
        var key = (Index, Search.Length > 0 ? 0 : SelectedClass, Search);
        if (key != _rowsKey)
        {
            _rowsKey = key;
            _rows = Search.Length > 0 ? Index.Search(Search) : SelectedClass != 0 ? [.. Index.Instances(SelectedClass)] : [];
        }
        if (_rows.Length == 0 && Search.Length == 0 && SelectedClass == 0)
        {
            ImGui.TextDisabled("pick a class, or search");
            return;
        }
        SortableTable.Draw("instances", _rows, _columns ??= Columns(), ref _filter, null, Math.Max(120f, height - 8f));
    }

    TableColumn<ObjectRow>[] Columns() =>
    [
        new() { Header = "#", Width = 60f, SortKey = r => r.Index, Draw = (row, _) => SelectCell(row) },
        new() { Header = "Name", SortKey = r => r.Name, Draw = (row, _) => ImGui.TextUnformatted(row.Name) },
        new() { Header = "Class", SortKey = r => r.Class, Draw = (row, _) => ImGui.TextUnformatted(row.Class) },
        new() { Header = "Dist", Width = 60f, Draw = (row, _) => Distance(row) },
        new() { Header = "Address", Width = 130f, Draw = (row, _) => ImGui.TextUnformatted($"0x{row.Address:X}") },
    ];

    void SelectCell(ObjectRow row)
    {
        if (ImGui.Selectable($"{row.Index}###row{row.Address}", row.Address == History.Current, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) History.Push(row.Address);
        if (!ImGui.BeginPopupContextItem("row")) return;
        View.AddressMenu(row.Address, $"{row.Class} {row.Name}");
        ImGui.EndPopup();
    }

    void Distance(ObjectRow row)
    {
        if (Game.State.Player is { } player && ActorPosition(row) is { } at) ImGui.TextUnformatted($"{player.FlatDistance(at):0}");
        else ImGui.TextDisabled("-");
    }

    internal Vector3? ActorPosition(ObjectRow row)
    {
        if (!_locations.TryGetValue(row.Address, out var known))
        {
            if (Ue.Struct(row.ClassAddress)?.Property("RootComponent") is not { } root) return null;
            known = (row.Address + root.Offset, 0, 0);
        }
        var component = _snapshots.Get(known.PointerAt, 8) is { } pointer ? (nint)BitConverter.ToInt64(pointer, 0) : 0;
        if (component != known.Component || known.Location == 0)
            known = (known.PointerAt, component, component != 0 && Ue.TryObject(component, out var comp) && comp.Class?.Property("RelativeLocation") is { } location ? component + location.Offset : 0);
        _locations[row.Address] = known;
        return known.Location != 0 && _snapshots.Get(known.Location, 12) is { } xyz ? new Vector3(BitConverter.ToSingle(xyz, 0), BitConverter.ToSingle(xyz, 4), BitConverter.ToSingle(xyz, 8)) : null;
    }

    void ObjectPane()
    {
        if (ImGui.Button("<")) History.Back();
        ImGui.SameLine();
        if (ImGui.Button(">")) History.Forward();
        ImGui.SameLine();
        View.Toolbar();
        var address = History.Current;
        if (address == 0)
        {
            ImGui.TextDisabled("select an object");
            return;
        }
        if (!Ue.TryObject(address, out var obj) || obj.Class is not { } cls)
        {
            ImGui.TextDisabled($"gone 0x{address:X}");
            return;
        }
        DrawCrumb(Crumb(obj));
        ImGui.Separator();
        View.Object(address, obj, cls, 0, true);
    }

    internal (string Name, string Label, nint Address)[] Crumb(UObj obj)
    {
        var key = (obj.Address, obj.Name, Index);
        if (key == _crumbKey) return _crumb;
        List<(string Name, string Label, nint Address)> trail = [];
        var current = obj;
        for (var depth = 0; depth < 8; depth++)
        {
            trail.Add((current.Name, $"{current.Name}###crumb{depth}", current.Address));
            if (current.Outer is not { } outer || !Ue.TryObject(outer.Address, out _)) break;
            current = outer;
        }
        _crumbKey = key;
        return _crumb = [.. trail];
    }

    void DrawCrumb((string Name, string Label, nint Address)[] trail)
    {
        for (var i = 0; i < trail.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("<");
                ImGui.SameLine();
            }
            if (i == 0) ImGui.TextUnformatted(trail[i].Name);
            else if (ImGui.SmallButton(trail[i].Label)) History.Push(trail[i].Address);
        }
    }
}

internal sealed class History
{
    const int Cap = 200;

    readonly List<nint> _items = [];
    int _at = -1;

    public nint Current => _at >= 0 ? _items[_at] : 0;

    public bool CanBack => _at > 0;

    public bool CanForward => _at < _items.Count - 1;

    public void Push(nint address)
    {
        if (address == Current) return;
        _items.RemoveRange(_at + 1, _items.Count - _at - 1);
        _items.Add(address);
        _at++;
        if (_items.Count <= Cap) return;
        _items.RemoveAt(0);
        _at--;
    }

    public void Back()
    {
        if (CanBack) _at--;
    }

    public void Forward()
    {
        if (CanForward) _at++;
    }
}
