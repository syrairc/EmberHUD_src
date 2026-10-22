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

internal sealed record MemberSet(Type Type, string Filter, float FontSize, Member[] Members, float NameWidth);

internal sealed class ApiTree(Action<object> pin)
{
    const int MaxDepth = 8;
    static readonly Vector4 ErrorText = new(0.9f, 0.35f, 0.35f, 0.7f);

    sealed class ListState
    {
        public readonly ListModel Model = new();
        public string Filter = "";
        public SortMode Sort;
        public int Shown = ListModel.Page;
        public Row[]? Built;
        public object? BuiltSource;
        public string BuiltFilter = "";
        public SortMode BuiltSort;
        public Vector3? BuiltAt;
    }

    readonly Dictionary<uint, ListState> _lists = [];
    readonly Dictionary<(Type, string), MemberSet> _memberSets = [];
    Player? _player;

    public object? Selected;
    public object? Hover;
    public bool ShowNulls;
    public int RootHidden { get; private set; }
    internal int Builds { get; private set; }

    public void BeginFrame() => Hover = null;

    public void Draw(string id, object? value)
    {
        ImGui.PushID(id);
        Body(value, 0);
        ImGui.PopID();
    }

    public void Show(GameState state)
    {
        _player = state.Player;
        var roots = Roots(state);
        var width = roots.Where(r => r.Value is not null && IsLeaf(r.Value)).Select(r => ImGui.CalcTextSize(r.Name).X).DefaultIfEmpty(0f).Max() + 16f;
        var start = ImGui.GetCursorPosX();
        RootHidden = 0;
        foreach (var (name, value) in roots)
        {
            if (value is null && !ShowNulls)
            {
                RootHidden++;
                continue;
            }
            ImGui.PushID(name);
            if (value is null) ImGui.TextDisabled(Text.EscPct(NotProvided(name)));
            else Member(name, value, 0, start, width);
            ImGui.PopID();
        }
    }

    internal static string NotProvided(string name) => $"{name}  not provided right now";

    internal static object? Key(object? item) => item switch
    {
        Entity entity => entity.Id,
        MapIcon { EntityId: not 0 } icon => ("icon", icon.EntityId),
        _ => item,
    };

    internal static (string Name, object? Value)[] Roots(GameState state) =>
    [
        .. Walker.Members(typeof(GameState)).Select(m => (m.Name, Walker.Read(typeof(GameState), m, state))),
        ("Map view", Map.View),
    ];

    public IEnumerable<(Vector3 Position, string Label)> Highlights()
    {
        foreach (var item in new[] { Selected, Hover }.Where(i => i is not null).Distinct())
            if (Walker.Position(item) is { } position)
                yield return (position, item!.GetType().Name);
    }

    public void Clear()
    {
        _lists.Clear();
        _memberSets.Clear();
        Selected = null;
        Hover = null;
    }

    internal int ShownOf(uint listId) => _lists.TryGetValue(listId, out var state) ? state.Shown : ListModel.Page;

    internal void ShowMore(uint listId)
    {
        if (!_lists.TryGetValue(listId, out var state)) _lists[listId] = state = new ListState();
        state.Shown += ListModel.Page;
    }

    static bool IsLeaf(object? value) => value is null || value is Failed || Walker.IsScalar(value.GetType());

    void Body(object? value, int depth)
    {
        if (depth > MaxDepth)
            ImGui.TextDisabled("...");
        else if (IsLeaf(value))
            Scalar(value, Walker.Format(value));
        else if (value is IEnumerable items)
            ListBody(items, depth);
        else
            Members(value!, depth);
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

    internal MemberSet MembersFor(Type type, string filter)
    {
        var fontSize = ImGui.GetFontSize();
        if (_memberSets.TryGetValue((type, filter), out var cached) && cached.FontSize == fontSize) return cached;
        var members = Walker.Members(type).Where(m => Text.Matches(filter, m.Name)).ToArray();
        return _memberSets[(type, filter)] = new MemberSet(type, filter, fontSize, members, members.Length == 0 ? 0f : members.Max(m => ImGui.CalcTextSize(m.Name).X) + 16f);
    }

    void Members(object value, int depth)
    {
        var (shown, hidden) = Walker.Visible(value, ShowNulls);
        var width = MembersFor(value.GetType(), "").NameWidth;
        var start = ImGui.GetCursorPosX();
        foreach (var (member, memberValue) in shown)
        {
            ImGui.PushID(member.Name);
            if (memberValue is null) ImGui.TextDisabled(Text.EscPct(NotProvided(member.Name)));
            else Member(member.Name, memberValue, depth, start, width);
            ImGui.PopID();
        }
        if (hidden > 0 && !ShowNulls) ImGui.TextDisabled($"{hidden} not provided");
    }

    static void Leaf(string name, object? value, float start, float width)
    {
        ImGui.TextDisabled(Text.EscPct(name));
        ImGui.SameLine(start + width);
        Cell(value, Walker.Format(value));
    }

    void Member(string name, object value, int depth, float start, float width)
    {
        if (IsLeaf(value))
        {
            Leaf(name, value, start, width);
            return;
        }
        var label = value is IEnumerable sequence && Walker.Count(sequence) is { } count ? $"{name} ({count})" : name;
        var open = ImGui.TreeNode(MemberLabel(label));
        RowMenu(value, label, false);
        if (!open) return;
        Body(value, depth + 1);
        ImGui.TreePop();
    }

    internal static string MemberLabel(string label) => $"{label}###tree";

    internal static string RowLabel(Row row, bool distance, Player? player)
    {
        var label = row.Label;
        if (distance && player is not null && row.Dist(player) is not null) label = $"{label}  d {row.DistText}";
        return $"{label}###row{row.Index}";
    }

    Row[] Rows(ListState state, IEnumerable items)
    {
        var at = state.Sort == SortMode.Distance ? _player?.Position : null;
        if (state.Built is null || !ReferenceEquals(state.BuiltSource, items) || state.BuiltFilter != state.Filter || state.BuiltSort != state.Sort || state.BuiltAt != at)
        {
            state.Built = state.Model.Build(items, state.Filter, state.Sort, _player);
            (state.BuiltSource, state.BuiltFilter, state.BuiltSort, state.BuiltAt) = (items, state.Filter, state.Sort, at);
            Builds++;
        }
        return state.Built;
    }

    void ListBody(IEnumerable items, int depth)
    {
        var key = ImGui.GetID("list");
        if (Walker.Count(items) > ListModel.MaxCollectionRows)
        {
            ImGui.TextDisabled(Text.EscPct($"{Walker.Format(items)} - too many rows to list"));
            return;
        }
        if (!_lists.TryGetValue(key, out var state)) _lists[key] = state = new ListState();
        Rows(state, items);
        if (state.Model.TooMany)
        {
            ImGui.TextDisabled($"more than {ListModel.LimitOf(items)} rows - not listed");
            return;
        }
        Controls.Filter(ref state.Filter, "Filter...", state.Model.Positional ? -130f : -1f);
        if (state.Model.Positional)
        {
            ImGui.SameLine();
            Combo.Enum(ref state.Sort, "sort");
        }
        var rows = Rows(state, items);
        var clipped = ListModel.Clip(rows, state.Shown);
        foreach (var row in clipped) ListRow(row, state.Sort, depth);
        if (rows.Length > clipped.Length && ImGui.Button($"show more ({rows.Length - clipped.Length} left)###more")) ShowMore(key);
    }

    void ListRow(Row row, SortMode sort, int depth)
    {
        var item = row.Item;
        var leaf = IsLeaf(item);
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (leaf) flags |= ImGuiTreeNodeFlags.Leaf;
        var selected = item is not null && Selected is not null && Equals(Key(Selected), Key(item));
        if (selected)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
            Selected = item;
        }
        var open = ImGui.TreeNodeEx(RowLabel(row, sort == SortMode.Distance, _player), flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Selected = selected ? null : item;
        if (ImGui.IsItemHovered()) Hover = item;
        RowMenu(item, row.Label, leaf);
        if (!open) return;
        if (!leaf) Body(item, depth + 1);
        ImGui.TreePop();
    }

    void RowMenu(object? item, string label, bool leaf)
    {
        if (!ImGui.BeginPopupContextItem("row")) return;
        if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(label);
        if (ImGui.MenuItem("Copy row")) ImGui.SetClipboardText(Walker.Json(item));
        if (!leaf && ImGui.MenuItem("Pin")) pin(item!);
        ImGui.EndPopup();
    }
}
