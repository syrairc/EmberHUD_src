using System;
using System.Collections.Generic;
using System.Linq;

namespace Tartarus.Sim;

public static class Strategy
{
    const double TierBase = 3.3;
    const double ChromaticWorth = 400;
    const double OpenWeight = 1.2;
    const double NowWeight = 1.12;
    const int ReplaceMax = 1;
    const double WalkWeight = 1;
    const double StepWeight = 0.5;
    const double InstigateWeight = 1;
    const int ChainDepth = 4;
    const double RingWeight = 0.7;
    const double HornWeight = 0.7;
    const double WineWeight = 1.96;

    static readonly Beast[] AllBeasts = Enum.GetValues<Beast>();

    static readonly ToolId[] ToolRank =
    [
        ToolId.Crown, ToolId.Coin, ToolId.Maul, ToolId.Rope, ToolId.Maiden, ToolId.Keys, ToolId.Revenge, ToolId.Skewer, ToolId.Muzzle, ToolId.Ravenous,
        ToolId.Shackles, ToolId.Blacklist, ToolId.Saw, ToolId.Hook, ToolId.Guillotine, ToolId.Stake, ToolId.ShortChain, ToolId.Collar, ToolId.DogTag, ToolId.Axe,
        ToolId.Whip, ToolId.Fire, ToolId.Cage, ToolId.Forged, ToolId.Tongs, ToolId.Scales, ToolId.Pestle, ToolId.IronBox, ToolId.Scapegoat,
    ];

    public static Move Choose(Run run, Rng rng)
    {
        var legal = run.Legal();
        if (legal.Count == 0) return new Move(MoveKind.End);
        if (legal.Count == 1) return legal[0];
        return legal[0].Kind switch
        {
            MoveKind.Instrument => legal.MinBy(move => Array.IndexOf(ToolRank, (ToolId)move.Param)),
            MoveKind.Reward => legal.MaxBy(move => RewardGain(run, (RewardId)move.Param)),
            MoveKind.Confirm => Decide(run),
            MoveKind.Door => legal.MaxBy(move => DoorWorth(run, move.Cell) + WalkBonus(run, move.Cell)),
            MoveKind.Instigate => Office(run),
            _ => Manage(run, legal),
        };
    }

    static double Worth(int tier) => tier switch { < 0 => 0, >= Tiers.Chromatic => ChromaticWorth, _ => Math.Pow(TierBase, tier) };

    static Move Decide(Run run)
    {
        var accept = run.PendingEvent switch
        {
            EventKind.Execution => ExecutionGain(run) > 0,
            EventKind.BehindClosedDoors => run.EventCell < 0 || !run.Cells[run.EventCell].Open || run.Cells[run.EventCell].Tier < Tiers.Ultimate,
            _ => true,
        };
        return new Move(accept ? MoveKind.Confirm : MoveKind.Skip);
    }

    static double ExecutionGain(Run run)
    {
        if (run.EventCell < 0 || !run.Cells[run.EventCell].Open) return 0;
        var executioner = run.Cells[run.EventCell];
        var victims = Enumerable.Range(0, run.Cells.Length)
            .Where(other => other != run.EventCell && run.Cells[other].Open && run.Cells[other].Tier < executioner.Tier && ((int)run.Cells[other].Beast - (int)executioner.Beast + 4) % 4 == 1)
            .ToList();
        if (victims.Count == 0) return 0;
        return Worth(Math.Min(executioner.Tier + victims.Count, Tiers.Chromatic)) - Worth(executioner.Tier) - victims.Sum(victim => Worth(run.Cells[victim].Tier));
    }

    static List<Cell> Inmates(Run run) => [.. run.Cells.Where(cell => cell.Open && cell.Tier < Tiers.Chromatic)];

    static double Lift(Run run, int tiers) => Inmates(run) is { Count: > 0 } inmates ? inmates.Average(cell => Worth(cell.Tier + tiers) - Worth(cell.Tier)) : 0;

    static readonly double OpenValue = Odds.DoorTierWeights.Select((weight, tier) => weight * Worth(tier)).Sum() / Odds.DoorTierWeights.Sum();

    static double EventValue(Run run, EventKind kind)
    {
        var inmates = Inmates(run);
        return kind switch
        {
            EventKind.ChainReaction => 3 * OpenValue,
            EventKind.Execution => 0.5 * Worth(Tiers.Rare),
            EventKind.BehindClosedDoors => Worth(Tiers.Ultimate) - OpenValue,
            EventKind.RavenousAppetite => inmates.Count == 0 ? 0 : Enumerable.Range(0, 6).Sum(tries => FiveCoins[tries] * Worth(Math.Min(inmates.Min(cell => cell.Tier) + tries, Tiers.Chromatic))) - Worth(inmates.Min(cell => cell.Tier)),
            EventKind.HighRiskConvict => (Odds.DoorTierWeights[3] * Worth(3) + Odds.DoorTierWeights[4] * Worth(4)) / (Odds.DoorTierWeights[3] + Odds.DoorTierWeights[4]),
            EventKind.CrowWhispers => 0.5 * OpenValue,
            EventKind.TasteOfFreedom => 3 * OpenValue,
            EventKind.UsurpationI => Lift(run, 1),
            EventKind.UsurpationII => 2 * Lift(run, 1),
            EventKind.UsurpationIII => 2 * Lift(run, 2),
            EventKind.UsurpationIV => inmates.Count * Lift(run, 2),
            _ => OpenValue,
        };
    }

    static readonly double[] FiveCoins = [1 / 32.0, 5 / 32.0, 10 / 32.0, 10 / 32.0, 5 / 32.0, 1 / 32.0];

    static double AnyEventValue(Run run) => run.EventPool.Average(kind => EventValue(run, kind));

    static double InstigateValue(Run run) => 2 * AnyEventValue(run) * Math.Min(1, (run.MaxDay - run.Day) / 5.0) * InstigateWeight;

    static double BestowValue(Run run) => Lift(run, 1);

    static Move Office(Run run) => new(BestowValue(run) >= InstigateValue(run) ? MoveKind.Bestow : MoveKind.Instigate);

    static Move Manage(Run run, IReadOnlyList<Move> legal)
    {
        var eater = run.Chaining >= 0 ? run.Chaining : run.Position;
        var meals = legal.Where(move => move.Kind == MoveKind.Consume).Select(move => (move, gain: ConsumeGain(run, eater, move.Cell))).ToList();
        var best = meals.Count == 0 ? (move: default(Move), gain: double.NegativeInfinity) : meals.MaxBy(meal => meal.gain);
        var replaceable = legal.Any(move => move.Kind == MoveKind.Replace);
        var replace = replaceable ? ReplaceGain(run, run.Cells[run.Position]) : double.NegativeInfinity;
        if (run.Chaining >= 0) return best.gain > 0 ? best.move : new Move(MoveKind.StopChain);
        if (best.gain >= 0 && best.gain >= replace) return best.move;
        return replace > 0 ? new Move(MoveKind.Replace) : new Move(MoveKind.End);
    }

    static double ReplaceGain(Run run, Cell old)
    {
        if (!old.Open) return double.NegativeInfinity;
        var weights = Odds.ReplaceTierWeights;
        var total = weights.Sum();
        var expected = weights.Select((weight, tier) => weight / total * Worth(tier)).Sum();
        return old.Tier <= ReplaceMax ? Math.Max(expected - Worth(old.Tier), 0.01) : expected - Worth(old.Tier);
    }

    static double ConsumeGain(Run run, int eaterCell, int mealCell) => Bite(run, run.Cells[eaterCell], eaterCell, mealCell, 0UL, ChainDepth);

    static double Bite(Run run, Cell eater, int eaterCell, int mealCell, ulong eaten, int depth)
    {
        var meal = run.Cells[mealCell];
        var open = meal.Open;
        var gain = open ? 1 : 3;
        if (run.Holds(ToolId.Maul) && eater.Beast == Beast.Mammoth) gain = Math.Max(1, meal.Tier + 2 - eater.Tier);
        if (run.Holds(ToolId.Crown) && eater.Beast == Beast.Lion) gain += 2;
        if (run.Holds(ToolId.Skewer) && eater.Beast == Beast.Rat) gain++;
        if (run.Holds(ToolId.Muzzle) && run.Cells.Count(cell => cell.Open) - (open ? 1 : 0) + (run.Cells[eaterCell].Open ? 0 : 1) <= 4) gain += 2;
        var tier = Math.Min(eater.Tier + gain, Tiers.Chromatic);
        var worth = Worth(tier) - Worth(eater.Tier) - (open ? Worth(meal.Tier) : 0);
        if (tier >= Tiers.Chromatic || depth == 0) return worth;
        eaten |= 1UL << mealCell;
        var keeps = run.Rules.EaterKeepsBeast || run.Rules.Card == StartCard.CageFight;
        var beasts = keeps ? [eater.Beast] : AllBeasts;
        var follow = 0.0;
        foreach (var beast in beasts)
        {
            var next = new Cell(true, beast, tier, false);
            var best = 0.0;
            for (var other = 0; other < run.Cells.Length; other++)
                if (other != eaterCell && (eaten >> other & 1) == 0 && run.Eatable(next, other))
                    best = Math.Max(best, Bite(run, next, eaterCell, other, eaten, depth - 1));
            follow += best / beasts.Length;
        }
        return worth + follow;
    }

    static double RewardGain(Run run, RewardId reward)
    {
        var inmates = Enumerable.Range(0, run.Cells.Length).Where(cell => run.Cells[cell].Open).ToList();
        switch (reward)
        {
            case RewardId.Wine:
                return WineWeight * inmates.Where(cell => run.Cells[cell].Tier < Tiers.Legendary).Sum(cell => Worth(run.Cells[cell].Tier + 1) - Worth(run.Cells[cell].Tier));
            case RewardId.Ring:
                return RingWeight * Math.Min(3, run.Cells.Count(cell => !cell.Open)) * OpenValue;
            case RewardId.Horn:
                return HornWeight * inmates.Sum(cell => run.Meals(cell).Where(meal => run.Cells[meal].Open).Select(meal => Bite(run, run.Cells[cell], cell, meal, 0UL, 1)).DefaultIfEmpty(0).Average());
            default:
                return 0;
        }
    }

    static double WalkBonus(Run run, int door)
    {
        if (run.Ratway) return 0;
        var steps = (door - run.Position + run.Cells.Length) % run.Cells.Length;
        return StepWeight * steps + (run.Objective is { Kind: ObjectiveKind.Walk, Paid: false } ? WalkWeight * steps : 0);
    }

    static double DoorWorth(Run run, int door)
    {
        if (door == 0) return Math.Max(BestowValue(run), InstigateValue(run));
        var cell = run.Cells[door];
        if (cell.Open) return Math.Max(0, Math.Max(BestMeal(run, door), ReplaceGain(run, cell))) + (cell.Event is { } waiting ? EventValue(run, waiting) : 0);
        if (cell.Markers.HasFlag(Marker.Nest)) return -Worth(2);
        var tiers = TierOdds(cell);
        var worth = 0.0;
        for (var tier = 0; tier < tiers.Length; tier++)
        {
            if (tiers[tier] <= 0) continue;
            var beasts = cell.Known && cell.Tier >= 0 ? [cell.Beast] : AllBeasts;
            foreach (var beast in beasts)
            {
                var now = Math.Max(0, Enumerable.Range(0, run.Cells.Length)
                    .Where(other => other != door && run.Eatable(new Cell(true, beast, tier, false), other))
                    .Select(other => Bite(run, new Cell(true, beast, tier, false), door, other, 0UL, ChainDepth)).DefaultIfEmpty(0).Max());
                worth += tiers[tier] / beasts.Length * (OpenWeight * Worth(tier) + NowWeight * now);
            }
        }
        return worth + (cell.Seen && cell.Event is { } shown ? EventValue(run, shown) : run.Rules.EventChance / 100.0 * AnyEventValue(run));
    }

    static double BestMeal(Run run, int cell) => run.Meals(cell).Select(meal => ConsumeGain(run, cell, meal)).DefaultIfEmpty(0).Max();

    static double[] TierOdds(Cell cell)
    {
        if (cell.Tier >= 0 && (cell.Known || cell.Markers.HasFlag(Marker.Scratch)))
        {
            var known = new double[Tiers.Chromatic];
            known[Math.Min(cell.Tier, known.Length - 1)] = 1;
            return known;
        }
        var floor = Math.Min(cell.AtLeast, Odds.DoorTierWeights.Length - 1);
        var weights = Odds.DoorTierWeights.Select((weight, tier) => tier < floor ? 0 : weight).ToArray();
        var total = weights.Sum();
        if (total <= 0) weights[floor] = total = 1;
        return [.. weights.Select(weight => weight / total)];
    }
}
