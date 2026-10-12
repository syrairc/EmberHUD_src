using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record ObjectRow(int Index, string Name, string Class, string Outer, nint Address);

internal sealed record RawPin(nint Address, string Label);

internal sealed class RawTree(Snapshots snapshots)
{
    const int MaxDepth = 8;
    const int MaxBlock = 0x4000;
    const int HexDefault = 0x200;
    const int HexStep = 0x100;
    const int MaxElements = 200;
    const int OpenAllDepth = 2;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    readonly Dictionary<uint, int> _hexLength = [];
    readonly HashSet<nint> _path = [];
    TableColumn<ObjectRow>[]? _columns;
    int _objectsArea = -1;
    string _objectsFilter = "";
    string _address = "";
    nint _goto;

    public bool OpenAll;
    public int RowsDrawn;
    public object? PinRequest;
    public BackgroundJob<ObjectRow[]> ObjectsJob = new();
    public ObjectRow[] ObjectRows { get; internal set; } = [];

    public void Node(string id, nint address, int depth)
    {
        ImGui.PushID(id);
        if (!Memory.Attached) ImGui.TextDisabled("not attached (--fake or no game)");
        else if (depth > MaxDepth) ImGui.TextDisabled("...");
        else if (!_path.Add(address)) ImGui.TextDisabled($"cycle 0x{address:X}");
        else
        {
            if (Ue.TryObject(address, out var obj) && obj.Class is { } cls)
            {
                Header($"{cls.Name}  {obj.Name}", address);
                Fields(address, cls, depth);
                if (OpenAll && depth < OpenAllDepth) ImGui.SetNextItemOpen(true);
                if (ImGui.TreeNode("Raw bytes"))
                {
                    Hex(address, Math.Clamp(cls.Size, 8, 0x1000), depth);
                    ImGui.TreePop();
                }
            }
            else
            {
                Header($"0x{address:X}", address);
                var key = ImGui.GetID("length");
                var length = _hexLength.GetValueOrDefault(key, HexDefault);
                var before = length;
                if (ImGui.SmallButton("+")) length = Math.Min(MaxBlock, length + HexStep);
                ImGui.SameLine();
                if (ImGui.SmallButton("-")) length = Math.Max(HexStep, length - HexStep);
                if (length != before) _hexLength[key] = length;
                Hex(address, length, depth);
            }
            _path.Remove(address);
        }
        ImGui.PopID();
    }

    public void Globals()
    {
        if (!Attached()) return;
        foreach (var (name, address) in Memory.Globals.OrderBy(pair => pair.Key))
        {
            if (OpenAll) ImGui.SetNextItemOpen(true);
            if (!ImGui.TreeNode($"{name}  0x{address:X}###{name}")) continue;
            Node(name, address, 1);
            ImGui.TreePop();
        }
    }

    public void Objects(int areaId)
    {
        if (!Attached()) return;
        if (ObjectsJob.TakeFinished() is { } done)
        {
            if (done.IsFaulted) Log.Warn($"Inspector objects read failed - {done.Exception?.GetBaseException().Message}");
            ObjectRows = done.IsCompletedSuccessfully ? done.Result : [];
        }
        RowsDrawn = 0;
        var refresh = ImGui.Button("Refresh");
        if (!ObjectsJob.Running && (refresh || areaId != _objectsArea))
        {
            _objectsArea = areaId;
            ObjectsJob.Start(() => [.. Ue.Objects().Select(o => new ObjectRow(o.Index, o.Name, o.Class?.Name ?? "?", o.Outer?.Name ?? "", o.Address))]);
        }
        ImGui.SameLine();
        ImGui.TextDisabled(ObjectsJob.Running ? "reading..." : $"{ObjectRows.Length} objects");
        SortableTable.Draw("objects", ObjectRows, _columns ??= ObjectColumns(), ref _objectsFilter, row => _objectsFilter.Length == 0 ? "" : $"{row.Name} {row.Class} {row.Outer}", Math.Max(120f, ImGui.GetContentRegionAvail().Y));
    }

    public void Address()
    {
        if (!Attached()) return;
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##address", "0x...", ref _address, 32);
        ImGui.SameLine();
        if (ImGui.Button("Go") && long.TryParse(_address.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, Invariant, out var parsed)) _goto = (nint)parsed;
        if (_goto != 0) Node("goto", _goto, 0);
    }

    static bool Attached()
    {
        if (Memory.Attached) return true;
        ImGui.TextDisabled("not attached (--fake or no game)");
        return false;
    }

    TableColumn<ObjectRow>[] ObjectColumns() =>
    [
        new() { Header = "#", Width = 64f, SortKey = r => r.Index, Draw = (row, _) => PinCell(row) },
        new() { Header = "Name", SortKey = r => r.Name, Draw = (row, _) => ImGui.TextUnformatted(row.Name) },
        new() { Header = "Class", SortKey = r => r.Class, Draw = (row, _) => ImGui.TextUnformatted(row.Class) },
        new() { Header = "Outer", SortKey = r => r.Outer, Draw = (row, _) => ImGui.TextUnformatted(row.Outer) },
        new() { Header = "Address", Width = 140f, Draw = (row, _) => ImGui.TextUnformatted($"0x{row.Address:X}") },
    ];

    void PinCell(ObjectRow row)
    {
        RowsDrawn++;
        if (ImGui.Selectable($"{row.Index}###pin{row.Address}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) PinRequest = new RawPin(row.Address, $"{row.Class} {row.Name}");
        if (!ImGui.BeginPopupContextItem("row")) return;
        if (ImGui.MenuItem("Copy address")) ImGui.SetClipboardText($"0x{row.Address:X}");
        ImGui.EndPopup();
    }

    void Header(string text, nint address)
    {
        ImGui.TextUnformatted(text);
        ImGui.SameLine();
        ImGui.TextDisabled($"@0x{address:X}");
        if (!ImGui.BeginPopupContextItem("header")) return;
        if (ImGui.MenuItem("Copy address")) ImGui.SetClipboardText($"0x{address:X}");
        if (ImGui.MenuItem("Pin")) PinRequest = new RawPin(address, text);
        ImGui.EndPopup();
    }

    void Fields(nint container, UStructInfo info, int depth)
    {
        var bytes = snapshots.Get(container, Math.Clamp(info.Size, 1, MaxBlock));
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        foreach (var prop in info.Properties)
        {
            ImGui.PushID($"{prop.Offset:X}{prop.Name}");
            var value = Ue.Format(bytes, prop);
            var pointer = Pointer(bytes, prop.Offset);
            var structInfo = prop.Type == "StructProperty" ? Ue.Struct(prop.Struct) : null;
            var expandable = prop.Type is "ObjectProperty" or "ClassProperty" ? pointer != 0 : structInfo is not null || prop is { Type: "ArrayProperty", Element: not null };
            if (Line($"+{prop.Offset:X}", expandable, prop.Name, prop.Type, value, pointer, depth))
            {
                if (structInfo is not null) Fields(container + prop.Offset, structInfo, depth + 1);
                else if (prop.Element is { } element) Elements(pointer, Count(bytes, prop.Offset), element, depth + 1);
                else Node("target", pointer, depth + 1);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }

    void Elements(nint data, int count, UProp element, int depth)
    {
        if (depth > MaxDepth || count <= 0 || element.Size <= 0)
        {
            ImGui.TextDisabled(count <= 0 ? "empty" : "...");
            return;
        }
        var shown = Math.Min(count, MaxElements);
        var bytes = element.Size > MaxBlock ? null : snapshots.Get(data, (int)Math.Min((long)shown * element.Size, MaxBlock));
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        var structInfo = element.Type == "StructProperty" ? Ue.Struct(element.Struct) : null;
        for (var i = 0; i < shown && (i + 1) * element.Size <= bytes.Length; i++)
        {
            ImGui.PushID(i);
            var slice = bytes.AsSpan(i * element.Size, element.Size);
            var pointer = Pointer(bytes, i * element.Size);
            var expandable = element.Type is "ObjectProperty" or "ClassProperty" ? pointer != 0 : structInfo is not null;
            if (Line($"[{i}]", expandable, "", element.Type, Ue.Format(slice, element with { Offset = 0 }), pointer, depth))
            {
                if (structInfo is not null) Fields(data + i * element.Size, structInfo, depth + 1);
                else Node("target", pointer, depth + 1);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if (count > shown) ImGui.TextDisabled($"{count - shown} more");
    }

    void Hex(nint address, int length, int depth)
    {
        var bytes = snapshots.Get(address, length);
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        for (var offset = 0; offset + 8 <= bytes.Length; offset += 8)
        {
            ImGui.PushID(offset);
            var row = bytes.AsSpan(offset, 8);
            var pointer = (nint)BitConverter.ToInt64(row);
            var target = PointerLabel(pointer);
            var text = $"{BitConverter.ToString(bytes, offset, 8).Replace('-', ' ')}   {BitConverter.ToInt32(row)} {BitConverter.ToInt32(row[4..])}   {BitConverter.ToSingle(row).ToString("0.###", Invariant)} {BitConverter.ToSingle(row[4..]).ToString("0.###", Invariant)}   {BitConverter.ToDouble(row).ToString("G6", Invariant)}";
            if (Line($"+{offset:X}", target is not null, target ?? "", "", text, target is null ? 0 : pointer, depth))
            {
                Node("target", pointer, depth + 1);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }

    // ponytail: PointerLabel and the object-property values call Ue.TryObject live every frame instead of going through Snapshots - a few hundred reads per open hex block, cache labels per refresh if it shows up
    string? PointerLabel(nint pointer)
    {
        if (pointer < 0x10000 || pointer > 0x7FFF_FFFF_0000) return null;
        if (Ue.TryObject(pointer, out var obj)) return $"-> {obj.Class?.Name ?? "?"} {obj.Name}";
        return snapshots.Get(pointer, 8) is null ? null : "->";
    }

    bool Line(string offset, bool expandable, string name, string type, string value, nint pointer, int depth)
    {
        var x = ImGui.GetCursorPosX();
        if (expandable && OpenAll && depth < OpenAllDepth) ImGui.SetNextItemOpen(true);
        var open = ImGui.TreeNodeEx($"{offset}###node", expandable ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        if (ImGui.BeginPopupContextItem("line"))
        {
            if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(value);
            if (ImGui.MenuItem("Copy row")) ImGui.SetClipboardText($"{offset} {name} {type} {value}");
            if (pointer != 0 && ImGui.MenuItem("Pin")) PinRequest = new RawPin(pointer, name.Length > 0 ? name : $"0x{pointer:X}");
            ImGui.EndPopup();
        }
        ImGui.SameLine(x + 90f);
        ImGui.TextUnformatted(name);
        ImGui.SameLine(x + 330f);
        ImGui.TextDisabled(Text.EscPct(type));
        ImGui.SameLine(x + 480f);
        ImGui.TextUnformatted(value);
        return open && expandable;
    }

    static nint Pointer(byte[] bytes, int offset) => offset >= 0 && offset <= bytes.Length - 8 ? (nint)BitConverter.ToInt64(bytes, offset) : 0;

    // ponytail: TArray Num at +8 is the engine-wide shape and the same TArrayNum the layout carries, expose Ue.ArrayCount(bytes, prop) if it ever differs
    static int Count(byte[] bytes, int offset) => offset >= 0 && offset <= bytes.Length - 12 ? BitConverter.ToInt32(bytes, offset + 8) : 0;
}
