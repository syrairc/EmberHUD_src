using System.Collections.Generic;
using EmberHUD;

namespace Tartarus;

public static class Names
{
    static readonly Dictionary<int, string> Instruments = new()
    {
        [102] = "Ferocious Beast's Ravenous Chains",
        [106] = "Barbed Whip",
        [112] = "Lost Ring of Keys",
        [123] = "Severed Iron Maiden",
        [127] = "Iron Pestle",
        [131] = "Short-Chained Shackles",
    };

    static readonly Dictionary<int, string> Rewards = new()
    {
        [1] = "Three-Headed Thumb Ring",
        [2] = "Brass Horn of Strife",
        [3] = "Crimson Vintage",
        [4] = "Brand of Original Sin",
    };

    public static string Option(TartarusOption option, TartarusState state) => option.Kind switch
    {
        TartarusOptionKind.Door => Door(option, state),
        TartarusOptionKind.Consume => $"Consume cell {option.Cell + 1}",
        TartarusOptionKind.Replace => "Replace",
        TartarusOptionKind.End => "End",
        TartarusOptionKind.StopChain => "Stop chain",
        TartarusOptionKind.Instigate => "Instigate",
        TartarusOptionKind.Bestow => "Bestow",
        TartarusOptionKind.Instrument => Instruments.GetValueOrDefault(option.Param, $"Instrument {option.Param}"),
        TartarusOptionKind.Reward => Rewards.GetValueOrDefault(option.Param, $"Reward {option.Param}"),
        _ => $"Option {option.Id}",
    };

    static string Door(TartarusOption option, TartarusState state)
    {
        var name = $"Cell {option.Cell + 1} ({option.Param} steps)";
        foreach (var cell in state.Cells)
            if (cell.Index == option.Cell && cell.Open && cell.Tier >= 0) return $"{name} {cell.Beast} T{cell.Tier}";
        return name;
    }
}
