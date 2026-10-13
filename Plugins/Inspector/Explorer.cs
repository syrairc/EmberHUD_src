using System;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed class Explorer
{
    readonly RawTree _raw;
    readonly Snapshots _snapshots;
    TableColumn<ObjectRow>[]? _columns;
    ObjectRow[] _rows = [];
    (ObjectIndex, nint, string) _rowsKey;
    string _filter = "";
    int _area = -1;

    public Explorer(RawTree raw, Snapshots snapshots)
    {
        _raw = raw;
        _snapshots = snapshots;
        raw.View.Navigate = History.Push;
    }

    public ObjectIndex Index { get; private set; } = ObjectIndex.Empty;

    public BackgroundJob<ObjectIndex> Job = new();

    public History History { get; } = new();

    public nint SelectedClass;

    public string Search = "";

    public int RowsDrawn;

    internal void Seed(ObjectIndex index, int areaId)
    {
        Index = index;
        _area = areaId;
    }

    public void Draw(int areaId)
    {
        if (!Memory.Attached)
        {
            ImGui.TextDisabled("not attached (--fake or no game)");
            return;
        }
        if (Job.TakeFinished() is { } done)
        {
            if (done.IsFaulted) Log.Warn($"Inspector objects read failed - {done.Exception?.GetBaseException().Message}");
            Index = done.IsCompletedSuccessfully ? done.Result : ObjectIndex.Empty;
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
        var open = ImGui.TreeNodeEx($"{node.Name}  {node.Total}###class{classAt}", flags);
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
        RowsDrawn = 0;
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
        RowsDrawn++;
        if (ImGui.Selectable($"{row.Index}###row{row.Address}", row.Address == History.Current, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) History.Push(row.Address);
        if (!ImGui.BeginPopupContextItem("row")) return;
        if (ImGui.MenuItem("Pin")) _raw.PinRequest = new RawPin(row.Address, $"{row.Class} {row.Name}");
        if (ImGui.MenuItem("Copy address")) ImGui.SetClipboardText($"0x{row.Address:X}");
        ImGui.EndPopup();
    }

    void Distance(ObjectRow row)
    {
        if (Game.State.Player is { } player && ActorSpot(row) is { } at) ImGui.TextUnformatted($"{Vector2.Distance(new Vector2(at.X, at.Y), new Vector2(player.Position.X, player.Position.Y)):0}");
        else ImGui.TextDisabled("-");
    }

    Vector3? ActorSpot(ObjectRow row)
    {
        if (Ue.Struct(row.ClassAddress)?.Property("RootComponent") is not { } root || _snapshots.Get(row.Address + root.Offset, 8) is not { } pointer) return null;
        var component = (nint)BitConverter.ToInt64(pointer);
        if (component == 0 || !Ue.TryObject(component, out var comp) || comp.Class?.Property("RelativeLocation") is not { } location) return null;
        return _snapshots.Get(component + location.Offset, 12) is { } xyz ? new Vector3(BitConverter.ToSingle(xyz, 0), BitConverter.ToSingle(xyz, 4), BitConverter.ToSingle(xyz, 8)) : null;
    }

    void ObjectPane()
    {
        if (ImGui.Button("<") && History.CanBack) History.Back();
        ImGui.SameLine();
        if (ImGui.Button(">") && History.CanForward) History.Forward();
        ImGui.SameLine();
        _raw.View.Toolbar();
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
        Breadcrumb(obj);
        ImGui.Separator();
        _raw.View.Object(address, obj, cls, 0, true);
    }

    void Breadcrumb(UObj obj)
    {
        var current = obj;
        for (var depth = 0; depth < 8; depth++)
        {
            if (depth > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("<");
                ImGui.SameLine();
            }
            if (depth == 0) ImGui.TextUnformatted(current.Name);
            else if (ImGui.SmallButton($"{current.Name}###crumb{depth}")) History.Push(current.Address);
            if (current.Outer is not { } outer || !Ue.TryObject(outer.Address, out _)) break;
            current = outer;
        }
    }
}
