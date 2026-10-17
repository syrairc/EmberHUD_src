using System;
using System.Linq;
using EmberHUD;

namespace Tartarus;

public readonly record struct ValueRange(double Low, double High);

public sealed record OptionAdvice(int OptionId, ValueRange Value, bool Glow, string Reason);

public sealed record Advice(OptionAdvice[] Options, string Summary);

public static class Advisor
{
    const int Chromatic = 5;

    public static Advice Advise(TartarusState state, Settings settings)
    {
        var choice = state.Choice;
        if (choice is null) return new Advice([], "unsure: no choice");
        if (choice.Kind == TartarusChoiceKind.Event) return new Advice([], "unsure: event");
        if (choice.Kind is TartarusChoiceKind.Instrument or TartarusChoiceKind.Reward or TartarusChoiceKind.Other)
            return new Advice([.. choice.Options.Select(o => new OptionAdvice(o.Id, default, false, "unmeasured"))], "unsure: unmeasured");

        var evaluated = choice.Options.Select(o => (Option: o, Result: Evaluate(state, o, settings))).ToArray();
        var damp = Damp(state, settings);
        var values = evaluated.Select(e => e.Result.Value is { } v ? v with { High = v.Low + (v.High - v.Low) * damp } : default).ToArray();
        var glow = new bool[values.Length];
        var summary = Verdict(state, choice, settings, evaluated.Select(e => e.Result.Value is null).ToArray(), values, glow);
        return new Advice([.. evaluated.Select((e, i) => new OptionAdvice(e.Option.Id, values[i], glow[i], e.Result.Reason))], summary);
    }

    internal static int PercentileTier(double[] prior, double percentile)
    {
        var cumulative = 0.0;
        for (var tier = 0; tier < prior.Length; tier++)
        {
            cumulative += prior[tier];
            if (cumulative >= percentile) return tier;
        }
        return prior.Length - 1;
    }

    static ValueRange? PriorRange(double[]? prior, Settings s) =>
        prior is { Length: > 0 } ? new ValueRange(Value(PercentileTier(prior, s.PriorLowPercentile), s), Value(PercentileTier(prior, s.PriorHighPercentile), s)) : null;

    static double Damp(TartarusState state, Settings settings)
    {
        var daysLeft = state.MaxDay - state.Day;
        return daysLeft >= settings.LateDays ? 1 : Math.Clamp((daysLeft + 1.0) / Math.Max(settings.LateDays + 1, 1), 0, 1);
    }

    static string Verdict(TartarusState state, TartarusChoice choice, Settings settings, bool[] unsure, ValueRange[] values, bool[] glow)
    {
        if (state.MaxDay != settings.StandardDays) return "unsure: unmeasured mode";
        if (!choice.Complete) return "unsure: choice not fully read";
        if (unsure.Any(u => u)) return "unsure: unknown tier";
        if (values.Length == 0) return "unsure: no options";

        var best = values.Max(v => v.Low);
        var inSet = values.Select(v => v.High + settings.CloseMargin * Math.Abs(best) >= best).ToArray();
        var wide = settings.UnsureWidthRatio * Math.Max(best, 1);
        if (values.Where((v, i) => inSet[i] && v.High - v.Low > wide).Any()) return "unsure: wide ranges";
        var count = inSet.Count(i => i);
        if (count == values.Length) return "no clear best";
        for (var i = 0; i < glow.Length; i++) glow[i] = inSet[i];
        return $"{count} best";
    }

    static (ValueRange? Value, string Reason) Evaluate(TartarusState state, TartarusOption option, Settings s)
    {
        var eater = Known(state, state.Position);
        return option.Kind switch
        {
            TartarusOptionKind.Door => Door(state, option, s),
            TartarusOptionKind.Consume => ConsumeCells(eater, Known(state, option.Cell), s),
            TartarusOptionKind.Replace => eater is { } e && PriorRange(s.ReplacePrior, s) is { } r
                ? (new ValueRange(r.Low - Value(e.Tier, s), r.High - Value(e.Tier, s)), "Replace: a random inmate")
                : (null, "unknown tier"),
            TartarusOptionKind.End or TartarusOptionKind.StopChain => (new ValueRange(0, 0), "nothing gained"),
            TartarusOptionKind.Bestow => Bestow(state, s),
            TartarusOptionKind.Instigate => (new ValueRange(0, Value(3, s)), "Instigate: an event"),
            _ => (null, "unmeasured"),
        };
    }

    static (ValueRange? Value, string Reason) Door(TartarusState state, TartarusOption option, Settings s)
    {
        if (option.Cell == 0)
        {
            var (bestow, _) = Bestow(state, s);
            return bestow is { } b ? (new ValueRange(0, Math.Max(b.High, Value(3, s))), "office: Bestow or Instigate") : (null, "unknown tier");
        }
        var cell = Cell(state, option.Cell);
        if (cell is null) return (null, "unknown cell");
        var c = cell.Value;
        if (c.EventMarker) return (new ValueRange(0, Value(3, s)), "event, outcome unmeasured");
        if (c.Open)
        {
            if (!c.DevourTarget) return (new ValueRange(0, 0), $"{Label(c)}: nothing to eat");
            if (c.Tier < 0) return (null, "unknown tier");
            var low = Consume(c.Tier, c.Tier, s);
            return (Chain(low, s), $"{Label(c)} can consume: {Signed(low)}");
        }
        if (c.Tier >= 0) return (new ValueRange(Value(c.Tier, s), Value(c.Tier, s) + Math.Max(Consume(c.Tier, c.Tier, s), 0)), $"locked, tier {c.Tier}");
        return PriorRange(s.DoorPrior, s) is { } prior ? (prior, "locked, unknown") : (null, "unknown tier");
    }

    static (ValueRange? Value, string Reason) ConsumeCells(TartarusCell? eater, TartarusCell? target, Settings s)
    {
        if (eater is not { } e || target is not { } t) return (null, "unknown tier");
        var low = Consume(e.Tier, t.Tier, s);
        return (Chain(low, s), $"{Label(e)} eats {Label(t)}: {Signed(low)}");
    }

    static ValueRange Chain(double low, Settings s) => new(low, low + s.ChainUpside * Math.Max(low, 0));

    static double Value(int tier, Settings s) => Math.Pow(s.TierBase, tier);

    static double Rise(int tier, Settings s) => tier + 1 == Chromatic ? Value(Chromatic, s) * s.RainbowMult : tier >= Chromatic ? Value(tier, s) : Value(tier + 1, s);

    static double Consume(int eater, int eaten, Settings s) => Rise(eater, s) - Value(eater, s) - Value(eaten, s);

    static (ValueRange? Value, string Reason) Bestow(TartarusState state, Settings s)
    {
        var open = state.Cells.Where(c => c.Index != 0 && c.Open).ToArray();
        if (open.Any(c => c.Tier is < 0 or > Chromatic)) return (null, "unknown tier");
        var lifts = open.Where(c => c.Tier < Chromatic).Select(c => Rise(c.Tier, s) - Value(c.Tier, s)).ToArray();
        if (lifts.Length == 0) return (new ValueRange(0, 0), "office: no inmate to lift");
        var range = new ValueRange(lifts.Min(), lifts.Max());
        return (range, $"office: Bestow lifts one inmate: {Signed(range.Low)} to {Signed(range.High)}");
    }

    static TartarusCell? Cell(TartarusState state, int index)
    {
        foreach (var cell in state.Cells)
            if (cell.Index == index) return cell;
        return null;
    }

    static TartarusCell? Known(TartarusState state, int index) => Cell(state, index) is { Open: true, Tier: >= 0 } cell ? cell : null;

    static string Label(TartarusCell cell) => $"{cell.Beast} T{cell.Tier}";

    static string Signed(double value) => value.ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture);
}
