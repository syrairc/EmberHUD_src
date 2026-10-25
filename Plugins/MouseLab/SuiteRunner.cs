using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;

namespace MouseLab;

public sealed record MoveResult(string Scenario, string Label, PathScores Scores, Verdict Verdict, IReadOnlyList<PathSample> Trail);

public sealed record RunResult(string Preset, IReadOnlyList<MoveResult> Moves, IReadOnlyList<string> Skipped, bool Cancelled, string? Refused);

public sealed class SuiteRunner(Func<Sampler> newSampler, Func<int, CancellationToken, Task> delay, Thresholds thresholds, Action<Step>? stepStarted = null)
{
    const int SettleMs = 150;
    const int StepPauseMs = 250;
    const int ScenarioPauseMs = 500;

    public async Task<RunResult> RunAsync(string presetName, SuiteBuild build, HumanizerConfig config, CancellationToken cancel)
    {
        var moves = new List<MoveResult>();
        RunResult Result(bool cancelled = false, string? refused = null) => new(presetName, moves, build.Skipped, cancelled, refused);

        RunResult Stopped()
        {
            Send.ReleaseAll();
            return Result(cancelled: true);
        }

        if (!Send.Allowed) return Result(refused: "Send is not allowed: game not focused or chat open");
        if (build.Scenarios.Count == 0) return Result(refused: "Nothing to run: the suite has no scenarios");

        using var sampler = newSampler();
        try
        {
            for (var s = 0; s < build.Scenarios.Count; s++)
            {
                if (s > 0) await delay(ScenarioPauseMs, cancel);
                var scenario = build.Scenarios[s];
                for (var i = 0; i < scenario.Steps.Count; i++)
                {
                    if (i > 0) await delay(StepPauseMs, cancel);
                    var step = scenario.Steps[i];
                    var centreX = (int)((step.Target.Min.X + step.Target.Max.X) / 2);
                    var centreY = (int)((step.Target.Min.Y + step.Target.Max.Y) / 2);
                    stepStarted?.Invoke(step);
                    sampler.Start();
                    var moved = await Send.MoveTo(centreX, centreY, MouseMode.Humanized, cancel, config);
                    if (moved) await delay(SettleMs, cancel);
                    var trail = sampler.Stop();
                    if (!moved || cancel.IsCancellationRequested) return Stopped();
                    if (!step.Scored) continue;
                    if (Send.ClientToScreen((int)step.Target.Min.X, (int)step.Target.Min.Y) is not var (minX, minY) || Send.ClientToScreen((int)step.Target.Max.X, (int)step.Target.Max.Y) is not var (maxX, maxY)) return Stopped();
                    var scores = PathMetrics.Measure(trail, new MetricsRect(minX, minY, maxX, maxY));
                    moves.Add(new MoveResult(scenario.Name, step.Label, scores, Scoring.Judge(scores, thresholds), trail));
                }
            }
        }
        catch (OperationCanceledException)
        {
            return Stopped();
        }
        return Result();
    }
}
