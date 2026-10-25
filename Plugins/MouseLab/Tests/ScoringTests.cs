using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class ScoringTests
{
    static PathScores Scores(double backtrack = 0, double overshoot = 0, double finalError = 0, double stall = 0) => new(0, 0, 0, 0, 0, finalError, overshoot, backtrack, 0, 0, stall);

    [Test]
    public void JudgeFlagsEachThresholdSeparately()
    {
        var limits = new Thresholds();
        Check.That(Scoring.Judge(Scores(), limits).All, "clean");
        Check.That(Scoring.Judge(Scores(3, 3, 6, 40), limits).All, "exact boundaries pass");
        var backtrack = Scoring.Judge(Scores(backtrack: 3.01), limits);
        Check.That(!backtrack.BacktrackOk && backtrack.OvershootOk && backtrack.FinalErrorOk && backtrack.StallOk && !backtrack.All, "backtrack");
        var overshoot = Scoring.Judge(Scores(overshoot: 3.01), limits);
        Check.That(overshoot.BacktrackOk && !overshoot.OvershootOk && overshoot.FinalErrorOk && overshoot.StallOk, "overshoot");
        var finalError = Scoring.Judge(Scores(finalError: 6.01), limits);
        Check.That(finalError.BacktrackOk && finalError.OvershootOk && !finalError.FinalErrorOk && finalError.StallOk, "final error");
        var stall = Scoring.Judge(Scores(stall: 40.01), limits);
        Check.That(stall.BacktrackOk && stall.OvershootOk && stall.FinalErrorOk && !stall.StallOk, "stall");
    }
}
