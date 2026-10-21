using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;
using EmberHUD.Ui;

namespace Logging;

public sealed class Main : Plugin<Settings>
{
    readonly Overlay _overlay = new();
    readonly HashSet<string> _sources = [];
    readonly Throttle _daysThrottle = new(5000, () => Environment.TickCount64);
    LogEntry[]? _raw;
    (int Version, DateTime Date, DateOnly? Day, int Max) _rawKey;
    (int Loads, LogLevel Level, string Search, DateTime ClearedAt, int Sources, int Max) _shownKey;
    int _loads;
    int _sourceEpoch;
    string[] _sourceNames = [];
    string _sourcePreview = "All sources";
    DateOnly[] _days = [];
    string[] _dayLabels = ["Today"];
    bool _levelSet;

    internal int FilterRuns { get; private set; }
    internal LogEntry[] Shown { get; private set; } = [];
    internal LogRow[] Rows { get; private set; } = [];
    internal LogLevel Level { get; set; }
    internal string Search { get; set; } = "";
    internal DateOnly? Day { get; set; }
    internal bool Paused { get; set; }
    internal DateTime ClearedAt { get; private set; } = DateTime.MinValue;
    internal int MaxLines => Math.Clamp(Settings.MaxLines, 50, 20000);
    internal string CopyText => LogFilter.Text(Shown);

    public override void Initialize() => RegisterHotkey("Toggle", "Shows or hides the log panel", () => Settings.ShowPanel = !Settings.ShowPanel, new Hotkey(Key.F9));

    internal void Clear() => ClearedAt = DateTime.Now;

    internal void ToggleSource(string source)
    {
        if (!_sources.Remove(source)) _sources.Add(source);
        SourcesChanged();
    }

    internal void ClearSources()
    {
        _sources.Clear();
        SourcesChanged();
    }

    void SourcesChanged()
    {
        _sourceEpoch++;
        _sourcePreview = _sources.Count == 0 ? "All sources" : string.Join(", ", _sources.Order());
    }

    void Refresh()
    {
        if (!_levelSet)
        {
            Level = Settings.DefaultLevel;
            _levelSet = true;
        }
        var max = MaxLines;
        var rawKey = (Paused && _raw is not null ? _rawKey.Version : LogStore.Version, DateTime.Now.Date, Day, max);
        if (_raw is null || rawKey != _rawKey)
        {
            _rawKey = rawKey;
            try
            {
                _raw = Day is { } day ? LogStore.Read(day, max) : LogStore.Today(max);
                _sourceNames = _raw.Select(entry => entry.Source ?? "").Distinct().Order().ToArray();
            }
            catch (Exception)
            {
                _raw ??= [];
            }
            _loads++;
        }
        var shownKey = (_loads, Level, Search, ClearedAt, _sourceEpoch, max);
        if (FilterRuns > 0 && shownKey == _shownKey) return;
        _shownKey = shownKey;
        var cleared = ClearedAt;
        Shown = LogFilter.Apply(_raw.Where(entry => entry.Time > cleared).ToArray(), Level, _sources, Search);
        Rows = LogFilter.Rows(Shown, LogFilter.SourceWidth(Shown));
        FilterRuns++;
    }

    void RefreshDays()
    {
        if (!_daysThrottle.Due()) return;
        try
        {
            _days = LogStore.Days().OrderByDescending(day => day).ToArray();
        }
        catch (Exception)
        {
            return;
        }
        _dayLabels = _days.Select(day => day.ToString("yyyy-MM-dd")).Prepend("Today").ToArray();
        if (Day is { } current && !_days.Contains(current))
        {
            Day = null;
            ClearedAt = DateTime.MinValue;
        }
    }

    public override void Render()
    {
        if (!Settings.ShowPanel) return;
        Refresh();
        RefreshDays();

        _overlay.Locked = Settings.Lock;
        _overlay.MoveHeld = Settings.MoveKey.Key != Key.None && Input.IsDown(Settings.MoveKey.Key);
        var origin = new Vector2(Settings.PanelX, Settings.PanelY);
        var size = new Vector2(Settings.Width, Settings.Height);
        var (hovered, _, active) = _overlay.Handle("logging", ref origin, size, ref Settings.PanelX, ref Settings.PanelY, ref Settings.Width, 300, 4000, ref Settings.Height, 120, 2000, Overlay.ResizeEdge.Corner);
        size = new Vector2(Settings.Width, Settings.Height);
        if (hovered || active) Overlay.ClickBlocker("logging", origin, size);
        Draw.RectFilled(origin, origin + size, GameColors.LabelBackground, 4);
        if (_overlay.Movable) Overlay.DragHint(origin, origin + size, active, hovered, Overlay.ResizeEdge.Corner);

        ImGui.SetNextWindowPos(origin);
        ImGui.SetNextWindowSize(size);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (_overlay.Movable) flags |= ImGuiWindowFlags.NoInputs;
        var visible = ImGui.Begin("Log###logging", flags);
        try
        {
            if (!visible) return;
            FilterStrip();
            ImGui.Separator();
            List();
        }
        finally
        {
            ImGui.End();
        }
    }

    void FilterStrip()
    {
        var level = Level;
        if (Combo.Enum(ref level, "level", 80)) Level = level;
        ImGui.SameLine();
        SourceCombo();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        var search = Search;
        if (ImGui.InputTextWithHint("##search", "search", ref search, 256)) Search = search;
        ImGui.SameLine();
        DayCombo();
        ImGui.SameLine();
        var paused = Paused;
        if (ImGui.Checkbox("Pause", ref paused)) Paused = paused;
        ImGui.SameLine();
        if (ImGui.Button("Clear")) Clear();
        ImGui.SameLine();
        if (ImGui.Button("Copy")) ImGui.SetClipboardText(CopyText);
    }

    void SourceCombo()
    {
        ImGui.SetNextItemWidth(140);
        if (!ImGui.BeginCombo("##sources", _sourcePreview)) return;
        try
        {
            if (ImGui.Selectable("All", _sources.Count == 0, ImGuiSelectableFlags.NoAutoClosePopups)) ClearSources();
            foreach (var name in _sourceNames)
                if (ImGui.Selectable(name.Length == 0 ? "(none)" : name, _sources.Contains(name), ImGuiSelectableFlags.NoAutoClosePopups)) ToggleSource(name);
        }
        finally
        {
            ImGui.EndCombo();
        }
    }

    void DayCombo()
    {
        var index = Day is { } current ? Math.Max(0, Array.IndexOf(_dayLabels, current.ToString("yyyy-MM-dd"))) : 0;
        if (!Combo.Option(ref index, _dayLabels, "day", 110)) return;
        Day = index == 0 ? null : _days[index - 1];
        ClearedAt = DateTime.MinValue;
    }

    void List()
    {
        using var _ = Fonts.Use(FontFace.Mono);
        var sourceWidth = Rows.Length == 0 ? 0 : LogFilter.SourceWidth(Shown);
        var charWidth = ImGui.CalcTextSize("0").X;
        if (!ImGui.BeginTable("##lines", 4, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings, Vector2.Zero)) return;
        var clipper = ImGui.ImGuiListClipper();
        try
        {
            ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed, charWidth * 12 + 6);
            ImGui.TableSetupColumn("level", ImGuiTableColumnFlags.WidthFixed, charWidth * 5 + 6);
            ImGui.TableSetupColumn("source", ImGuiTableColumnFlags.WidthFixed, charWidth * Math.Max(1, sourceWidth) + 6);
            ImGui.TableSetupColumn("content", ImGuiTableColumnFlags.WidthStretch);
            var follow = !Paused && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4;
            clipper.Begin(Rows.Length);
            while (clipper.Step())
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) Row(Rows[i], charWidth);
            clipper.End();
            if (follow) ImGui.SetScrollHereY(1);
        }
        finally
        {
            clipper.Destroy();
            ImGui.EndTable();
        }
    }

    static void Row(in LogRow row, float charWidth)
    {
        ImGui.TableNextRow();
        ImGui.PushStyleColor(ImGuiCol.Text, EColor.ToVector4(LevelColour(row.Severity)));
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(row.Time);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(row.Level);
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(row.Source);
        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(row.Content);
        ImGui.PopStyleColor();
        if (!ImGui.IsItemHovered() || (row.Full.Length == row.Content.Length && row.Content.Length * charWidth <= ImGui.GetContentRegionAvail().X)) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(row.Full);
        ImGui.EndTooltip();
    }

    static System.Drawing.Color LevelColour(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => GameColors.TextDim,
        LogLevel.Warn => GameColors.Gold,
        LogLevel.Error => GameColors.Negative,
        _ => GameColors.Text,
    };
}
