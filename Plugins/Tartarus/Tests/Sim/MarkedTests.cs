using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class MarkedTests
{
    static Run Patrol(int marked, ObjectiveKind? objective = null)
    {
        var run = Boards.At(Phase.Patrol, 4, (1, Beast.Wolf, 1));
        run.Doors = [3, 5];
        run.Ratway = true;
        run.Cells[marked] = run.Cells[marked] with { Marked = true };
        if (objective is { } kind) run.Objective = new Objective(kind, 0, false);
        return run;
    }

    [Test]
    public void AMarkedLockedDoorIsWorthAnUnknownEvent()
    {
        foreach (var marked in new[] { 3, 5 })
        {
            var run = Patrol(marked);
            run.Pool = [EventKind.HighRiskConvict];
            Check.Equal(new Move(MoveKind.Door, marked), Strategy.Choose(run, new Rng(1)));
        }
    }

    [Test]
    public void AMarkedCellCountsAsAnIncident()
    {
        foreach (var marked in new[] { 3, 5 })
            Check.Equal(new Move(MoveKind.Door, marked), Strategy.Choose(Patrol(marked, ObjectiveKind.Incident), new Rng(1)));
    }

    [Test]
    public void AMarkedCellCountsAsHoldingAnEventForInstigate()
    {
        var run = Boards.At(Phase.Office, 0, (1, Beast.Wolf, 1));
        run.Cells[3] = run.Cells[3] with { Marked = true };
        Check.Equal(1, run.InstigateCount);
        Check.That(!run.InstigateCells().Contains(3));
    }

    [Test]
    public void ASpawnedEventNeverLandsOnAMarkedCell()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var run = new Run(Boards.Quiet with { EventActionChance = 100 }, new Rng(seed)) { Phase = Phase.Manage, Position = 1, Pool = [EventKind.HighRiskConvict] };
            run.Cells[1] = new Cell(true, Beast.Wolf, 1, false);
            for (var cell = 2; cell < run.Cells.Length; cell++) run.Cells[cell] = run.Cells[cell] with { Event = EventKind.CrowWhispers };
            run.Cells[3] = run.Cells[3] with { Event = null, Marked = true };
            run.Apply(new Move(MoveKind.Replace));
            Check.That(run.Cells[3].Event is null, $"seed {seed}");
        }
    }

    [Test]
    public void AnOpenChromaticCellNeverBreaksTheStrategy()
    {
        foreach (var maul in new[] { false, true })
            for (var beast = Beast.Mammoth; beast <= Beast.Rat; beast++)
            {
                var run = Boards.At(Phase.Manage, 1, (1, Beast.Mammoth, 2), (2, Beast.Lion, 5), (3, beast, 5));
                if (maul) run.Held.Add(ToolId.Maul);
                Strategy.Choose(run, new Rng(1));
            }
    }
}
