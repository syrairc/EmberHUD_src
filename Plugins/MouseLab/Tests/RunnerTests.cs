using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class RunnerTests
{
    static ScreenRect Rect(float x, float y) => new(new Vector2(x, y), new Vector2(x + 40, y + 40));

    static Step Move(string label, float x, float y, bool scored = true) => new(label, Rect(x, y), scored);

    static SuiteBuild Build(params Scenario[] scenarios) => new(scenarios, []);

    static SuiteBuild OneScenario() => Build(new Scenario("One", [Move("start", 0, 0, false), Move("far", 300, 200)]));

    static SuiteBuild TwoScenarios() => Build(
        new Scenario("One", [Move("start", 0, 0, false), Move("a", 300, 200), Move("b", 100, 400)]),
        new Scenario("Two", [Move("start", 0, 0, false), Move("c", 500, 300)]));

    sealed class Fixture
    {
        public List<SentInput> Sent = TestSend.Record();
        public int CursorReads;
        public long Clock;
        public List<int> Delays = [];

        public (int X, int Y) Cursor()
        {
            Interlocked.Increment(ref CursorReads);
            var sent = Sent;
            lock (sent)
            {
                for (var i = sent.Count - 1; i >= 0; i--)
                {
                    var input = sent[i];
                    if (input.Button is null && input.Key == Key.None) return (input.X, input.Y);
                }
            }
            return (0, 0);
        }

        public Sampler NewSampler() => new(Cursor, () => Interlocked.Add(ref Clock, 4), _ => Thread.Sleep(1));

        public SuiteRunner Runner(Func<int, CancellationToken, Task>? delay = null) => new(NewSampler, delay ?? ((ms, _) => { Delays.Add(ms); return Task.CompletedTask; }), new Thresholds());
    }

    static Fixture Setup()
    {
        TestSend.Reset();
        TestGame.Set(GameState.Empty with { ChatOpen = false });
        var fixture = new Fixture();
        TestSend.Instant();
        return fixture;
    }

    static RunResult Run(SuiteRunner runner, SuiteBuild build, CancellationToken cancel = default) => runner.RunAsync("Preset", build, new HumanizerConfig(), cancel).GetAwaiter().GetResult();

    [Test]
    public void ScoresAMoveFromTheRealCursorSamples()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(), OneScenario());
        Check.That(result.Refused is null && !result.Cancelled, "ran clean");
        var move = result.Moves.Single();
        Check.Equal("One", move.Scenario);
        Check.Equal("far", move.Label);
        Check.That(move.Trail.Count >= 2, "trail has the start and the end");
        Check.That(move.Scores.Distance > 300, $"distance {move.Scores.Distance}");
        Check.That(move.Scores.FinalError <= 6, $"final error {move.Scores.FinalError}");
        Check.That(move.Verdict.FinalErrorOk, "verdict follows the scores");
        Check.Equal("Preset", result.Preset);
    }

    [Test]
    public void RefusesWhenSendIsNotAllowedAndSendsNothing()
    {
        var fixture = Setup();
        fixture.Sent = TestSend.Record(allowed: false);
        var result = Run(fixture.Runner(), OneScenario());
        Check.That(result.Refused is not null, "refused");
        Check.Equal(0, result.Moves.Count);
        Check.Equal(0, fixture.Sent.Count);
    }

    [Test]
    public void RefusesAnEmptyBuild()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(), Build());
        Check.That(result.Refused is not null, "refused");
        Check.Equal(0, fixture.Sent.Count);
    }

    [Test]
    public void PausesBetweenMovesAndScenarios()
    {
        var fixture = Setup();
        Run(fixture.Runner(), TwoScenarios());
        Check.That(fixture.Delays.SequenceEqual([150, 250, 150, 250, 150, 500, 150, 250, 150]), string.Join(",", fixture.Delays));
    }

    [Test]
    public void CancelMidRunStopsReleasesAndMarksCancelled()
    {
        var fixture = Setup();
        using var source = new CancellationTokenSource();
        var sentAtCancel = -1;
        var runner = fixture.Runner((ms, token) =>
        {
            fixture.Delays.Add(ms);
            if (ms == 250 && fixture.Delays.Count(delay => delay == 250) == 2)
            {
                sentAtCancel = fixture.Sent.Count;
                source.Cancel();
            }
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        var result = Run(runner, TwoScenarios(), source.Token);
        var readsAtEnd = fixture.CursorReads;
        Check.That(result.Cancelled, "cancelled");
        Check.Equal(1, result.Moves.Count, "only the first scored move was kept");
        Check.Equal(sentAtCancel, fixture.Sent.Count, "nothing sent after the cancel");
        Thread.Sleep(30);
        Check.Equal(readsAtEnd, fixture.CursorReads, "sampler thread ended");
    }

    [Test]
    public void AFailedMoveStopsTheRun()
    {
        var fixture = Setup();
        var runner = fixture.Runner((ms, _) =>
        {
            fixture.Delays.Add(ms);
            if (fixture.Delays.Count == 1) fixture.Sent = TestSend.Record(allowed: false);
            return Task.CompletedTask;
        });
        var result = Run(runner, TwoScenarios());
        Check.That(result.Cancelled, "stopped");
        Check.Equal(0, result.Moves.Count);
        Check.That(fixture.Delays.Count < 9, $"stopped early, {fixture.Delays.Count} delays");
    }

    [Test]
    public void UnscoredStepsAreNotInTheResult()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(), TwoScenarios());
        Check.That(result.Moves.Select(move => move.Label).SequenceEqual(["a", "b", "c"]), string.Join(",", result.Moves.Select(move => move.Label)));
    }

    [Test]
    public void SamplerCapsAtFiveThousandSamples()
    {
        var reads = 0;
        var sampler = new Sampler(() => (Interlocked.Increment(ref reads), 0), () => reads, _ => { });
        sampler.Start();
        var give = Environment.TickCount64 + 5000;
        while (Volatile.Read(ref reads) <= 5000 && Environment.TickCount64 < give) Thread.Sleep(1);
        var samples = sampler.Stop();
        Check.Equal(5000, samples.Count);
    }

    [Test]
    public void SamplerStopJoinsItsThread()
    {
        var reads = 0;
        var sampler = new Sampler(() => (Interlocked.Increment(ref reads), 0), () => reads, _ => Thread.Sleep(1));
        sampler.Start();
        Thread.Sleep(20);
        sampler.Stop();
        var after = Volatile.Read(ref reads);
        Thread.Sleep(30);
        Check.Equal(after, Volatile.Read(ref reads), "no reads after Stop");
        Check.That(after > 1, "it was sampling");
    }

    [Test]
    public void SamplerEndsQuietlyWhenTheDelegatesThrow()
    {
        var reads = 0;
        var sampler = new Sampler(() => Interlocked.Increment(ref reads) > 2 ? throw new InvalidOperationException("cursor") : (reads, 0), () => 0, _ => throw new InvalidOperationException("wait"));
        sampler.Start();
        Thread.Sleep(20);
        var samples = sampler.Stop();
        Check.That(samples.Count >= 1, "kept what it read");
        var throwing = new Sampler(() => throw new InvalidOperationException("cursor"), () => 0, _ => { });
        throwing.Start();
        Check.Equal(0, throwing.Stop().Count);
    }

    [Test]
    public void StopWithoutStartTakesNoSample()
    {
        var reads = 0;
        var sampler = new Sampler(() => (Interlocked.Increment(ref reads), 0), () => 0, _ => { });
        Check.Equal(0, sampler.Stop().Count);
        sampler.Dispose();
        Check.Equal(0, reads);
    }
}
