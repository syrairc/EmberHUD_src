using System;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Tartarus;

public sealed class Main : Plugin<Settings>
{
    readonly TextStyle _style = TextStyle.Hud();
    TartarusState? _state;

    public Advice? Advice { get; private set; }

    public override void Tick()
    {
        Game.WantTartarus();
        var state = Game.State.Tartarus;
        _state = state;
        Advice = state?.Choice is { Kind: not TartarusChoiceKind.Event } ? Advisor.Advise(state, Settings) : null;
    }

    public override void Render()
    {
        if (!Settings.ShowPanel || Advice is not { } advice || _state is not { Choice: { } choice } state) return;
        var unsure = advice.Summary.StartsWith("unsure");
        var rows = new (string Left, string Right, bool Glow)[choice.Options.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var option = choice.Options[i];
            var entry = advice.Options[i];
            rows[i] = (Names.Option(option, state), entry.Reason, !unsure && entry.Glow);
        }

        const float Pad = 8;
        const float Gap = 24;
        var width = 0f;
        foreach (var (left, right, _) in rows) width = MathF.Max(width, Draw.MeasureText(left, _style).X + Gap + Draw.MeasureText(right, _style).X);
        width = MathF.Max(width, Draw.MeasureText(advice.Summary, _style).X);
        var rowHeight = Draw.RowHeight(_style);
        var origin = ImGui.GetIO().DisplaySize * new Vector2(Settings.PanelX, Settings.PanelY);
        var size = new Vector2(width + Pad * 2, rowHeight * (rows.Length + 1) + Pad * 2);
        Draw.RectFilled(origin, origin + size, GameColors.LabelBackground, 4);
        var y = origin.Y + Pad;
        foreach (var (left, right, glow) in rows)
        {
            var at = new Vector2(origin.X + Pad, y);
            if (glow) Draw.GlowRect(at - new Vector2(3, 1), at + new Vector2(width + 3, rowHeight - 1), GameColors.Positive);
            Draw.Row(at, width, left, right, _style);
            y += rowHeight;
        }
        var footer = _style.Clone();
        footer.Color = unsure ? GameColors.Gold : GameColors.TextDim;
        Draw.Text(advice.Summary, new Vector2(origin.X + Pad, y), footer);
    }
}
