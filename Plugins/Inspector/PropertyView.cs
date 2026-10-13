using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed class PropertyView(Snapshots snapshots, RawTree raw)
{
    const int MaxDepth = 8;
    const int MaxBlock = 0x4000;
    const int MaxElements = 200;
    const int OpenAllDepth = 2;
    static readonly HashSet<string> EngineBases = ["Object", "Actor", "Pawn", "Controller", "Character", "ActorComponent", "SceneComponent", "PrimitiveComponent"];

    public bool HideDefaults;
    public string Search = "";
    public bool OpenAll;
    public Action<nint>? Navigate;

    public void Toolbar()
    {
        ImGui.Checkbox("Hide defaults", ref HideDefaults);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##inobject", "Search in object...", ref Search, 64);
    }

    public void Object(nint address, UObj obj, UStructInfo cls, int depth, bool link)
    {
        Header($"{cls.Name}  {obj.Name}", address);
        var size = Math.Clamp(cls.Size, 1, MaxBlock);
        var bytes = snapshots.Get(address, size);
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        byte[]? defaults = null;
        if (HideDefaults && Ue.DefaultObject(cls) is var cdo && cdo != 0) defaults = snapshots.Get(cdo, size);
        var searching = Search.Length > 0;
        var first = true;
        foreach (var (owner, rows) in PropertyRows.Group(cls, bytes, defaults, HideDefaults, Search))
        {
            ImGui.SetNextItemOpen(searching || OpenAll || first || !EngineBases.Contains(owner), searching ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
            first = false;
            if (!ImGui.CollapsingHeader($"{owner}  ({rows.Count})###group{owner}")) continue;
            foreach (var row in rows) Member(address, bytes, row.Prop, row.Prop.Name, $"+{row.Prop.Offset:X}", row.Value, depth, link);
        }
        if (OpenAll && depth < OpenAllDepth) ImGui.SetNextItemOpen(true);
        if (!ImGui.TreeNode("Raw bytes")) return;
        raw.Hex(address, Math.Clamp(cls.Size, 8, 0x1000), depth);
        ImGui.TreePop();
    }

    public void Header(string text, nint address)
    {
        ImGui.TextUnformatted(text);
        ImGui.SameLine();
        ImGui.TextDisabled($"@0x{address:X}");
        if (!ImGui.BeginPopupContextItem("header")) return;
        if (ImGui.MenuItem("Copy address")) ImGui.SetClipboardText($"0x{address:X}");
        if (ImGui.MenuItem("Pin")) raw.PinRequest = new RawPin(address, text);
        ImGui.EndPopup();
    }

    public bool Line(string offset, bool expandable, string name, string type, string value, nint pointer, int depth, nint link = 0)
    {
        var x = ImGui.GetCursorPosX();
        if (expandable && OpenAll && depth < OpenAllDepth) ImGui.SetNextItemOpen(true);
        var open = ImGui.TreeNodeEx($"{offset}###node", expandable ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        if (ImGui.BeginPopupContextItem("line"))
        {
            if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(value);
            if (ImGui.MenuItem("Copy row")) ImGui.SetClipboardText($"{offset} {name} {type} {value}");
            if (pointer != 0 && ImGui.MenuItem("Pin")) raw.PinRequest = new RawPin(pointer, name.Length > 0 ? name : $"0x{pointer:X}");
            ImGui.EndPopup();
        }
        ImGui.SameLine(x + 90f);
        ImGui.TextUnformatted(name);
        ImGui.SameLine(x + 330f);
        ImGui.TextDisabled(Text.EscPct(type));
        ImGui.SameLine(x + 480f);
        if (link != 0 && Navigate is { } go)
        {
            if (ImGui.Selectable($"{value}###link", false, ImGuiSelectableFlags.None, Vector2.Zero)) go(link);
        }
        else ImGui.TextUnformatted(value);
        return open && expandable;
    }

    void Member(nint container, byte[] bytes, UProp prop, string label, string offset, string value, int depth, bool link)
    {
        ImGui.PushID($"{prop.Offset:X}{prop.Name}{label}");
        var pointer = Pointer(bytes, prop.Offset);
        var structInfo = prop.Type == "StructProperty" ? Ue.Struct(prop.Struct) : null;
        var isObject = prop.Type is "ObjectProperty" or "ClassProperty";
        var isEntries = prop.Type is "MapProperty" or "SetProperty" && prop.Stride > 0 && Count(bytes, prop.Offset) > 0;
        var isArray = prop is { Type: "ArrayProperty", Element: not null };
        var target = link && isObject && pointer != 0 && Ue.TryObject(pointer, out _);
        var expandable = isObject ? pointer != 0 && !link : structInfo is not null || isArray || isEntries;
        if (Search.Length > 0 && structInfo is not null) ImGui.SetNextItemOpen(true);
        if (Line(offset, expandable, label, prop.Type, value, pointer, depth, target ? pointer : 0))
        {
            if (structInfo is not null) Struct(container + prop.Offset, structInfo, depth + 1, link);
            else if (isEntries) Entries(container + prop.Offset, prop, depth + 1, link);
            else if (isArray) Elements(pointer, Count(bytes, prop.Offset), prop.Element!, depth + 1, link);
            else raw.Node("target", pointer, depth + 1);
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    void Struct(nint container, UStructInfo info, int depth, bool link)
    {
        if (depth > MaxDepth)
        {
            ImGui.TextDisabled("...");
            return;
        }
        var bytes = snapshots.Get(container, Math.Clamp(info.Size, 1, MaxBlock));
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        foreach (var prop in info.Properties.Where(p => !p.Type.EndsWith("DelegateProperty", StringComparison.Ordinal)))
            Member(container, bytes, prop, prop.Name, $"+{prop.Offset:X}", Ue.Format(bytes, prop), depth, link);
    }

    void Entries(nint at, UProp prop, int depth, bool link)
    {
        if (depth > MaxDepth)
        {
            ImGui.TextDisabled("...");
            return;
        }
        var entries = Ue.Entries(at, prop, MaxElements);
        if (entries.Length == 0)
        {
            ImGui.TextDisabled("empty");
            return;
        }
        for (var i = 0; i < entries.Length; i++)
        {
            var block = snapshots.Get(entries[i].Address, Math.Clamp(prop.Stride, 1, MaxBlock));
            if (block is null)
            {
                ImGui.TextDisabled("??");
                continue;
            }
            ImGui.PushID(i);
            Member(entries[i].Address, block, entries[i].Key, $"[{i}] key", $"[{i}]", Ue.Format(block, entries[i].Key), depth, link);
            if (entries[i].Value is { } value) Member(entries[i].Address, block, value, "value", "", Ue.Format(block, value), depth, link);
            ImGui.PopID();
        }
    }

    void Elements(nint data, int count, UProp element, int depth, bool link)
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
        for (var i = 0; i < shown && (i + 1) * element.Size <= bytes.Length; i++)
        {
            ImGui.PushID(i);
            var slice = bytes.AsSpan(i * element.Size, element.Size).ToArray();
            Member(data + i * element.Size, slice, element with { Offset = 0 }, "", $"[{i}]", Ue.Format(slice, element with { Offset = 0 }), depth, link);
            ImGui.PopID();
        }
        if (count > shown) ImGui.TextDisabled($"{count - shown} more");
    }

    static nint Pointer(byte[] bytes, int offset) => offset >= 0 && offset <= bytes.Length - 8 ? (nint)BitConverter.ToInt64(bytes, offset) : 0;

    // ponytail: TArray Num at +8 is the engine-wide shape and the same TArrayNum the layout carries, expose Ue.ArrayCount(bytes, prop) if it ever differs
    static int Count(byte[] bytes, int offset) => offset >= 0 && offset <= bytes.Length - 12 ? BitConverter.ToInt32(bytes, offset + 8) : 0;
}
