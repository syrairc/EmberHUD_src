using System;
using System.Collections.Generic;
using System.Linq;

namespace Tartarus.Sim;

public sealed partial class Run
{
    public Objective Objective { get; internal set; }

    public IReadOnlyList<Offer> Offers => Queue;

    internal List<Offer> Queue { get; set; } = [];

    static int Target(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Walk => 18,
        ObjectiveKind.Consume => 4,
        ObjectiveKind.Incident => 2,
        _ => int.MaxValue,
    };

    void Progress(ObjectiveKind kind, int amount)
    {
        if (Objective.Kind != kind || Objective.Paid || amount <= 0) return;
        var progress = Math.Min(Objective.Progress + amount, Target(kind));
        Objective = Objective with { Progress = progress };
        if (progress >= Target(kind)) Pay();
    }

    void Reach()
    {
        if (Objective.Kind == ObjectiveKind.King && !Objective.Paid) Pay();
    }

    void Pay()
    {
        Objective = Objective with { Paid = true };
        OfferTools(true);
    }

    void OfferTools(bool first)
    {
        var options = Sample([.. Enum.GetValues<ToolId>().Where(tool => !Holds(tool)).Select(tool => (int)tool)], 3);
        if (options.Count == 0) return;
        var offer = new Offer(MoveKind.Instrument, [.. options]);
        if (first) Queue.Insert(0, offer);
        else Queue.Add(offer);
    }

    void Choose(Move move)
    {
        var offer = Queue[0];
        Queue.RemoveAt(0);
        if (offer.Kind == MoveKind.Reward)
        {
            Treasure((RewardId)move.Param);
            return;
        }
        if (!Holds((ToolId)move.Param)) Held.Add((ToolId)move.Param);
        if (Queue.Count == 0 && Phase == Phase.Patrol && Doors.Length == 0) RollDoors();
    }

    void Treasure(RewardId reward)
    {
        switch (reward)
        {
            case RewardId.Ring:
                OpenRandom(3);
                break;
            case RewardId.Horn:
                foreach (var eater in Inmates())
                {
                    var meals = Cells[eater].Open ? Meals(eater).Where(meal => Cells[meal].Open).ToList() : [];
                    if (meals.Count > 0) Eat(eater, meals[Pick(meals.Count)], false);
                }
                break;
            case RewardId.Wine:
                foreach (var cell in Inmates().Where(cell => Cells[cell].Tier < Tiers.Legendary)) Raise(cell, 1);
                break;
            default:
                foreach (var cell in Sample([.. Inmates().Where(cell => !Meals(cell).Any(meal => Cells[meal].Open))], 4)) Hunger(cell);
                break;
        }
    }

    void Hunger(int cell)
    {
        if (!Cells[cell].Open) return;
        var fed = Enum.GetValues<Beast>().Where(beast => Inmates().Any(other => other != cell && Reaches(Cells[cell] with { Beast = beast }, Cells[other]))).ToList();
        if (fed.Count > 0) Cells[cell] = Cells[cell] with { Beast = fed[Pick(fed.Count)] };
    }
}
