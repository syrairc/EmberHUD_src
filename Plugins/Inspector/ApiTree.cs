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

internal sealed class Row(object item)
{
    string? _text;
    Dictionary<string, IComparable?>? _keys;
    Vector3? _distFrom;
    float? _dist;

    public object Item => item;

    public IComparable? Key(Type owner, Member member)
    {
        _keys ??= [];
        if (!_keys.TryGetValue(member.Name, out var key)) _keys[member.Name] = key = Walker.Read(owner, member, item) as IComparable;
        return key;
    }

    public float? Dist(Vector3 from)
    {
        if (_distFrom != from)
        {
            _dist = Walker.Distance(item, from);
            _distFrom = from;
        }
        return _dist;
    }

    public string Text => _text ??= BuildText();

    string BuildText()
    {
        var type = item.GetType();
        if (Walker.IsFlat(type)) return Walker.Format(item);
        return string.Join(' ', Walker.Members(type).Where(m => Walker.IsScalar(m.Type)).Select(m => Walker.Format(Walker.Read(type, m, item))));
    }
}

internal sealed class ApiTree
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
    readonly Dictionary<uint, (object Source, Row[] Rows)> _rows = [];
    readonly Dictionary<Type, TableColumn<Row>[]> _columns = [];
    Section? _current;
    Vector3? _origin;

    public bool OpenAll;
    public object? Selected;
    public object? Hover;
    public bool ShowsPositions;
    public object? PinRequest;

    public void BeginFrame() => Hover = null;

    public void Draw(string id, object? value) => Node(id, value, 0);

    public void Show(Section section, GameState state)
    {
        if (!ReferenceEquals(_current, section))
        {
            if (_current is not null) Selected = null;
            _current = section;
        }
        _origin = state.Player?.Position;
        ShowsPositions = false;
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
        _rows.Clear();
        _columns.Clear();
        Selected = null;
        Hover = null;
    }

    void Node(string id, object? value, int depth)
    {
        if (depth > MaxDepth)
            ImGui.TextDisabled("...");
        else if (value is null || value is Failed || Walker.IsScalar(value.GetType()))
            Scalar(value);
        else if (value is IEnumerable big && Walker.Count(big) > MaxRows)
            ImGui.TextDisabled(Text.EscPct($"{Walker.Format(big)} - too many rows to list"));
        else if (value is IEnumerable items)
            Table(id, items, depth == 0 ? 4000f : 300f);
        else
            Grid(id, value.GetType(), value, depth);
    }

    static void Scalar(object? value)
    {
        if (value is null)
        {
            ImGui.TextDisabled("null");
            return;
        }
        if (value is Failed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ErrorText);
            ImGui.TextUnformatted(Walker.Format(value));
            ImGui.PopStyleColor();
            return;
        }
        if (value is Color color)
        {
            ImGui.ColorButton("##swatch", EColor.ToVector4(color), ImGuiColorEditFlags.NoTooltip, new Vector2(12, 12));
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(Walker.Format(value));
    }

    static void Cell(object? value)
    {
        Scalar(value);
        if (!ImGui.BeginPopupContextItem("cell")) return;
        if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(Walker.Format(value));
        ImGui.EndPopup();
    }

    void Grid(string id, Type type, object value, int depth)
    {
        ImGui.PushID(id);
        var key = ImGui.GetID("filter");
        var filter = _filters.GetValueOrDefault(key, "");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##filter", "Filter members...", ref filter, 64);
        _filters[key] = filter;

        var members = Walker.Members(type).Where(m => Text.Matches(filter, m.Name)).ToArray();
        var start = ImGui.GetCursorPosX();
        var nameWidth = members.Length == 0 ? 0f : members.Max(m => ImGui.CalcTextSize(m.Name).X) + 16f;
        foreach (var member in members)
        {
            ImGui.PushID(member.Name);
            var memberValue = Walker.Read(type, member, value);
            if (memberValue is null || memberValue is Failed || Walker.IsScalar(memberValue.GetType()))
            {
                ImGui.TextDisabled(member.Name);
                ImGui.SameLine(start + nameWidth);
                Scalar(memberValue);
            }
            else
            {
                if (OpenAll) ImGui.SetNextItemOpen(true);
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
        var type = Walker.ElementType(items.GetType());
        if (type == typeof(object) && rows.Length > 0) type = rows[0].Item.GetType();
        if (Walker.HasPosition(type)) ShowsPositions = true;

        ImGui.TextDisabled($"{rows.Length} rows");
        var filter = _filters.GetValueOrDefault(key, "");
        SortableTable.Draw(id, rows, ColumnsFor(type), ref filter, row => filter.Length == 0 ? "" : row.Text, maxHeight);
        _filters[key] = filter;
    }

    Row[] RowsFor(uint key, IEnumerable items)
    {
        if (_rows.TryGetValue(key, out var cached) && ReferenceEquals(cached.Source, items)) return cached.Rows;
        var rows = items.Cast<object>().Select(item => new Row(item)).ToArray();
        _rows[key] = (items, rows);
        return rows;
    }

    TableColumn<Row>[] ColumnsFor(Type type)
    {
        if (_columns.TryGetValue(type, out var cached)) return cached;
        List<TableColumn<Row>> columns = [new TableColumn<Row> { Header = "#", Width = 44f, Draw = SelectCell }];
        if (Walker.IsFlat(type))
            columns.Add(new TableColumn<Row> { Header = "Value", Draw = (row, _) => Cell(row.Item) });
        else
        {
            foreach (var member in Walker.Members(type))
                columns.Add(new TableColumn<Row>
                {
                    Header = member.Name,
                    SortKey = typeof(IComparable).IsAssignableFrom(member.Type) ? row => row.Key(type, member) : null,
                    Draw = (row, _) => Cell(Walker.Read(type, member, row.Item))
                });
            if (Walker.HasPosition(type))
                columns.Add(new TableColumn<Row>
                {
                    Header = "Dist",
                    Width = 56f,
                    SortKey = row => _origin is { } origin ? row.Dist(origin) : null,
                    Draw = (row, _) => Cell(_origin is { } origin ? row.Dist(origin) : null)
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
        if (ImGui.MenuItem("Pin")) PinRequest = row.Item;
        ImGui.EndPopup();
    }
}
