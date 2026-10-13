using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Ui;
using Hexa.NET.ImGui;

namespace Market;

internal readonly record struct Pick(int Page, int Slot, long Quantity, long Price)
{
    public static Pick Of(Row row) => new(row.Page, row.Listing.Slot, row.Listing.Quantity, row.Listing.Price);
}

internal enum PickState { Here, OtherPage, Gone }

public sealed class Main : Plugin<Settings>
{
    static readonly Color Dim = Color.FromArgb(0xFF, 0x80, 0x80, 0x80);

    readonly Book _book = new();
    string _tableFilter = "";
    TableColumn<Row>[]? _columns;
    string _priceIcon = "";
    TradePage? _shown;
    readonly HashSet<Pick> _picks = [];
    Pick? _hovered;

    internal IReadOnlyCollection<Pick> Picks => _picks;

    internal void Toggle(Pick pick)
    {
        if (!_picks.Remove(pick)) _picks.Add(pick);
    }

    internal static PickState StateOf(Pick pick, TradePage shown, Book book)
    {
        if (pick.Page != shown.Page)
            return book.Find(pick.Page, pick.Slot) is { } row && Matches(pick, row.Listing) ? PickState.OtherPage : PickState.Gone;
        foreach (var listing in shown.Listings)
            if (listing.Slot == pick.Slot) return Matches(pick, listing) ? PickState.Here : PickState.Gone;
        return PickState.Gone;
    }

    static bool Matches(Pick pick, TradeListing listing) =>
        listing.Status is null && listing.Quantity == pick.Quantity && listing.Price == pick.Price;

    internal static bool Passes(Row row, Settings settings) =>
        (settings.MaxPrice.Value <= 0 || row.Listing.Price <= settings.MaxPrice.Value) &&
        (settings.MaxPerUnit.Value <= 0 || row.PerUnit <= settings.MaxPerUnit.Value);

    internal static List<Row> Shown(List<Row> rows, Settings settings, int currentPage)
    {
        return [.. rows.Where(row => !row.Sold && (!settings.CurrentPageOnly.Value || row.Page == currentPage) && Passes(row, settings))
            .OrderBy(row => row.PerUnit)
            .ThenBy(row => row.Listing.Price)
            .ThenBy(row => row.Page)
            .ThenBy(row => row.Listing.Slot)];
    }

    public override void Initialize() =>
        RegisterHotkey("Toggle window", "Shows or hides the market listing window", () => Settings.Show.Value = !Settings.Show.Value);

    public override void Tick()
    {
        var shown = Game.State.Trade;
        _book.Update(shown, Environment.TickCount64);
        if (shown is null) _picks.Clear();
        else _picks.RemoveWhere(pick => StateOf(pick, shown, _book) == PickState.Gone);
    }

    public override void Render()
    {
        if (Game.State.Trade is not { } shown || !Settings.Show.Value) return;
        _priceIcon = shown.PriceIcon;
        var rows = Shown(_book.Rows(Environment.TickCount64, Settings.StaleMinutes.Value * 60_000L), Settings, shown.Page);
        _hovered = null;
        DrawWindow(rows, shown);
        DrawGrid(shown);
    }

    void DrawWindow(List<Row> rows, TradePage shown)
    {
        _shown = shown;
        var open = true;
        ImGui.SetNextWindowSize(new Vector2(340, 560), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Market###market", ref open))
        {
            ImGui.TextDisabled($"Pages {_book.PagesText}");
            ImGui.SameLine();
            ImGui.Checkbox("Current page only", ref Settings.CurrentPageOnly.Value);
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                _book.Clear();
                _picks.Clear();
            }

            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("Max price", ref Settings.MaxPrice.Value)) Settings.MaxPrice.Value = Math.Max(0, Settings.MaxPrice.Value);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputFloat("Max per unit", ref Settings.MaxPerUnit.Value, 0, 0, "%.3f")) Settings.MaxPerUnit.Value = Math.Max(0, Settings.MaxPerUnit.Value);

            SortableTable.Draw("rows", rows, _columns ??= Columns(), ref _tableFilter, row => row.Listing.Name, ImGui.GetContentRegionAvail().Y);
        }
        ImGui.End();
        if (!open) Settings.Show.Value = false;
    }

    internal TableColumn<Row>[] Columns() =>
    [
        new() { Header = "", Width = 24, Draw = (row, _) => PickCell(row) },
        new() { Header = "Name", SortKey = row => row.Listing.Name, Draw = (row, _) => Cell(row, row.Listing.Name) },
        new() { Header = "Page", Width = 40, SortKey = row => row.Page * 100_000L + row.Listing.Slot, Draw = (row, _) => Cell(row, PageLabel(row)) },
        new() { Header = "Qty", Width = 64, SortKey = row => row.Listing.Quantity, Draw = (row, _) => Cell(row, row.Listing.Quantity.ToString("N0")) },
        new() { Header = "Price", Width = 84, SortKey = row => row.Listing.Price, Draw = (row, _) => PriceCell(row) },
        new() { Header = "Per unit", Width = 64, SortKey = row => row.PerUnit, Draw = (row, _) => Cell(row, double.IsPositiveInfinity(row.PerUnit) ? "-" : row.PerUnit.ToString("0.000")) },
    ];

    string PageLabel(Row row) =>
        _shown is { } shown && _picks.Contains(Pick.Of(row)) && StateOf(Pick.Of(row), shown, _book) == PickState.OtherPage ? $"{row.Page} >" : row.Page.ToString();

    void PickCell(Row row)
    {
        var pick = Pick.Of(row);
        var picked = _picks.Contains(pick);
        if (ImGuiP.TableGetHoveredRow() == ImGui.TableGetRowIndex()) _hovered = pick;
        if (ImGui.Checkbox($"##{row.Page}-{row.Listing.Slot}", ref picked)) Toggle(pick);
    }

    void DrawGrid(TradePage shown)
    {
        var color = Settings.GlowColor.Value;
        if (Settings.DimNonMatches.Value)
            foreach (var listing in Dimmed(shown, Settings))
                Draw.RectFilled(listing.Min, listing.Max, Color.FromArgb(128, 0, 0, 0));
        if (_hovered is { } hovered && hovered.Page == shown.Page && Cell(shown, hovered.Slot) is { } hover)
            Draw.Rect(hover.Min, hover.Max, Color.FromArgb(color.A * 7 / 10, color), 2);
        var pulse = 0.65f + 0.35f * MathF.Sin((float)ImGui.GetTime() * 1000f / 180f);
        var thickness = Settings.GlowThickness.Value;
        foreach (var pick in _picks)
            if (StateOf(pick, shown, _book) == PickState.Here && Cell(shown, pick.Slot) is { } picked)
                Draw.GlowRect(picked.Min, picked.Max, Color.FromArgb((int)(color.A * pulse), color), thickness, thickness * 4);
    }

    internal static IEnumerable<TradeListing> Dimmed(TradePage shown, Settings settings) =>
        shown.Listings.Where(listing =>
        {
            var row = new Row(shown.Page, listing, false);
            return row.Sold || !Passes(row, settings);
        });

    static TradeListing? Cell(TradePage shown, int slot)
    {
        foreach (var listing in shown.Listings)
            if (listing.Slot == slot) return listing;
        return null;
    }

    static void Cell(Row row, string text)
    {
        if (row.Stale) ImGui.TextColored(EColor.ToVector4(Dim), text);
        else ImGui.TextUnformatted(text);
    }

    void PriceCell(Row row)
    {
        var size = ImGui.GetTextLineHeight();
        if (_priceIcon.StartsWith(Draw.GameIconPrefix, StringComparison.Ordinal) && Draw.TryGameIcon(_priceIcon[Draw.GameIconPrefix.Length..], out var sprite))
        {
            Controls.IconImage(sprite, size, row.Stale ? Dim : Color.White);
            ImGui.SameLine();
            Cell(row, row.Listing.Price.ToString("N0"));
        }
        else Cell(row, $"{row.Listing.Price:N0} FE");
    }
}
