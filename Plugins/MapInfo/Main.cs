using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Ui;

namespace MapInfoPlugin;

internal enum RowKind { Title, Line, Bar, Content, Modifier }

internal readonly record struct Row(RowKind Kind, string Text, MapContent Content = default, int Remaining = 0, int Max = 0);

internal readonly record struct Laid(Row Row, string[] Lines, float LineHeight, float Height);

public sealed class Main : Plugin<Settings>
{
    internal const float Gap = 4;
    const float UnderlineGap = 4;

    internal static List<Row> Rows(MapInfo map, SectionsTab show, int? enemyMax = null)
    {
        List<Row> rows = [];
        if (show.Title.Value && map.Name.Length > 0) rows.Add(new Row(RowKind.Title, map.Name));
        List<string> parts = [];
        if (show.Level.Value) parts.Add($"Monster level {map.MonsterLevel}");
        var bar = show.Enemies.Value && show.EnemiesBar.Value;
        if (show.Enemies.Value && !bar && map.Remaining is { } remaining) parts.Add($"{remaining} enemies");
        if (parts.Count > 0) rows.Add(new Row(RowKind.Line, string.Join(" · ", parts)));
        if (bar && map.Remaining is { } left)
        {
            var max = Math.Max(enemyMax ?? 0, left);
            rows.Add(new Row(RowKind.Bar, $"{left} / {max}", Remaining: left, Max: max));
        }
        if (show.Xp.Value && map.XpGain is { } xp) rows.Add(new Row(RowKind.Line, $"Map Base XP gain: {xp}"));
        if (show.Content.Value) rows.AddRange(map.Content.Select(c => new Row(RowKind.Content, c.Total > 1 ? $"{c.Name}  {c.Done}/{c.Total}" : c.Name, c)));
        if (show.Modifiers.Value) rows.AddRange(map.Modifiers.Select(m => new Row(RowKind.Modifier, m)));
        return rows;
    }

    internal static bool Hidden(Settings settings, GameState state) =>
        !settings.General.Show.Value || state.Map is not { } map ||
        settings.General.HideInTown.Value && map.IsTown ||
        settings.General.HideBehindPanels.Value && state.Ui.AnyFullScreen;

    internal static TextStyle StyleOf(RowKind kind, LookTab look) => kind switch
    {
        RowKind.Title => look.Title,
        RowKind.Line => look.Lines,
        RowKind.Bar => look.Bar,
        RowKind.Content => look.ContentText,
        _ => look.Modifiers,
    };

    internal static List<Laid> Layout(List<Row> rows, LookTab look, float width)
    {
        List<Laid> laid = [];
        foreach (var row in rows)
        {
            var style = StyleOf(row.Kind, look);
            var lineHeight = Draw.MeasureText("Ag", style).Y;
            var textWidth = row.Kind == RowKind.Content ? width - 2 * (look.IconSize.Value + Gap) : width;
            string[] lines = row.Kind is RowKind.Title or RowKind.Bar ? [row.Text] : Text.Wrap(row.Text, Math.Max(1, textWidth), style).ToArray();
            var height = row.Kind switch
            {
                RowKind.Title => lineHeight + UnderlineGap + 2,
                RowKind.Bar => look.BarHeight.Value,
                RowKind.Content => Math.Max(look.IconSize.Value, lineHeight * lines.Length),
                _ => lineHeight * lines.Length,
            };
            laid.Add(new Laid(row, lines, lineHeight, height + Gap));
        }
        return laid;
    }

    readonly Overlay _panel = new();
    int _maxArea = -1;
    int _max;

    internal int SeenMax => _max;

    public override void Tick() => EnemyMax(Game.State.AreaId, Game.State.Map?.Remaining);

    internal int EnemyMax(int areaId, int? remaining)
    {
        if (areaId != _maxArea)
        {
            _maxArea = areaId;
            _max = 0;
        }
        return _max = Math.Max(_max, remaining ?? 0);
    }

    public override void Initialize() =>
        RegisterHotkey("Toggle panel", "Shows or hides the map info panel", () => Settings.General.Show.Value = !Settings.General.Show.Value);

    public override void Render()
    {
        var state = Game.State;
        if (Hidden(Settings, state)) return;
        var general = Settings.General;
        var look = Settings.Look;
        var laid = Layout(Rows(state.Map!, Settings.Sections, _max), look, general.Width.Value);
        if (laid.Count == 0) return;

        _panel.Locked = general.Lock.Value;
        _panel.MoveHeld = general.MoveKey.Value.Key != Key.None && Input.IsDown(general.MoveKey.Value.Key);
        var origin = new Vector2(general.X.Value, general.Y.Value);
        var size = new Vector2(general.Width.Value, laid.Sum(l => l.Height));
        var (hovered, edge, active) = _panel.Handle("mapinfo", ref origin, size, ref general.X.Value, ref general.Y.Value, ref general.Width.Value, general.Width.Min, general.Width.Max);
        if (hovered || active) Overlay.ClickBlocker("mapinfo", origin, size);

        var at = origin;
        foreach (var row in laid)
        {
            DrawRow(row, at, size.X, look);
            at.Y += row.Height;
        }
        if (_panel.Movable) Overlay.DragHint(origin, origin + size, active, hovered, edge);
    }

    static void DrawRow(Laid laid, Vector2 at, float width, LookTab look)
    {
        var style = StyleOf(laid.Row.Kind, look);
        if (laid.Row.Kind == RowKind.Content)
        {
            var icon = look.IconSize.Value;
            var middle = at.Y + Math.Max(icon, laid.LineHeight * laid.Lines.Length) / 2;
            Draw.Icon(laid.Row.Content.Icon, new Vector2(at.X + icon / 2, middle), icon / 2, Color.White, null);
            var textAt = new Vector2(at.X + icon + Gap, middle - laid.LineHeight * laid.Lines.Length / 2);
            foreach (var line in laid.Lines)
            {
                Draw.Text(line, textAt, style);
                textAt.Y += laid.LineHeight;
            }
            DrawMark(new Vector2(at.X + width - icon / 2, middle), icon * 0.4f, laid.Row.Content.State, look);
            return;
        }

        if (laid.Row.Kind == RowKind.Bar)
        {
            var height = look.BarHeight.Value;
            var row = laid.Row;
            Draw.Bar(at, at + new Vector2(width, height), row.Max > 0 ? 1 - row.Remaining / (float)row.Max : 0, look.BarFill.Value, look.BarTrack.Value);
            Draw.Text(row.Text, at + new Vector2(width / 2, height / 2), style, Align.Center | Align.Middle);
            return;
        }

        foreach (var line in laid.Lines)
        {
            Draw.Text(line, at, style);
            at.Y += laid.LineHeight;
        }
        if (laid.Row.Kind == RowKind.Title)
            Draw.RectGradient(new Vector2(at.X, at.Y + UnderlineGap), new Vector2(at.X + width, at.Y + UnderlineGap + 2), look.Underline.Value, Color.FromArgb(0, look.Underline.Value));
    }

    internal static void DrawMark(Vector2 centre, float radius, ContentState state, LookTab look)
    {
        switch (state)
        {
            case ContentState.Done:
                Span<Vector2> tick = [centre + new Vector2(-radius, 0), centre + new Vector2(-radius * 0.3f, radius * 0.7f), centre + new Vector2(radius, -radius * 0.7f)];
                Draw.Polyline(tick, look.Done.Value, 2.5f);
                break;
            case ContentState.Active:
                Draw.Circle(centre, radius, look.Active.Value, 2);
                Draw.CircleFilled(centre, radius * 0.5f, look.Active.Value);
                break;
            default:
                Draw.Circle(centre, radius, look.Pending.Value, 2);
                break;
        }
    }
}
