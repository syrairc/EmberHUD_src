using System.Drawing;
using System.Numerics;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public sealed class PickerOpts<T>
{
    public Func<T, string>? Primary;
    public Func<T, string>? Secondary;
    public Func<T, string>? Category;
    public Func<T, Color>? CategoryColor;
    public Func<T, string>? Search;
    public Func<T, string>? Key;
    public bool ShowSearch = true;
    public bool ShowCategoryFilter = true;
    public bool GroupByCategory = true;
    public string Hint = "Search...";
    public string? Tip;
    public string EmptyText = "nothing to pick from.";
    public Vector2 Size = new(560f, 420f);
    public float CategoryWidth = 130f;
    public int MaxRows = 300;
    public string CommitLabel = "Add";
}

public sealed class PickerState<T>
{
    public string Filter = "";
    public string Category = Picker.AnyCategory;
    public readonly List<T> Selected = [];
}

public static class Picker
{
    public const string AnyCategory = "All";

    const string DefaultTip = "Each space-separated word has to match somewhere in the row, in any order. A word is treated as a regex when it is one.";

    sealed record TermCache(string Filter, Func<string, bool>[] Terms);

    static TermCache _terms = new("", []);

    public static bool Matches(string? filter, string? text) => Terms(filter).All(m => m(text ?? ""));

    public static bool Popup<T>(string id, IEnumerable<T>? items, ref PickerState<T>? state, out T? picked, PickerOpts<T>? opts = null) =>
        Draw(id, items, ref state, false, opts, out picked, out _);

    public static bool PopupMulti<T>(string id, IEnumerable<T>? items, ref PickerState<T>? state, out List<T>? picked, PickerOpts<T>? opts = null) =>
        Draw(id, items, ref state, true, opts, out _, out picked);

    public static bool Button<T>(string label, IEnumerable<T>? items, ref PickerState<T>? state, out T? picked, PickerOpts<T>? opts = null, string? id = null)
    {
        id ??= label;
        if (ImGui.Button(label + "##" + id)) ImGui.OpenPopup(id);
        return Popup(id, items, ref state, out picked, opts);
    }

    public static bool ButtonMulti<T>(string label, IEnumerable<T>? items, ref PickerState<T>? state, out List<T>? picked, PickerOpts<T>? opts = null, string? id = null)
    {
        id ??= label;
        if (ImGui.Button(label + "##" + id)) ImGui.OpenPopup(id);
        return PopupMulti(id, items, ref state, out picked, opts);
    }

    static bool Draw<T>(string id, IEnumerable<T>? items, ref PickerState<T>? stateRef, bool multi, PickerOpts<T>? opts, out T? picked, out List<T>? committed)
    {
        picked = default;
        committed = null;
        var state = stateRef ??= new PickerState<T>();

        using var style = new Controls.PanelStyleScope();
        if (!ImGui.BeginPopup(id)) return false;
        var o = opts ?? new PickerOpts<T>();

        var appearing = ImGui.IsWindowAppearing();
        if (appearing)
        {
            state.Filter = "";
            state.Category = AnyCategory;
            state.Selected.Clear();
        }

        ImGui.BeginChild("##body", o.Size);

        var all = items as IReadOnlyList<T> ?? items?.ToList() ?? [];
        var prim = o.Primary ?? (x => x?.ToString() ?? "");
        var key = o.Key ?? prim;

        if (all.Count == 0)
        {
            ImGui.TextDisabled(Text.EscPct(o.EmptyText));
            ImGui.EndChild();
            ImGui.EndPopup();
            return false;
        }

        var matches = Header(all, state, o, prim, appearing);

        var acted = false;
        var footer = multi ? ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y : 0f;
        ImGui.BeginChild("##rows", new Vector2(0f, -footer));
        Rows(matches, state, o, prim, key, multi, ref picked, ref acted);
        ImGui.EndChild();
        if (acted) ImGui.CloseCurrentPopup();

        if (multi)
        {
            ImGui.BeginDisabled(state.Selected.Count == 0);
            if (ImGui.Button(o.CommitLabel + " (" + state.Selected.Count + ")"))
            {
                committed = [.. state.Selected];
                acted = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Clear")) state.Selected.Clear();
            ImGui.SameLine();
            if (ImGui.Button("Close")) ImGui.CloseCurrentPopup();
            if (state.Selected.Count > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(Text.EscPct(string.Join(", ", state.Selected.Select(key))));
            }
        }

        ImGui.EndChild();
        ImGui.EndPopup();
        return acted;
    }

    static List<T> Header<T>(IReadOnlyList<T> all, PickerState<T> state, PickerOpts<T> o, Func<T, string> prim, bool appearing)
    {
        if (o.ShowSearch)
        {
            ImGui.SetNextItemWidth(-90f);
            if (appearing) ImGui.SetKeyboardFocusHere();
            ImGui.InputTextWithHint("##pickfilter", o.Hint, ref state.Filter, 128);
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            Text.Tip(o.Tip ?? DefaultTip);
        }

        var cats = o.Category is { } category && o.ShowCategoryFilter ? Categories(all, category) : null;
        if (cats is { Length: > 2 })
        {
            if (cats.Length <= 4 && cats.All(c => c.Length <= 12)) Controls.ButtonGroup("##pickcat", ref state.Category, cats.Select(c => (c, c)).ToArray());
            else
            {
                ImGui.SetNextItemWidth(200f);
                if (ImGui.BeginCombo("##pickcat", state.Category))
                {
                    foreach (var c in cats)
                        if (ImGui.Selectable(c, c == state.Category)) state.Category = c;
                    ImGui.EndCombo();
                }
            }
            ImGui.SameLine();
        }
        else if (o.ShowSearch) ImGui.SameLine();

        var matches = Filter(all, state, o);
        ImGui.TextDisabled(matches.Count + " of " + all.Count);
        ImGui.Separator();

        if (o.Category is { } group && o.GroupByCategory && state.Category == AnyCategory)
            matches.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(group(a) ?? "", group(b) ?? "");
                return c != 0 ? c : string.CompareOrdinal(prim(a) ?? "", prim(b) ?? "");
            });
        return matches;
    }

    static List<T> Filter<T>(IReadOnlyList<T> all, PickerState<T> state, PickerOpts<T> o)
    {
        var terms = Terms(state.Filter);
        var only = state.Category == AnyCategory ? null : o.Category;
        return [.. all.Where(it => (only is null || (only(it) ?? "") == state.Category) && RowMatches(it, o, terms))];
    }

    static bool RowMatches<T>(T it, PickerOpts<T> o, Func<string, bool>[] terms)
    {
        if (terms.Length == 0) return true;
        if (o.Search is { } search)
        {
            var s = search(it) ?? "";
            return terms.All(m => m(s));
        }
        var a = (o.Primary is { } primary ? primary(it) : it?.ToString()) ?? "";
        var b = o.Secondary?.Invoke(it) ?? "";
        var c = o.Category?.Invoke(it) ?? "";
        return terms.All(m => m(a) || m(b) || m(c));
    }

    static void Rows<T>(List<T> matches, PickerState<T> state, PickerOpts<T> o, Func<T, string> prim, Func<T, string> key, bool multi, ref T? picked, ref bool acted)
    {
        var category = o.Category;
        if (!ImGui.BeginTable("##pickrows", category is null ? 1 : 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) return;
        ImGui.TableSetupColumn("row", ImGuiTableColumnFlags.WidthStretch);
        if (category is not null) ImGui.TableSetupColumn("cat", ImGuiTableColumnFlags.WidthFixed, o.CategoryWidth);

        var grouped = category is not null && o.GroupByCategory && state.Category == AnyCategory;
        string? curGroup = null;
        foreach (var it in matches.Take(o.MaxRows))
        {
            var cat = category?.Invoke(it) ?? "";
            if (grouped && cat != curGroup)
            {
                curGroup = cat;
                if (cat.Length > 0) Note(cat.ToUpperInvariant());
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.PushID(key(it) ?? "");
            if (Row(it, prim, o, cat, multi && IndexOf(state.Selected, it, key) >= 0))
            {
                if (!multi)
                {
                    picked = it;
                    acted = true;
                }
                else
                {
                    var at = IndexOf(state.Selected, it, key);
                    if (at >= 0) state.Selected.RemoveAt(at);
                    else state.Selected.Add(it);
                }
            }
            ImGui.PopID();
        }

        if (matches.Count > o.MaxRows) Note((matches.Count - o.MaxRows) + " more - narrow the search.");
        if (matches.Count == 0) Note("nothing matches that.");
        ImGui.EndTable();
    }

    static bool Row<T>(T it, Func<T, string> prim, PickerOpts<T> o, string cat, bool selected)
    {
        var a = prim(it) ?? "";
        var b = o.Secondary?.Invoke(it) ?? "";
        var wrap = Math.Max(80f, ImGui.GetContentRegionAvail().X - 8f);

        var h = ImGui.CalcTextSize(a, false, wrap).Y;
        if (b.Length > 0) h += ImGui.CalcTextSize(b, false, wrap).Y;

        var start = ImGui.GetCursorPos();
        var hit = ImGui.Selectable("##r", selected, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0f, h));

        ImGui.SetCursorPos(start);
        ImGui.PushTextWrapPos(start.X + wrap);
        ImGui.TextWrapped(Text.EscPct(a));
        if (b.Length > 0)
            using (new EColor.StyleColorScope((ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))))
                ImGui.TextWrapped(Text.EscPct(b));
        ImGui.PopTextWrapPos();

        if (cat.Length > 0)
        {
            ImGui.TableSetColumnIndex(1);
            var pw = Badges.Width(cat, dot: true);
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > pw) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - pw));
            Badges.Draw(cat, o.CategoryColor?.Invoke(it) ?? EColor.CategoryColor(cat), dot: true);
        }
        return hit;
    }

    static void Note(string text)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextDisabled(Text.EscPct(text));
    }

    internal static string[] Categories<T>(IReadOnlyList<T> all, Func<T, string?> category) =>
        [AnyCategory, .. all.Select(it => category(it) ?? "").Where(c => c.Length > 0).Distinct().Order(StringComparer.OrdinalIgnoreCase)];

    internal static Func<string, bool>[] Terms(string? filter)
    {
        var f = filter ?? "";
        var cached = _terms;
        if (cached.Filter == f) return cached.Terms;
        var terms = f.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(TermMatcher).ToArray();
        _terms = new TermCache(f, terms);
        return terms;
    }

    internal static Func<string, bool> TermMatcher(string term)
    {
        try
        {
            var rx = new Regex(term, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
            return rx.IsMatch;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return s => s.Contains(term, StringComparison.OrdinalIgnoreCase);
        }
    }

    static int IndexOf<T>(List<T> list, T item, Func<T, string> key)
    {
        var k = key(item) ?? "";
        return list.FindIndex(x => (key(x) ?? "") == k);
    }
}
