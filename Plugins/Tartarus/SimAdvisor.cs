using System;
using System.Globalization;
using System.Linq;
using EmberHUD;
using Tartarus.Sim;

namespace Tartarus;

public readonly record struct ValueRange(double Low, double High);

public sealed record OptionAdvice(int OptionId, ValueRange Value, bool Glow, string Reason, bool Top = false);

public sealed record Advice(OptionAdvice[] Options, string Summary);

public static class SimAdvisor
{
    public static Advice Advise(TartarusState state, MapResult map, Ranked[]? ranked, Settings settings)
    {
        if (state?.Choice is not { Options: { } options } || map is null) return new Advice([], "");
        var found = options.Select(option => map.Run is null ? null : ranked?.FirstOrDefault(entry => Matches(entry.Move, option))).ToArray();
        var glow = new bool[options.Length];
        var core = Core(map, ranked, found, glow, settings);
        var top = Enumerable.Range(0, options.Length).Where(i => glow[i]).OrderByDescending(i => found[i]!.Value).Cast<int?>().FirstOrDefault();
        var advice = options.Select((option, i) => found[i] is { } entry
            ? new OptionAdvice(option.Id, new ValueRange(entry.Value - entry.Spread, entry.Value + entry.Spread), glow[i], entry.Value.ToString("0.00", CultureInfo.InvariantCulture), top == i)
            : new OptionAdvice(option.Id, default, false, "")).ToArray();
        var notes = settings.ShowAssumed && map.Assumed.Length > 0 ? $"assumed: {string.Join('\n', map.Assumed)}\n" : "";
        return new Advice(advice, notes + core);
    }

    static string Core(MapResult map, Ranked[]? ranked, Ranked?[] found, bool[] glow, Settings settings)
    {
        if (map.Mismatch is not null) return $"mismatch: {map.Mismatch}";
        if (map.Unsupported is not null) return map.Unsupported;
        if (map.Run is null || ranked is null) return "thinking";
        if (found.Length == 0 || found.Any(entry => entry is null)) return "unsure: not every option is simulated";

        if (found.Length == 1) return "only option";

        var best = found.Max(entry => entry!.Value - entry.Spread);
        var close = found.Select(entry => entry!.Value + entry.Spread + settings.CloseMargin * Math.Abs(best) >= best).ToArray();
        var count = close.Count(inSet => inSet);
        if (count == found.Length && count > 1) return "no clear best";
        Array.Copy(close, glow, glow.Length);
        return $"{count} best";
    }

    static bool Matches(Move move, TartarusOption option) => option.Kind switch
    {
        TartarusOptionKind.Door => move.Kind == MoveKind.Door && move.Cell == option.Cell,
        TartarusOptionKind.Consume => move.Kind == MoveKind.Consume && move.Cell == option.Cell,
        TartarusOptionKind.Replace => move.Kind == MoveKind.Replace,
        TartarusOptionKind.End => move.Kind == MoveKind.End,
        TartarusOptionKind.StopChain => move.Kind == MoveKind.StopChain,
        TartarusOptionKind.Instigate => move.Kind == MoveKind.Instigate,
        TartarusOptionKind.Bestow => move.Kind == MoveKind.Bestow,
        TartarusOptionKind.Instrument => move.Kind == MoveKind.Instrument && StateMapper.ToolOf(option.Param) is { } tool && move.Param == (int)tool,
        TartarusOptionKind.Reward => move.Kind == MoveKind.Reward && move.Param == option.Param - 1,
        _ => false,
    };
}
