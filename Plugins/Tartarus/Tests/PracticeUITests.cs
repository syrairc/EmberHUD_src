using System.Linq;
using EmberHUD.Testing;
using Tartarus.Sim;

namespace Tartarus;

public sealed class PracticeUITests
{
    [Test]
    public void DrawingDoesNotThrowOnAnyLegalState()
    {
        using var session = new PracticeSession(new Rules { Days = 30 }, 11) { Playouts = 5 };
        var rng = new Rng(3);
        for (var step = 0; step < 5000 && !session.Run.Over; step++)
        {
            TestFrame.Run(() => PracticeUI.Draw(session));
            var legal = session.Legal;
            session.Play(legal[rng.Next(legal.Count)]);
            if (step % 50 == 0) session.RequestHint();
        }
        Check.That(session.Run.Over, "run never ended");
        TestFrame.Run(() => PracticeUI.Draw(session));
    }

    [Test]
    public void DrawsOneButtonPerLegalMove()
    {
        using var session = new PracticeSession(new Rules { Days = 30 }, 11);
        for (var step = 0; step < 40 && !session.Run.Over; step++)
        {
            TestFrame.Run(() => PracticeUI.Draw(session));
            Check.Equal(session.Legal.Count, PracticeUI.MoveButtons);
            session.Play(session.Legal[^1]);
        }
    }

    [Test]
    public void EveryLegalMoveHasAReadableLabel()
    {
        using var session = new PracticeSession(new Rules { Days = 30 }, 11);
        var rng = new Rng(9);
        for (var step = 0; step < 400 && !session.Run.Over; step++)
        {
            Check.That(session.Legal.All(move => PracticeUI.Label(move, session.Run).Length > 0));
            session.Play(session.Legal[rng.Next(session.Legal.Count)]);
        }
    }
}
