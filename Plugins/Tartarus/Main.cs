using System;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;
using Tartarus.Sim;

namespace Tartarus;

public sealed class Main : Plugin<Settings>
{
    readonly TextStyle _style = TextStyle.Hud();
    readonly SolverWorker _worker = new();
    TartarusState? _state;
    RunTracker? _tracker;
    MapResult? _map;
    int _boardKey;
    int _generation;
    PracticeSession? _practice;

    internal InmateLogger Logger { get; } = new();

    public Advice? Advice { get; private set; }

    public override void Tick()
    {
        Game.WantTartarus();
        var state = Game.State.Tartarus;
        _state = state;
        _tracker ??= new RunTracker(Logger.DataDirectory, Logger.Seconds);
        var tracked = _tracker.Update(state);
        Advice = state?.Choice is { Kind: not TartarusChoiceKind.Event } ? Advise(state, tracked) : null;
        Logger.Tick();
    }

    Advice Advise(TartarusState state, TrackedRun tracked)
    {
        var rules = Settings.ToRules();
        var key = BoardKey(state, tracked, rules);
        if (_map is null || key != _boardKey)
        {
            _map = StateMapper.Map(state, tracked, rules);
            _boardKey = key;
            _generation++;
            _worker.Playouts = Settings.ClampedPlayouts;
            if (_map.Run is { } run) _worker.Submit(run, _generation);
        }
        var (ranked, generation) = _worker.Latest;
        return SimAdvisor.Advise(state, _map, generation == _generation ? ranked : null, Settings);
    }

    int BoardKey(TartarusState state, TrackedRun tracked, Rules rules)
    {
        var key = new HashCode();
        key.Add(rules);
        key.Add(Settings.ClampedPlayouts);
        key.Add(tracked.SwapUsedToday);
        key.Add(state.Position);
        key.Add(state.Size);
        key.Add(state.Day);
        key.Add(state.MaxDay);
        key.Add(state.Chromatics);
        key.Add(state.Choice?.Id);
        key.Add(state.Choice?.Kind);
        key.Add(state.Choice?.Complete);
        foreach (var cell in state.Cells ?? []) key.Add(cell);
        foreach (var instrument in state.Instruments ?? []) key.Add(instrument);
        foreach (var option in state.Choice?.Options ?? []) key.Add(option);
        return key.ToHashCode();
    }

    void DrawPractice()
    {
        var rules = Settings.ToRules() with { Days = 30 };
        if (_practice is null || _practice.Rules != rules)
        {
            _practice?.Dispose();
            _practice = new PracticeSession(rules, (ulong)Random.Shared.NextInt64());
        }
        _practice.Playouts = Settings.ClampedPlayouts;
        PracticeUI.Draw(_practice);
    }

    public override void Dispose()
    {
        _practice?.Dispose();
        _worker.Dispose();
        _tracker?.Dispose();
        Logger.Dispose();
        base.Dispose();
    }

    public override void DrawSettings() => Layout.TabBar("tartarus",
        ("Panel", () => SettingsUI.DrawPanel(Settings)),
        ("Advice", () => SettingsUI.DrawAdvice(Settings)),
        ("Rules", () => SettingsUI.DrawRules(Settings)),
        ("Practice", DrawPractice),
        ("Inmate log", Logger.DrawPage),
        ("Minigame log", Logger.DrawMinigamePage));

    readonly Overlay _overlay = new();
    int _unusedWidth, _unusedHeight;

    void DrawCards(TartarusOption[] options, OptionAdvice[] advice)
    {
        var origin = new Vector2(Settings.CardsX, Settings.CardsY);
        var size = new Vector2(Settings.CardsWidth, Settings.CardsHeight);
        var (hovered, edge, active) = _overlay.Handle("tartarus-cards", ref origin, size, ref Settings.CardsX, ref Settings.CardsY, ref Settings.CardsWidth, 300, 4000, ref Settings.CardsHeight, 60, 800);
        if (hovered || active) Overlay.ClickBlocker("tartarus-cards", origin, size);
        size = new Vector2(Settings.CardsWidth, Settings.CardsHeight);
        if (_overlay.Movable) Overlay.DragHint(origin, origin + size, active, hovered, edge);
        const float Gap = 16;
        var card = new Vector2((size.X - Gap * 2) / 3, size.Y);
        for (var i = 0; i < options.Length; i++)
        {
            var slot = options[i].Kind switch { TartarusOptionKind.Consume => 0, TartarusOptionKind.Replace => 1, TartarusOptionKind.End => 2, _ => -1 };
            if (slot < 0 || !advice[i].Glow) continue;
            var min = origin + new Vector2((card.X + Gap) * slot, 0);
            Draw.GlowRect(min, min + card, GameColors.Positive);
        }
    }

    public override void Render()
    {
        if (!Settings.ShowPanel || Advice is not { } advice || _state is not { Cells: not null, Choice: { Options: not null } choice } state || advice.Options.Length != choice.Options.Length) return;
        var summary = advice.Summary.Split('\n');
        var rows = new (string Left, string Right, bool Glow)[choice.Options.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var option = choice.Options[i];
            var entry = advice.Options[i];
            rows[i] = (Names.Option(option, state, Settings.ReplaceIsExecute), entry.Reason, entry.Glow);
        }

        var cards = Settings.HighlightCards && choice.Kind == TartarusChoiceKind.Operation;
        const float Pad = 8;
        const float Gap = 24;
        var width = 0f;
        foreach (var (left, right, _) in rows) width = MathF.Max(width, Draw.MeasureText(left, _style).X + Gap + Draw.MeasureText(right, _style).X);
        foreach (var line in summary) width = MathF.Max(width, Draw.MeasureText(line, _style).X);
        var rowHeight = Draw.RowHeight(_style);
        _overlay.Locked = Settings.Lock;
        _overlay.MoveHeld = Settings.MoveKey.Key != Key.None && Input.IsDown(Settings.MoveKey.Key);
        var origin = new Vector2(Settings.PanelX, Settings.PanelY);
        var size = new Vector2(width + Pad * 2, rowHeight * (rows.Length + summary.Length) + Pad * 2);
        var (hovered, _, active) = _overlay.Handle("tartarus", ref origin, size, ref Settings.PanelX, ref Settings.PanelY, ref _unusedWidth, 60, 4000, ref _unusedHeight, 0, 0, Overlay.ResizeEdge.None);
        if (hovered || active) Overlay.ClickBlocker("tartarus", origin, size);
        Draw.RectFilled(origin, origin + size, GameColors.LabelBackground, 4);
        if (_overlay.Movable) Overlay.DragHint(origin, origin + size, active, hovered, Overlay.ResizeEdge.None);
        if (cards) DrawCards(choice.Options, advice.Options);
        var y = origin.Y + Pad;
        foreach (var (left, right, glow) in rows)
        {
            var at = new Vector2(origin.X + Pad, y);
            if (glow && !cards) Draw.GlowRect(at - new Vector2(3, 1), at + new Vector2(width + 3, rowHeight - 1), GameColors.Positive);
            Draw.Row(at, width, left, right, _style);
            y += rowHeight;
        }
        var footer = _style.Clone();
        for (var i = 0; i < summary.Length; i++)
        {
            var warn = i < summary.Length - 1 || summary[i].StartsWith("mismatch") || summary[i].StartsWith("unsure");
            footer.Color = warn ? GameColors.Gold : GameColors.TextDim;
            Draw.Text(summary[i], new Vector2(origin.X + Pad, y), footer);
            y += rowHeight;
        }
    }
}
