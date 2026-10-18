using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberHUD.Testing;

namespace Tartarus.Sim;

sealed record OracleHidden(string Beast, int Tier);

sealed record OracleMarkers(string? Event, bool Seen, int Known, int AtLeast, int? Nest, bool Tusk, bool Scratch);

sealed record OraclePending(string Kind, int Cell);

sealed record OracleObjective(string Id, int Progress);

sealed record OraclePick(string Kind, string[]? Options, int Count);

sealed record OraclePrey(int Lion, int Prey, int Rounds, int Next);

sealed record OracleCell(int I, bool Open, string Beast, int Tier, OracleHidden? Hidden, OracleMarkers? Markers);

sealed record OracleUsed(int Swap, int Devour);

sealed record OracleBoard(int Position, int Day, int MaxDay, string Phase, int Chromatics, string[] Tools, OracleCell[] Cells, int[]? Destinations, OracleUsed? Used, int? Chaining, string? StartEvent, bool Chain, bool Ratway, string[]? EventPool, OraclePrey[]? Prey, OraclePending? PendingEvent, OracleObjective? Objective, bool Milestone, OraclePick[]? Pending);

sealed record OracleAction(string Kind, int Cell, int Param, string? Id, OracleAction[]? Then);

sealed record OracleResult(bool? Ok, int[]? Targets, int[]? ChainTargets);

sealed record OracleParams(double[]? BeastWeights, double[]? TierWeights, double[]? SwapWeights, bool? ChainStop, int? EventChance);

sealed record OracleScenario(string Name, OracleBoard Before, OracleBoard After, OracleAction Action, OracleResult? Result, OracleParams? Params, bool Random, string? Branch);

sealed record OracleFrequency(string Name, string Measure, OracleBoard? Before, OracleAction Action, int Trials, Dictionary<string, int> Outcomes, OracleParams? Params);

sealed record OracleFile(OracleScenario[] Scenarios, OracleFrequency[] Frequencies);

static class OracleData
{
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static OracleFile Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Plugins", "Tartarus", "Tests", "Oracle", name + ".json");
        return JsonSerializer.Deserialize<OracleFile>(File.ReadAllText(path), Options)!;
    }

    public static readonly Dictionary<string, EventKind> EventIds = new()
    {
        ["chain"] = EventKind.ChainReaction, ["execution"] = EventKind.Execution, ["trickster"] = EventKind.BehindClosedDoors, ["appetite"] = EventKind.RavenousAppetite,
        ["convict"] = EventKind.HighRiskConvict, ["crow"] = EventKind.CrowWhispers, ["freedom"] = EventKind.TasteOfFreedom, ["usurp1"] = EventKind.UsurpationI,
        ["usurp2"] = EventKind.UsurpationII, ["usurp3"] = EventKind.UsurpationIII, ["usurp4"] = EventKind.UsurpationIV, ["ratway"] = EventKind.SewerRatway,
        ["hunt"] = EventKind.TreasureHunt,
    };

    public static readonly Dictionary<string, ObjectiveKind> ObjectiveIds = new()
    {
        ["king"] = ObjectiveKind.King, ["walk"] = ObjectiveKind.Walk, ["consume"] = ObjectiveKind.Consume, ["incident"] = ObjectiveKind.Incident,
    };

    public static string IdOf(EventKind kind) => EventIds.First(pair => pair.Value == kind).Key;

    public static Beast ParseBeast(string name) => Enum.Parse<Beast>(name, true);

    public static Run ToRun(OracleBoard board, ulong seed, OracleParams? parameters = null)
    {
        var card = board.StartEvent switch { "cagefight" => StartCard.CageFight, "ironlaw" => StartCard.IronLaw, "tyranny" => StartCard.Tyranny, _ => StartCard.None };
        var run = new Run(new Rules { Days = board.MaxDay, EventChance = parameters?.EventChance ?? 0, StartTools = 0, Card = card }, new Rng(seed))
        {
            Position = board.Position,
            Day = board.Day,
            Chromatics = board.Chromatics,
            Phase = Enum.Parse<Phase>(board.Phase, true),
            Doors = board.Destinations ?? [],
            Consumed = board.Used?.Devour > 0,
            Replaced = board.Used?.Swap > 0,
            Chaining = board.Chaining ?? -1,
            ChainArmed = board.Chain,
            Ratway = board.Ratway,
            PendingEvent = board.PendingEvent is { } pending ? EventIds[pending.Kind] : null,
            EventCell = board.PendingEvent?.Cell ?? -1,
            Chance = new Chance(parameters?.BeastWeights, parameters?.TierWeights, parameters?.SwapWeights),
            Objective = board.Objective is { } objective ? new Objective(ObjectiveIds[objective.Id], objective.Progress, board.Milestone) : default,
        };
        run.Queue.AddRange(board.Pending?.Select(pick => pick.Kind == "gift"
            ? new Offer(MoveKind.Reward, [.. pick.Options!.Select(id => (int)Enum.Parse<RewardId>(id, true))])
            : new Offer(MoveKind.Instrument, [.. pick.Options!.Select(id => (int)Enum.Parse<ToolId>(id, true))])) ?? []);
        run.Held.AddRange(board.Tools.Select(tool => Enum.Parse<ToolId>(tool, true)));
        if (board.EventPool is { } pool) run.Pool = [.. pool.Select(id => EventIds[id])];
        run.Prey.AddRange(board.Prey?.Select(mark => new PreyMark(mark.Lion, mark.Prey, mark.Rounds, mark.Next)) ?? []);
        foreach (var cell in board.Cells)
        {
            var markers = cell.Markers;
            var known = markers?.Known > 0;
            var seen = markers?.Seen ?? false;
            var atLeast = markers?.AtLeast ?? 0;
            var flags = (markers?.Tusk == true ? Marker.Tusk : 0) | (markers?.Scratch == true ? Marker.Scratch : 0) | (markers?.Nest is not null ? Marker.Nest : 0);
            EventKind? waiting = markers?.Event is { } id ? EventIds[id] : null;
            run.Cells[cell.I] = cell.Open
                ? new Cell(true, ParseBeast(cell.Beast), cell.Tier, false, false, 0, flags, 0, waiting)
                : cell.Hidden is { } hidden
                    ? new Cell(false, ParseBeast(hidden.Beast), hidden.Tier, known, seen, atLeast, flags, markers?.Nest ?? 0, waiting)
                    : new Cell(false, Beast.Mammoth, -1, known, seen, atLeast, flags, markers?.Nest ?? 0, waiting);
        }
        return run;
    }

    public static string? Differences(Run run, OracleBoard expected)
    {
        if (run.Position != expected.Position) return $"position {run.Position}, expected {expected.Position}";
        if (run.Day != expected.Day) return $"day {run.Day}, expected {expected.Day}";
        if (!run.Phase.ToString().Equals(expected.Phase, StringComparison.OrdinalIgnoreCase)) return $"phase {run.Phase}, expected {expected.Phase}";
        if (run.Chromatics != expected.Chromatics) return $"chromatics {run.Chromatics}, expected {expected.Chromatics}";
        if (run.Consumed != expected.Used?.Devour > 0) return $"consumed {run.Consumed}";
        if (run.Replaced != expected.Used?.Swap > 0) return $"replaced {run.Replaced}";
        if (run.Chaining != (expected.Chaining ?? -1)) return $"chaining {run.Chaining}, expected {expected.Chaining}";
        if (run.ChainArmed != expected.Chain) return $"chain armed {run.ChainArmed}";
        if (run.Ratway != expected.Ratway) return $"ratway {run.Ratway}";
        if (!run.Tools.SequenceEqual(expected.Tools.Select(tool => Enum.Parse<ToolId>(tool, true)))) return $"tools {string.Join(',', run.Tools)}";
        var wantObjective = expected.Objective is { } goal ? new Objective(ObjectiveIds[goal.Id], goal.Progress, expected.Milestone) : default;
        if (run.Objective != wantObjective) return $"objective {run.Objective}, expected {wantObjective}";
        var wantPicks = expected.Pending ?? [];
        if (run.Offers.Count != wantPicks.Length) return $"{run.Offers.Count} picks waiting, expected {wantPicks.Length}";
        for (var i = 0; i < wantPicks.Length; i++)
        {
            var offer = run.Offers[i];
            var want = wantPicks[i];
            if (offer.Kind != (want.Kind == "gift" ? MoveKind.Reward : MoveKind.Instrument)) return $"pick {i} is {offer.Kind}";
            var names = want.Options?.Select(name => want.Kind == "gift" ? (int)Enum.Parse<RewardId>(name, true) : (int)Enum.Parse<ToolId>(name, true));
            if (names is null ? offer.Options.Length != want.Count || offer.Options.Distinct().Count() != offer.Options.Length : !offer.Options.Order().SequenceEqual(names.Order())) return $"pick {i} offers {string.Join(',', offer.Options)}";
        }
        if (expected.Destinations is { } doors && !run.Doors.Order().SequenceEqual(doors.Order())) return $"doors [{string.Join(',', run.Doors)}], expected [{string.Join(',', doors)}]";
        if (run.PendingEvent != (expected.PendingEvent is { } pending ? EventIds[pending.Kind] : null) || run.EventCell != (expected.PendingEvent?.Cell ?? -1)) return $"pending event {run.PendingEvent} at {run.EventCell}";
        if (!run.EventPool.SequenceEqual((expected.EventPool ?? DefaultPool).Select(id => EventIds[id]))) return $"event pool {string.Join(',', run.EventPool)}";
        var wantPrey = expected.Prey?.Select(mark => new PreyMark(mark.Lion, mark.Prey, mark.Rounds, mark.Next)) ?? [];
        if (!run.PreyMarks.SequenceEqual(wantPrey)) return $"prey marks {string.Join(',', run.PreyMarks)}";
        foreach (var want in expected.Cells)
        {
            var got = run.Cells[want.I];
            if (got.Open != want.Open) return $"cell {want.I} open {got.Open}, expected {want.Open}";
            var marker = MarkerDifference(got, want);
            if (marker is not null) return $"cell {want.I} {marker}";
            var (beast, tier) = want.Open ? (ParseBeast(want.Beast), want.Tier) : want.Hidden is { } hidden ? (ParseBeast(hidden.Beast), hidden.Tier) : (got.Beast, -1);
            if (got.Tier != tier || got.Tier >= 0 && got.Beast != beast) return $"cell {want.I} holds {got.Beast} {got.Tier}, expected {beast} {tier}";
            if (want.Open) continue;
            if (got.Seen != (want.Markers?.Seen ?? false)) return $"cell {want.I} seen {got.Seen}";
            if (got.Known != (want.Markers?.Known > 0)) return $"cell {want.I} known {got.Known}";
            if (got.AtLeast != (want.Markers?.AtLeast ?? 0)) return $"cell {want.I} at least {got.AtLeast}";
        }
        return null;
    }

    static readonly string[] DefaultPool = ["chain", "execution", "trickster", "appetite", "convict", "crow", "freedom", "usurp1", "ratway", "hunt"];

    static string? MarkerDifference(Cell got, OracleCell want)
    {
        var markers = want.Markers;
        if (got.Markers.HasFlag(Marker.Tusk) != (markers?.Tusk ?? false)) return $"tusk {got.Markers}";
        if (got.Markers.HasFlag(Marker.Scratch) != (markers?.Scratch ?? false)) return $"scratch {got.Markers}";
        if ((got.Markers.HasFlag(Marker.Nest) ? got.NestDay : null) != markers?.Nest) return $"nest {got.Markers} {got.NestDay}";
        var waiting = markers?.Event;
        var matches = waiting == "any" ? got.Event is not null : got.Event == (waiting is null ? null : EventIds[waiting]);
        return matches ? null : $"event {got.Event}, expected {waiting}";
    }

    public static Move MoveOf(OracleAction action) => action.Kind switch
    {
        "patrol" => new Move(MoveKind.Door, action.Cell),
        "consume" => new Move(MoveKind.Consume, action.Cell, Math.Max(0, action.Param)),
        "chain" => new Move(MoveKind.Consume, action.Cell),
        "stopChain" => new Move(MoveKind.StopChain),
        "swap" => new Move(MoveKind.Replace),
        "office" => new Move(action.Id == "instigate" ? MoveKind.Instigate : MoveKind.Bestow),
        "event" => new Move(action.Param == 1 ? MoveKind.Confirm : MoveKind.Skip),
        "choose" when Enum.TryParse<RewardId>(action.Id, true, out var reward) => new Move(MoveKind.Reward, Param: (int)reward),
        "choose" => new Move(MoveKind.Instrument, Param: (int)Enum.Parse<ToolId>(action.Id!, true)),
        _ => new Move(MoveKind.End),
    };

    public static void CheckFrequency(OracleFrequency frequency, int index, Func<Run, IEnumerable<string>> outcomes) =>
        CheckCounts(frequency, trial =>
        {
            var run = ToRun(frequency.Before!, (ulong)trial * 2654435761UL + (ulong)index, frequency.Params);
            run.Apply(MoveOf(frequency.Action));
            foreach (var step in frequency.Action.Then ?? []) run.Apply(MoveOf(step));
            return outcomes(run);
        });

    public static void CheckCounts(OracleFrequency frequency, Func<int, IEnumerable<string>> outcomes)
    {
        var seen = new Dictionary<string, int>();
        for (var trial = 0; trial < frequency.Trials; trial++)
            foreach (var key in outcomes(trial))
                seen[key] = seen.GetValueOrDefault(key) + 1;
        foreach (var key in seen.Keys.Union(frequency.Outcomes.Keys))
        {
            double recorded = frequency.Outcomes.GetValueOrDefault(key), ours = seen.GetValueOrDefault(key), n = frequency.Trials;
            var share = recorded / n;
            var tolerance = 4 * Math.Sqrt(share * (1 - share) / n) + 1 / n;
            Check.That(Math.Abs(ours / n - share) <= tolerance, $"{frequency.Name} {key}: ours {ours / n:F4}, recorded {share:F4}, tolerance {tolerance:F4}");
        }
    }

    public static string? Replay(OracleScenario scenario, ulong seed)
    {
        var run = ToRun(scenario.Before, seed, scenario.Params);
        var steps = new[] { scenario.Action }.Concat(scenario.Action.Then ?? []).Where(step => step.Kind != "listConsume").ToArray();
        for (var i = 0; i < steps.Length; i++)
        {
            var move = MoveOf(steps[i]);
            if (i < steps.Length - 1 || scenario.Result?.Ok != false)
            {
                run.Apply(move);
                continue;
            }
            try
            {
                run.Apply(move);
                return "an illegal move was allowed";
            }
            catch (ArgumentException)
            {
            }
        }
        var difference = Differences(run, scenario.After);
        var meals = run.Legal().Where(move => move.Kind == MoveKind.Consume).Select(move => move.Cell).Order().ToArray();
        if (difference is null && scenario.Result?.Targets is { } targets && !targets.SequenceEqual(meals)) return $"meals [{string.Join(',', meals)}], expected [{string.Join(',', targets)}]";
        if (difference is null && scenario.Result?.ChainTargets is { } chain && !chain.SequenceEqual(meals)) return $"chain meals [{string.Join(',', meals)}], expected [{string.Join(',', chain)}]";
        return difference;
    }
}
