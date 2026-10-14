using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record Section(string Name, Func<GameState, object?> Root);

internal sealed record MemberSet(Type Type, string Filter, float FontSize, Member[] Members, float NameWidth);

internal sealed class Row(object item)
{
    Dictionary<string, object?>? _values;
    Dictionary<string, string>? _formatted;
    string? _text;
    Vector3? _distFrom;
    float? _dist;
    string _distText = "null";

    public object Item => item;

    public object? Value(Type owner, Member member)
    {
        _values ??= [];
        if (!_values.TryGetValue(member.Name, out var value)) _values[member.Name] = value = Walker.Read(owner, member, item);
        return value;
    }

    public IComparable? Key(Type owner, Member member) => Value(owner, member) as IComparable;

    public string Formatted(Type owner, Member member)
    {
        _formatted ??= [];
        if (!_formatted.TryGetValue(member.Name, out var text)) _formatted[member.Name] = text = Walker.Format(Value(owner, member));
        return text;
    }

    public float? Dist(Player from)
    {
        if (_distFrom != from.Position)
        {
            _dist = Walker.Position(item) is { } position ? from.FlatDistance(position) : null;
            _distText = _dist is { } distance ? Walker.Format(distance) : "null";
            _distFrom = from.Position;
        }
        return _dist;
    }

    public string DistText => _distText;

    public string Text => _text ??= BuildText();

    string BuildText()
    {
        var type = item.GetType();
        if (Walker.IsFlat(type)) return Walker.Format(item);
        return string.Join(' ', Walker.Members(type).Where(m => Walker.IsScalar(m.Type)).Select(m => Formatted(type, m)));
    }
}

internal sealed class ApiTree(Action<object> pin)
{
    const int MaxDepth = 8;
    const int MaxRows = 20000;
    static readonly Vector4 ErrorText = new(0.9f, 0.35f, 0.35f, 0.7f);

    public static readonly Section[] All =
    [
        new("Player", s => s.Player),
        new("Entities", s => s.Entities),
        new("Map icons", s => s.MapIcons),
        new("Terrain", s => s.Terrain),
        new("Explored", s => s.Explored),
        new("Camera", s => s.Camera),
        new("Game UI", s => s.Ui.Panels),
        new("Map view", _ => Map.View),
    ];

    readonly Dictionary<uint, string> _filters = [];
    sealed record RowSet(object Source, Row[] Rows);

    readonly ViewCache<RowSet> _rowSets = new();
    readonly Dictionary<Type, TableColumn<Row>[]> _columns = [];
    Section? _current;
    readonly ViewCache<MemberSet> _memberSets = new();
    Player? _player;

    public object? Selected;
    public object? Hover;

    public void BeginFrame() => Hover = null;

    public void Draw(string id, object? value) => Node(id, value, 0);

    public void Show(Section section, GameState state)
    {
        if (!ReferenceEquals(_current, section))
        {
            if (_current is not null) Selected = null;
            _current = section;
        }
        _player = state.Player;
        Node(section.Name, section.Root(state), 0);
    }

    public IEnumerable<(Vector3 Position, string Label)> Highlights()
    {
        foreach (var item in new[] { Selected, Hover }.Where(i => i is not null).Distinct())
            if (Walker.Position(item) is { } position)
                yield return (position, item!.GetType().Name);
    }

    public void Clear()
    {
        _filters.Clear();
        _rowSets.Clear();
        _columns.Clear();
        _memberSets.Clear();
        Selected = null;
        Hover = null;
    }

    void Node(string id, object? value, int depth)
    {
        if (depth > MaxDepth)
            ImGui.TextDisabled("...");
        else if (value is null || value is Failed || Walker.IsScalar(value.GetType()))
            Scalar(value, Walker.Format(value));
        else if (value is IEnumerable big && Walker.Count(big) > MaxRows)
            ImGui.TextDisabled(Text.EscPct($"{Walker.Format(big)} - too many rows to list"));
        else if (value is IEnumerable items)
            Table(id, items, depth == 0 ? 4000f : 300f);
        else
            Grid(id, value.GetType(), value, depth);
    }

    static void Scalar(object? value, string text)
    {
        if (value is null)
        {
            ImGui.TextDisabled("null");
            return;
        }
        if (value is Failed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ErrorText);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
            return;
        }
        if (value is Color color)
        {
            ImGui.ColorButton("##swatch", EColor.ToVector4(color), ImGuiColorEditFlags.NoTooltip, new Vector2(12, 12));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(text);
    }

    static void Cell(object? value, string text)
    {
        Scalar(value, text);
        if (!ImGui.BeginPopupContextItem("cell")) return;
        if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(text);
        ImGui.EndPopup();
    }

    void DistCell(Row row)
    {
        if (_player is { } player) Cell(row.Dist(player), row.DistText);
        else Cell(null, "null");
    }

    internal MemberSet MembersFor(Type type, string filter)
    {
        var id = ImGui.GetID("members");
        var fontSize = ImGui.GetFontSize();
        if (_memberSets.Get(id) is { } cached && cached.Type == type && cached.Filter == filter && cached.FontSize == fontSize) return cached;
        var members = Walker.Members(type).Where(m => Text.Matches(filter, m.Name)).ToArray();
        var set = new MemberSet(type, filter, fontSize, members, members.Length == 0 ? 0f : members.Max(m => ImGui.CalcTextSize(m.Name).X) + 16f);
        _memberSets.Set(id, set);
        return set;
    }

    void Grid(string id, Type type, object value, int depth)
    {
        ImGui.PushID(id);
        var key = ImGui.GetID("filter");
        var filter = _filters.GetValueOrDefault(key, "");
        Controls.Filter(ref filter, "Filter members...");
        _filters[key] = filter;

        var set = MembersFor(type, filter);
        var start = ImGui.GetCursorPosX();
        foreach (var member in set.Members)
        {
            ImGui.PushID(member.Name);
            var memberValue = Walker.Read(type, member, value);
            if (memberValue is null || memberValue is Failed || Walker.IsScalar(memberValue.GetType()))
            {
                ImGui.TextDisabled(member.Name);
                ImGui.SameLine(start + set.NameWidth);
                Scalar(memberValue, Walker.Format(memberValue));
            }
            else
            {
                var label = memberValue is IEnumerable sequence && Walker.Count(sequence) is { } count ? $"{member.Name} ({count})" : member.Name;
                if (ImGui.TreeNode($"{label}###tree"))
                {
                    Node($"{id}/{member.Name}", memberValue, depth + 1);
                    ImGui.TreePop();
                }
            }
            ImGui.PopID();
        }
        ImGui.PopID();
    }

    void Table(string id, IEnumerable items, float maxHeight)
    {
        var key = ImGui.GetID(id);
        var rows = RowsFor(key, items);
        if (rows.Length > MaxRows)
        {
            ImGui.TextDisabled($"more than {MaxRows} rows - not listed");
            return;
        }
        var type = Walker.ElementType(items.GetType());
        if (type == typeof(object) && rows.Length > 0) type = rows[0].Item.GetType();

        ImGui.TextDisabled($"{rows.Length} rows");
        var filter = _filters.GetValueOrDefault(key, "");
        SortableTable.Draw(id, rows, ColumnsFor(type), ref filter, row => filter.Length == 0 ? "" : row.Text, maxHeight);
        _filters[key] = filter;
    }

    Row[] RowsFor(uint key, IEnumerable items)
    {
        if (_rowSets.Get(key) is { } cached && ReferenceEquals(cached.Source, items)) return cached.Rows;
        var rows = items.Cast<object>().Take(MaxRows + 1).Select(item => new Row(item)).ToArray();
        _rowSets.Set(key, new RowSet(items, rows));
        return rows;
    }

    TableColumn<Row>[] ColumnsFor(Type type)
    {
        if (_columns.TryGetValue(type, out var cached)) return cached;
        List<TableColumn<Row>> columns = [new TableColumn<Row> { Header = "#", Width = 44f, Draw = SelectCell }];
        if (Walker.IsFlat(type))
            columns.Add(new TableColumn<Row> { Header = "Value", Draw = (row, _) => Cell(row.Item, row.Text) });
        else
        {
            foreach (var member in Walker.Members(type))
                columns.Add(new TableColumn<Row>
                {
                    Header = member.Name,
                    SortKey = typeof(IComparable).IsAssignableFrom(member.Type) ? row => row.Key(type, member) : null,
                    Draw = (row, _) => Cell(row.Value(type, member), row.Formatted(type, member))
                });
            if (Walker.HasPosition(type))
                columns.Add(new TableColumn<Row>
                {
                    Header = "Dist",
                    Width = 56f,
                    SortKey = row => _player is { } player ? row.Dist(player) : null,
                    Draw = (row, _) => DistCell(row)
                });
        }
        return _columns[type] = [.. columns];
    }

    void SelectCell(Row row, int index)
    {
        var selected = ReferenceEquals(Selected, row.Item);
        if (ImGui.Selectable($"{index}###select", selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
            Selected = selected ? null : row.Item;
        if (ImGui.IsItemHovered()) Hover = row.Item;
        if (!ImGui.BeginPopupContextItem("row")) return;
        if (ImGui.MenuItem("Copy row")) ImGui.SetClipboardText(Walker.Json(row.Item));
        if (ImGui.MenuItem("Pin")) pin(row.Item);
        ImGui.EndPopup();
    }
}
