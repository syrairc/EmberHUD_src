using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;

namespace DummyDpsPlugin;

internal readonly record struct Row(string Label, string Value, bool Note = false);

public sealed class Main : Plugin<Settings>
{
    const float Gap = 4;
    const float Pad = 8;
    static readonly string[] Units = ["", "K", "M", "B", "T", "Qa", "Qi"];

    internal static string Compact(long value)
    {
        var scaled = (double)value;
        var unit = 0;
        while (scaled >= 999.5 && unit < Units.Length - 1)
        {
            scaled /= 1000;
            unit++;
        }
        var format = unit == 0 ? "0" : scaled < 10 ? "0.00" : scaled < 100 ? "0.0" : "0";
        return scaled.ToString(format, CultureInfo.InvariantCulture) + Units[unit];
    }

    internal static string Exact(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    internal static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    internal static bool Over(long unlimited, long limited) => unlimited > limited + limited / 1000;

    internal static List<Row> Rows(WoodStakeStats stats, bool exact)
    {
        string Show(long value) => exact ? Exact(value) : Compact(value);
        List<Row> rows =
        [
            new("Average DPS in a span of time", Show(stats.AvgUnlimited)),
            new("Duration", Clock(stats.Seconds)),
            new("Highest DPS in 5s", Show(stats.MaxUnlimited)),
            new("Average DPS in 5s", Show(stats.CurUnlimited)),
        ];
        if (Over(stats.CurUnlimited, stats.Cur) || Over(stats.AvgUnlimited, stats.Avg) || Over(stats.MaxUnlimited, stats.Max))
            rows.Add(new("Game tracker is capped, shows", $"{Show(stats.Avg)} avg", true));
        return rows;
    }

    internal static bool Visible(WoodStakeStats? stats, int areaId, int keptArea, bool keepAfterStop) =>
        stats is not null && (stats.Running || keepAfterStop && stats.Seconds > 0 && areaId == keptArea);

    readonly Overlay _panel = new();
    int _keptArea = int.MinValue;

    public override void Tick()
    {
        if (Game.State.WoodStake is { Running: true })
            _keptArea = Game.State.AreaId;
    }

    public override void Initialize() =>
        RegisterHotkey("Toggle panel", "Shows or hides the dummy DPS panel", () => Settings.General.Show = !Settings.General.Show);

    public override void Render()
    {
        var state = Game.State;
        var general = Settings.General;
        if (!general.Show || general.HideBehindPanels && state.Ui.AnyFullScreen || !Visible(state.WoodStake, state.AreaId, _keptArea, general.KeepAfterStop)) return;

        var look = Settings.Look;
        var rows = Rows(state.WoodStake!, general.Exact);
        var titleHeight = Draw.MeasureText("Ag", look.Title).Y;
        var lineHeight = Draw.MeasureText("Ag", look.Values).Y;
        var size = new Vector2(general.Width, Pad * 2 + titleHeight + Gap + rows.Count * (lineHeight + Gap));

        _panel.Locked = general.Lock;
        _panel.MoveHeld = general.MoveKey.Key != Key.None && Input.IsDown(general.MoveKey.Key);
        var origin = new Vector2(general.X, general.Y);
        var (hovered, edge, active) = _panel.Handle("dummydps", ref origin, size, ref general.X, ref general.Y, ref general.Width, GeneralTab.MinWidth, GeneralTab.MaxWidth);
        if (hovered || active) Overlay.ClickBlocker("dummydps", origin, size);

        Draw.RectFilled(origin, origin + size, look.Background, 6);
        var at = origin + new Vector2(Pad, Pad);
        Draw.Text("Training dummy", at, look.Title);
        at.Y += titleHeight + Gap;
        foreach (var row in rows)
        {
            Draw.Text(row.Label, at, row.Note ? look.Note : look.Labels);
            Draw.Text(row.Value, new Vector2(origin.X + size.X - Pad, at.Y), row.Note ? look.Note : look.Values, Align.Right);
            at.Y += lineHeight + Gap;
        }
        if (_panel.Movable) Overlay.DragHint(origin, origin + size, active, hovered, edge);
    }
}
