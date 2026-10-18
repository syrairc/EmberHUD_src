using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Tartarus.Sim;

public sealed record Ranked(Move Move, double Value, double Spread);

public static class Solver
{
    const int StepLimit = 5000;

    public static Ranked[] Rank(Run start, int playouts, Rng rng, CancellationToken cancel = default)
    {
        var moves = start.Legal();
        if (moves.Count <= 1) return [.. moves.Select(move => new Ranked(move, 0, 0))];
        var sums = new double[moves.Count];
        var squares = new double[moves.Count];
        var done = 0;
        while (done < playouts && !cancel.IsCancellationRequested)
        {
            var seed = ((ulong)rng.Next() << 32) | rng.Next();
            for (var i = 0; i < moves.Count; i++)
            {
                var chromatics = Playout(start, moves[i], seed);
                sums[i] += chromatics;
                squares[i] += chromatics * chromatics;
            }
            done++;
        }
        return [.. moves.Select((move, i) => Summarize(move, sums[i], squares[i], done)).OrderByDescending(ranked => ranked.Value)];
    }

    static Ranked Summarize(Move move, double sum, double squares, int count)
    {
        if (count == 0) return new Ranked(move, 0, 0);
        var mean = sum / count;
        var variance = Math.Max(0, squares / count - mean * mean);
        return new Ranked(move, mean, Math.Sqrt(variance / count));
    }

    static int Playout(Run start, Move first, ulong seed)
    {
        var run = start.Reroll(new Rng(seed));
        var play = new Rng(seed ^ 0x9E3779B97F4A7C15UL);
        run.Apply(first);
        for (var step = 0; step < StepLimit && !run.Over; step++)
        {
            if (run.Legal().Count == 0) break;
            run.Apply(Strategy.Choose(run, play));
        }
        return run.Chromatics;
    }
}
