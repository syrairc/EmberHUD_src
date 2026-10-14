using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record RawPin(nint Address, string Label);

internal readonly record struct HexRow(string Offset, string Text, string? Target, nint Pointer);

internal readonly record struct PropRow(UProp Prop, string Label, string Offset, string Value, nint Pointer);

internal sealed record EntryRow(nint Address, byte[]? Block, PropRow? Key, PropRow? Value);

internal sealed class PropGroup(string owner)
{
    string? _header;

    public string Owner => owner;

    public List<PropRow> Rows { get; } = [];

    public string Header => _header ??= $"{owner}  ({Rows.Count})###group{owner}";
}

internal sealed class RawView(Snapshots snapshots, Action<object> pin)
{
    const int MaxDepth = 8;
    const int MaxBlock = 0x4000;
    const int MaxElements = 200;
    const int OpenAllDepth = 2;
    const int MaxSearchDepth = 4;
    const int HexDefault = 0x200;
    const int HexStep = 0x100;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly HashSet<string> EngineBases = ["Object", "Actor", "Pawn", "Controller", "Character", "ActorComponent", "SceneComponent", "PrimitiveComponent"];

    sealed record Snap(byte[] Bytes, object Value, object? Key);

    sealed record EntryCache(UEntry[] Entries, EntryRow[] Rows);

    sealed record Grouped(byte[]? Defaults, bool HideDefaults, string Search, List<PropGroup> Groups);

    readonly Dictionary<uint, int> _hexLength = [];
    readonly HashSet<nint> _path = [];
    readonly ViewCache<Snap> _snaps = new();
    string _address = "";
    nint _goto;

    public bool HideDefaults;
    public string Search = "";
    public bool OpenAll;
    public Action<nint>? Navigate;

    public void Node(string id, nint address, int depth)
    {
        if (!Attached()) return;
        ImGui.PushID(id);
        if (depth > MaxDepth) ImGui.TextDisabled("...");
        else if (!_path.Add(address)) ImGui.TextDisabled($"cycle 0x{address:X}");
        else
        {
            if (Ue.TryObject(address, out var obj) && obj.Class is { } cls) Object(address, obj, cls, depth, false);
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
        Toolbar();
        foreach (var (name, address) in Memory.Globals.OrderBy(pair => pair.Key))
        {
            if (OpenAll) ImGui.SetNextItemOpen(true);
            if (!ImGui.TreeNode($"{name}  0x{address:X}###{name}")) continue;
            Node(name, address, 1);
            ImGui.TreePop();
        }
    }

    public void Address()
    {
        if (!Attached()) return;
        Toolbar();
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##address", "0x...", ref _address, 32);
        ImGui.SameLine();
        if (ImGui.Button("Go") && Walker.TryHex(_address, out var parsed)) _goto = parsed;
        if (_goto != 0) Node("goto", _goto, 0);
    }

    internal static bool Attached()
    {
        if (Memory.Attached) return true;
        ImGui.TextDisabled("not attached (--fake or no game)");
        return false;
    }

    internal HexRow[] RowsOf(byte[] bytes)
    {
        if (Snapped<HexRow[]>("hex", bytes) is { } cached) return cached;
        var rows = new HexRow[bytes.Length / 8];
        for (var i = 0; i < rows.Length; i++)
        {
            var offset = i * 8;
            var row = bytes.AsSpan(offset, 8);
            var pointer = (nint)BitConverter.ToInt64(row);
            var text = $"{BitConverter.ToString(bytes, offset, 8).Replace('-', ' ')}   {BitConverter.ToInt32(row)} {BitConverter.ToInt32(row[4..])}   {BitConverter.ToSingle(row).ToString("0.###", Invariant)} {BitConverter.ToSingle(row[4..]).ToString("0.###", Invariant)}   {BitConverter.ToDouble(row).ToString("G6", Invariant)}";
            rows[i] = new HexRow($"+{offset:X}", text, PointerLabel(pointer), pointer);
        }
        Keep("hex", bytes, rows);
        return rows;
    }

    internal void Hex(nint address, int length, int depth)
    {
        var bytes = snapshots.Get(address, length);
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        var rows = RowsOf(bytes);
        for (var i = 0; i < rows.Length; i++)
        {
            ImGui.PushID(i * 8);
            var row = rows[i];
            if (Line(row.Offset, row.Target is not null, row.Target ?? "", "", row.Text, row.Target is null ? 0 : row.Pointer, depth))
            {
                Node("target", row.Pointer, depth + 1);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }

    string? PointerLabel(nint pointer)
    {
        if (pointer < 0x10000 || pointer > 0x7FFF_FFFF_0000) return null;
        if (Ue.TryObject(pointer, out var obj)) return $"-> {obj.Class?.Name ?? "?"} {obj.Name}";
        return snapshots.Get(pointer, 8) is null ? null : "->";
    }

    T? Snapped<T>(string id, byte[] bytes, object? key = null) where T : class =>
        _snaps.Get(ImGui.GetID(id)) is { } hit && ReferenceEquals(hit.Bytes, bytes) && Equals(hit.Key, key) ? hit.Value as T : null;

    void Keep(string id, byte[] bytes, object value, object? key = null) => _snaps.Set(ImGui.GetID(id), new Snap(bytes, value, key));

    internal List<PropGroup> Groups(UStructInfo cls, byte[] bytes, byte[]? defaults)
    {
        if (Snapped<Grouped>("groups", bytes) is { } cached && ReferenceEquals(cached.Defaults, defaults) && cached.HideDefaults == HideDefaults && cached.Search == Search) return cached.Groups;
        var groups = Group(cls, bytes, defaults, HideDefaults, Search);
        Keep("groups", bytes, new Grouped(defaults, HideDefaults, Search, groups));
        return groups;
    }

    public void Toolbar()
    {
        ImGui.Checkbox("Hide defaults", ref HideDefaults);
        ImGui.SameLine();
        ImGui.Checkbox("Expand", ref OpenAll);
        Text.Tip("Opens groups and nodes 2 levels deep");
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
        foreach (var group in Groups(cls, bytes, defaults))
        {
            ImGui.SetNextItemOpen(searching || OpenAll || first || !EngineBases.Contains(group.Owner), searching ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
            first = false;
            if (!ImGui.CollapsingHeader(group.Header)) continue;
            foreach (var row in group.Rows) Member(address, bytes, row, depth, link);
        }
        if (OpenAll && depth < OpenAllDepth) ImGui.SetNextItemOpen(true);
        if (!ImGui.TreeNode("Raw bytes")) return;
        Hex(address, Math.Clamp(cls.Size, 8, 0x1000), depth);
        ImGui.TreePop();
    }

    public void Header(string text, nint address)
    {
        ImGui.TextUnformatted(text);
        ImGui.SameLine();
        ImGui.TextDisabled($"@0x{address:X}");
        if (!ImGui.BeginPopupContextItem("header")) return;
        AddressMenu(address, text);
        ImGui.EndPopup();
    }

    internal void AddressMenu(nint address, string label)
    {
        if (ImGui.MenuItem("Copy address")) ImGui.SetClipboardText($"0x{address:X}");
        if (ImGui.MenuItem("Pin")) pin(new RawPin(address, label));
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
            if (pointer != 0) AddressMenu(pointer, name.Length > 0 ? name : $"0x{pointer:X}");
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

    void Member(nint container, byte[] bytes, PropRow row, int depth, bool link)
    {
        var prop = row.Prop;
        ImGui.PushID(HashCode.Combine(prop.Offset, prop.BoolMask, prop.Name, row.Label));
        var structInfo = prop.Type == "StructProperty" ? Ue.Struct(prop.Struct) : null;
        var isObject = prop.Type is "ObjectProperty" or "ClassProperty";
        var isEntries = prop.Type is "MapProperty" or "SetProperty" && prop.Stride > 0 && Ue.ArrayCount(bytes, prop) > 0;
        var isArray = prop is { Type: "ArrayProperty", Element: not null };
        var expandable = isObject ? row.Pointer != 0 && !link : structInfo is not null || isArray || isEntries;
        if (Search.Length > 0 && structInfo is not null) ImGui.SetNextItemOpen(true);
        if (Line(row.Offset, expandable, row.Label, prop.Type, row.Value, row.Pointer, depth, LinkTarget(row, link)))
        {
            if (structInfo is not null) Struct(container + prop.Offset, structInfo, depth + 1, link);
            else if (isEntries) Entries(bytes, container + prop.Offset, prop, depth + 1, link);
            else if (isArray) Elements(row.Pointer, Ue.ArrayCount(bytes, prop), prop.Element!, depth + 1, link);
            else Node("target", row.Pointer, depth + 1);
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    internal static nint LinkTarget(PropRow row, bool link) =>
        link && row.Prop.Type is "ObjectProperty" or "ClassProperty" && row.Pointer != 0 && Ue.TryObject(row.Pointer, out _) ? row.Pointer : 0;

    static PropRow RowOf(byte[] bytes, UProp prop, string label, string offset) => new(prop, label, offset, Ue.Format(bytes, prop), Pointer(bytes, prop.Offset));

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
        foreach (var row in StructRows(info, bytes)) Member(container, bytes, row, depth, link);
    }

    internal PropRow[] StructRows(UStructInfo info, byte[] bytes)
    {
        if (Snapped<PropRow[]>("struct", bytes, info) is { } cached) return cached;
        var rows = info.Properties.Where(p => !p.Type.EndsWith("DelegateProperty", StringComparison.Ordinal)).Select(p => RowOf(bytes, p, p.Name, $"+{p.Offset:X}")).ToArray();
        Keep("struct", bytes, rows, info);
        return rows;
    }

    void Entries(byte[] bytes, nint at, UProp prop, int depth, bool link)
    {
        if (depth > MaxDepth)
        {
            ImGui.TextDisabled("...");
            return;
        }
        var entries = EntryRows(bytes, at, prop);
        if (entries.Length == 0)
        {
            ImGui.TextDisabled("empty");
            return;
        }
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Block is null || entry.Key is not { } key)
            {
                ImGui.TextDisabled("??");
                continue;
            }
            ImGui.PushID(i);
            Member(entry.Address, entry.Block, key, depth, link);
            if (entry.Value is { } value) Member(entry.Address, entry.Block, value, depth, link);
            ImGui.PopID();
        }
    }

    internal EntryRow[] EntryRows(byte[] bytes, nint at, UProp prop)
    {
        var key = (at, prop.Stride, prop.Offset);
        if (Snapped<EntryCache>("entries", bytes, key) is not { } cache)
        {
            var entries = Ue.Entries(at, prop, MaxElements);
            cache = new EntryCache(entries, entries.Select(entry => new EntryRow(entry.Address, null, null, null)).ToArray());
            Keep("entries", bytes, cache, key);
        }
        for (var i = 0; i < cache.Rows.Length; i++)
        {
            var entry = cache.Entries[i];
            var block = snapshots.Get(entry.Address, Math.Clamp(prop.Stride, 1, MaxBlock));
            if (ReferenceEquals(cache.Rows[i].Block, block)) continue;
            cache.Rows[i] = block is null
                ? new EntryRow(entry.Address, null, null, null)
                : new EntryRow(entry.Address, block, RowOf(block, entry.Key, $"[{i}] key", $"[{i}]"), entry.Value is { } value ? RowOf(block, value, "value", "") : null);
        }
        return cache.Rows;
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
        var rows = ElementRows(bytes, element, shown);
        for (var i = 0; i < rows.Length; i++)
        {
            ImGui.PushID(i);
            Member(data, bytes, rows[i], depth, link);
            ImGui.PopID();
        }
        if (count > shown) ImGui.TextDisabled($"{count - shown} more");
    }

    internal PropRow[] ElementRows(byte[] bytes, UProp element, int shown)
    {
        if (Snapped<PropRow[]>("elements", bytes, (element, shown)) is { } cached) return cached;
        var rows = new PropRow[Math.Min(shown, bytes.Length / element.Size)];
        for (var i = 0; i < rows.Length; i++) rows[i] = RowOf(bytes, element with { Offset = i * element.Size }, "", $"[{i}]");
        Keep("elements", bytes, rows, (element, shown));
        return rows;
    }

    static nint Pointer(byte[] bytes, int offset) => offset >= 0 && offset <= bytes.Length - 8 ? (nint)BitConverter.ToInt64(bytes, offset) : 0;

    internal static List<PropGroup> Group(UStructInfo info, byte[] bytes, byte[]? defaults, bool hideDefaults, string search)
    {
        List<PropGroup> groups = [];
        for (var cls = info; cls is not null; cls = cls.Super) groups.Add(new PropGroup(cls.Name));
        foreach (var prop in info.Properties)
        {
            if (prop.Type.EndsWith("DelegateProperty", StringComparison.Ordinal)) continue;
            if (hideDefaults && defaults is not null && !Differs(bytes, defaults, prop)) continue;
            var row = RowOf(bytes, prop, prop.Name, $"+{prop.Offset:X}");
            if (search.Length > 0 && !Matches(prop, bytes, row.Value, search, 0)) continue;
            var group = groups.Find(g => g.Owner == prop.Owner);
            if (group is null) groups.Add(group = new PropGroup(prop.Owner));
            group.Rows.Add(row);
        }
        groups.RemoveAll(g => g.Rows.Count == 0);
        return groups;
    }

    static bool Differs(byte[] bytes, byte[] defaults, UProp prop)
    {
        if (prop.Offset < 0 || prop.Size <= 0 || (long)prop.Offset + prop.Size > Math.Min(bytes.Length, defaults.Length)) return true;
        if (prop.Type == "BoolProperty") return ((bytes[prop.Offset] ^ defaults[prop.Offset]) & prop.BoolMask) != 0;
        return !bytes.AsSpan(prop.Offset, prop.Size).SequenceEqual(defaults.AsSpan(prop.Offset, prop.Size));
    }

    static bool Matches(UProp prop, byte[] bytes, string value, string search, int depth)
    {
        if (prop.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || prop.Type.Contains(search, StringComparison.OrdinalIgnoreCase) || value.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (depth >= MaxSearchDepth || prop.Type != "StructProperty" || Ue.Struct(prop.Struct) is not { } inner) return false;
        return inner.Properties.Any(member =>
        {
            var shifted = member with { Offset = member.Offset + prop.Offset };
            return Matches(shifted, bytes, Ue.Format(bytes, shifted), search, depth + 1);
        });
    }
}
