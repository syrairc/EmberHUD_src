using System.Linq;
using System.Threading;
using EmberHUD.Testing;
using Tartarus.Sim;

namespace Tartarus;

public sealed class PracticeSessionTests
{
    static readonly Rules Rules = new() { Days = 30 };

    static int[] Snapshot(Run run) => [run.Day, run.Position, run.Chromatics, (int)run.Phase, run.Tools.Count, run.Offers.Count, .. run.Cells.Select(cell => cell.Open ? cell.Tier : -1)];

    [Test]
    public void PlayAppliesAndUndoRestores()
    {
        using var session = new PracticeSession(Rules, 7);
        var before = Snapshot(session.Run);
        var move = session.Legal[0];
        session.Play(move);
        Check.That(!Snapshot(session.Run).SequenceEqual(before), "move changed nothing");
        Check.That(session.CanUndo);
        Check.That(session.Undo());
        Check.That(Snapshot(session.Run).SequenceEqual(before));
        Check.That(session.Legal.Contains(move));
    }

    [Test]
    public void UndoWithNoMovesReturnsFalse()
    {
        using var session = new PracticeSession(Rules, 7);
        Check.That(!session.Undo());
        Check.That(!session.CanUndo);
    }

    [Test]
    public void SameSeedReplaysTheSameRun()
    {
        using var first = new PracticeSession(Rules, 42);
        using var second = new PracticeSession(Rules, 42);
        for (var step = 0; step < 300 && !first.Run.Over; step++)
        {
            Check.That(first.Legal.SequenceEqual(second.Legal));
            first.Play(first.Legal[^1]);
            second.Play(second.Legal[^1]);
        }
        Check.That(Snapshot(first.Run).SequenceEqual(Snapshot(second.Run)));
    }

    [Test]
    public void RestartKeepsRulesAndClearsUndo()
    {
        using var session = new PracticeSession(Rules, 1);
        session.Play(session.Legal[0]);
        session.Restart(2);
        Check.Equal(2UL, session.Seed);
        Check.That(!session.CanUndo);
        Check.Equal(Rules, session.Rules);
    }

    [Test]
    public void HintMatchesTheSolverRank()
    {
        using var session = new PracticeSession(Rules, 5) { Playouts = 20 };
        Check.That(session.Hint is null);
        session.RequestHint();
        Check.That(SpinWait.SpinUntil(() => session.Hint is not null, 30000), "no hint");
        var expected = Solver.Rank(session.Run.Clone(), 20, new Rng(2));
        Check.That(session.Hint!.SequenceEqual(expected));
    }

    [Test]
    public void PlayingDropsTheHint()
    {
        using var session = new PracticeSession(Rules, 5) { Playouts = 20 };
        session.RequestHint();
        Check.That(SpinWait.SpinUntil(() => session.Hint is not null, 30000), "no hint");
        session.Play(session.Legal[0]);
        Check.That(session.Hint is null);
        Check.That(!session.HintPending);
    }
}
