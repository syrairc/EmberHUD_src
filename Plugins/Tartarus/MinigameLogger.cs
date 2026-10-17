using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD;

namespace Tartarus;

public sealed class MinigameLogger(Func<int, int, int> collected)
{
    const double EndAfterSeconds = 3;
    const int MaxCollectableTier = 3;

    readonly HashSet<int> seen = [];
    Dictionary<int, TartarusCell>? previous;
    int previousChromatics;
    double? nullSince;

    public int Run { get; set; } = 1;

    public IReadOnlyList<PullEntry> Update(TartarusState? state, DateTime now, double nowSeconds)
    {
        if (state is null)
        {
            if (previous is null) return [];
            nullSince ??= nowSeconds;
            if (nowSeconds - nullSince >= EndAfterSeconds)
            {
                previous = null;
                seen.Clear();
                Run++;
            }
            return [];
        }

        nullSince = null;
        List<PullEntry> pulls = [];
        var sealedIndexes = previous is null ? [] : state.Cells.Where(cell => !cell.Open && previous.TryGetValue(cell.Index, out var before) && before.Open).Select(cell => cell.Index).ToArray();
        foreach (var cell in state.Cells)
        {
            if (!cell.Open || cell.Uid <= 0 || cell.Beast == TartarusBeast.Unknown || cell.Tier < 0 || !seen.Add(cell.Uid)) continue;
            var before = previous is not null && previous.TryGetValue(cell.Index, out var found) ? found : (TartarusCell?)null;
            var species = (int)cell.Beast - 1;
            var cause = previous is null ? "start"
                : before is not { Open: true } ? "door"
                : before.Value.Tier == 5 || state.Chromatics > previousChromatics ? "refill"
                : sealedIndexes.Any(index => index != cell.Index) && cell.Tier == before.Value.Tier + 1 ? "consume"
                : before.Value.Uid != cell.Uid ? "replace"
                : "other";
            pulls.Add(new(now, Run, state.Day, cell.Index, cell.Uid, species, cell.Tier, cause, cell.Tier <= MaxCollectableTier ? collected(species, cell.Tier) : -1));
        }

        previous = state.Cells.GroupBy(cell => cell.Index).ToDictionary(group => group.Key, group => group.First());
        previousChromatics = state.Chromatics;
        return pulls;
    }
}
