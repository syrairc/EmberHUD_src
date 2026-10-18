using System.Linq;

namespace Tartarus.Sim;

static class Boards
{
    public static Run At(Phase phase, int position, params (int Cell, Beast Beast, int Tier)[] inmates) => At(1, phase, position, inmates);

    public static Run At(ulong seed, Phase phase, int position, params (int Cell, Beast Beast, int Tier)[] inmates)
    {
        var run = new Run(new Rules { EventChance = 0 }, new Rng(seed)) { Phase = phase, Position = position };
        foreach (var (cell, beast, tier) in inmates) run.Cells[cell] = new Cell(true, beast, tier, false);
        return run;
    }

    public static Run Event(EventKind kind, int cell, params (int Cell, Beast Beast, int Tier)[] inmates) => Event(1, kind, cell, inmates);

    public static Run Event(ulong seed, EventKind kind, int cell, params (int Cell, Beast Beast, int Tier)[] inmates)
    {
        var run = At(seed, Phase.Event, cell, inmates);
        run.PendingEvent = kind;
        run.EventCell = cell;
        return run;
    }

    public static void Hide(this Run run, int cell, Beast beast, int tier) => run.Cells[cell] = new Cell(false, beast, tier, false);

    public static int[] Tiers(this Run run) => [.. run.Cells.Select(cell => cell.Open ? cell.Tier : -1)];

    public static int[] Where(this Run run, System.Func<Cell, bool> test) => [.. Enumerable.Range(0, run.Cells.Length).Where(cell => test(run.Cells[cell]))];
}
