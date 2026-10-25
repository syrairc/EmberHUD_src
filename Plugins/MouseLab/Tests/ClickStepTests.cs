using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class ClickStepTests
{
    static BagCell Cell(int slot) => new(slot, new Vector2(slot % 7 * 50, slot / 7 * 50), new Vector2(slot % 7 * 50 + 40, slot / 7 * 50 + 40));

    static StashView Stash(params int[] without) => new(0, Vector2.Zero, new Vector2(400, 300), Enumerable.Range(0, 35).Where(slot => !without.Contains(slot)).Select(Cell).ToArray());

    static ScreenRect Rect(float x, float y) => new(new Vector2(x, y), new Vector2(x + 60, y + 30));

    static readonly ScreenRect TakeOutRect = Rect(200, 60);

    static ItemMenu FullMenu(float lockX = 200) => new(TakeOutRect, Rect(lockX, 100), Rect(200, 140), null, null, null, null);

    static (float X, float Y) Centre(ScreenRect rect) => ((rect.Min.X + rect.Max.X) / 2, (rect.Min.Y + rect.Max.Y) / 2);

    sealed class Fixture
    {
        public List<SentInput> Sent = TestSend.Record();
        public List<int> Delays = [];

        public (int X, int Y) Cursor()
        {
            lock (Sent)
            {
                for (var i = Sent.Count - 1; i >= 0; i--)
                    if (Sent[i].Button is null && Sent[i].Key == Key.None) return (Sent[i].X, Sent[i].Y);
            }
            return (0, 0);
        }

        public List<(int X, int Y)> Clicks()
        {
            lock (Sent)
            {
                var clicks = new List<(int X, int Y)>();
                var position = (X: 0, Y: 0);
                foreach (var input in Sent)
                {
                    if (input.Button is null && input.Key == Key.None) position = (input.X, input.Y);
                    else if (input.Button is not null && input.Down) clicks.Add(position);
                }
                return clicks;
            }
        }

        public SuiteRunner Runner(Func<ItemMenu?> menu, Func<int, CancellationToken, Task>? delay = null) =>
            new(() => new Sampler(Cursor, () => 0, _ => Thread.Sleep(1)), delay ?? ((ms, _) => { Delays.Add(ms); return Task.CompletedTask; }), new Thresholds(), menu: menu);
    }

    static Fixture Setup()
    {
        TestSend.Reset();
        TestGame.Set(GameState.Empty with { ChatOpen = false });
        var fixture = new Fixture();
        TestSend.Instant();
        return fixture;
    }

    static SuiteBuild Build(MenuOption option) => Suite.Build(Stash(), null, menuOption: option);

    static SuiteBuild ClickScenario(MenuOption option) => new([Suite.Build(Stash(), null, menuOption: option).Scenarios.Single(scenario => scenario.Name == "Click and menu")], []);

    static RunResult Run(SuiteRunner runner, SuiteBuild build, CancellationToken cancel = default) => runner.RunAsync("Preset", build, new HumanizerConfig(), cancel).GetAwaiter().GetResult();

    [Test]
    public void ScenarioBuildsOnlyWhenACellExists()
    {
        Check.That(Build(MenuOption.Lock).Scenarios.Any(scenario => scenario.Name == "Click and menu"), "built with cell (0,0)");
        var without = Suite.Build(Stash(0), null, menuOption: MenuOption.Lock);
        Check.That(without.Scenarios.All(scenario => scenario.Name != "Click and menu"), "not built without a cell");
        Check.That(without.Skipped.Any(skip => skip.StartsWith("Click and menu")), string.Join("|", without.Skipped));
        Check.That(Suite.Build(Stash(), null).Scenarios.All(scenario => scenario.Name != "Click and menu"), "off unless asked for");
    }

    [Test]
    public void MenuNeverAppearingStopsTheRunWithANote()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(() => null), ClickScenario(MenuOption.Lock));
        Check.That(result.Refused is { } note && note.Contains("menu", StringComparison.OrdinalIgnoreCase), result.Refused);
        Check.Equal(0, result.Moves.Count);
        Check.Equal(1, fixture.Clicks().Count, "only the cell click");
        Check.Equal(20, fixture.Delays.Count(delay => delay == 50), "waited 1 s in 50 ms steps");
    }

    [Test]
    public void TakeOutIsNotSelectableAndNeverClicked()
    {
        Check.That(!Enum.GetNames<MenuOption>().Contains("TakeOut"), "no TakeOut value");
        foreach (var option in Enum.GetValues<MenuOption>().Where(option => option != MenuOption.None))
        {
            var fixture = Setup();
            var result = Run(fixture.Runner(() => new ItemMenu(TakeOutRect, null, null, null, null, null, null)), ClickScenario(option));
            Check.That(result.Refused is not null, "refused when only TakeOut shows");
            Check.Equal(1, fixture.Clicks().Count, "only the cell click");
            var (x, y) = Centre(TakeOutRect);
            Check.That(!fixture.Clicks().Contains(((int)x, (int)y)), "TakeOut centre not clicked");
        }
    }

    [Test]
    public void LockIsClickedAnEvenNumberOfTimesAndMoreOnce()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(() => FullMenu()), ClickScenario(MenuOption.Lock));
        Check.That(result.Refused is null && !result.Cancelled, "ran clean");
        var lockRect = FullMenu().Lock!.Value;
        var lockClicks = fixture.Clicks().Skip(1).ToArray();
        Check.Equal(2, lockClicks.Length, "lock twice");
        Check.That(lockClicks.All(click => click.X >= lockRect.Min.X && click.X <= lockRect.Max.X && click.Y >= lockRect.Min.Y && click.Y <= lockRect.Max.Y), "both on the lock button: " + string.Join(";", lockClicks));
        Check.Equal(1, result.Moves.Count, "one scored move to the button");
        var more = Setup();
        Run(more.Runner(() => FullMenu()), ClickScenario(MenuOption.More));
        Check.Equal(2, more.Clicks().Count, "cell plus one More click");
    }

    [Test]
    public void CancelBetweenTheLockClicksStillReleases()
    {
        var fixture = Setup();
        Send.Down(Key.D1);
        using var source = new CancellationTokenSource();
        var runner = fixture.Runner(() => FullMenu(), (ms, token) =>
        {
            fixture.Delays.Add(ms);
            if (fixture.Clicks().Count == 2) source.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        var result = Run(runner, ClickScenario(MenuOption.Lock), source.Token);
        Check.That(result.Cancelled, "cancelled");
        Check.Equal(2, fixture.Clicks().Count, "no second lock click");
        lock (fixture.Sent) Check.That(fixture.Sent.Contains(new SentInput(Key.D1, null, false, 0, 0)), "held key released");
        Check.That(result.Note is { } note && note.Contains("one Lock click"), result.Note);
    }

    [Test]
    public void MenuGoingAwayAfterTheFirstLockClickSaysSo()
    {
        var fixture = Setup();
        var result = Run(fixture.Runner(() => fixture.Clicks().Count >= 2 ? null : FullMenu()), ClickScenario(MenuOption.Lock));
        Check.That(result.Refused is not null, "refused");
        Check.That(result.Note is { } note && note.Contains("one Lock click") && note.Contains("check it"), result.Note);
        Check.Equal(2, fixture.Clicks().Count, "cell plus one lock click");
    }

    [Test]
    public void ACompleteLockPairLeavesNoNote()
    {
        var fixture = Setup();
        Check.That(Run(fixture.Runner(() => FullMenu()), ClickScenario(MenuOption.Lock)).Note is null, "no note");
    }

    [Test]
    public void NoneBuildsNoClickScenarioAndNoSkip()
    {
        var build = Suite.Build(Stash(), null, menuOption: MenuOption.None);
        Check.That(build.Scenarios.All(scenario => scenario.Name != "Click and menu"), "no scenario");
        Check.That(build.Skipped.All(skip => !skip.StartsWith("Click and menu")), "no skip line");
        Check.That(!Enum.GetNames<MenuOption>().Contains("TakeOut"), "still no TakeOut");
    }

    [Test]
    public void TheOptionClickUsesTheRectReadAtClickTime()
    {
        var fixture = Setup();
        var reads = 0;
        var result = Run(fixture.Runner(() => FullMenu(200 + 100 * Interlocked.Increment(ref reads))), ClickScenario(MenuOption.Lock));
        Check.That(result.Refused is null, result.Refused);
        var lockClicks = fixture.Clicks().Skip(1).Select(click => click.X).ToArray();
        Check.Equal(2, lockClicks.Length);
        Check.That(lockClicks[0] != lockClicks[1], $"second click follows the moved button: {string.Join(",", lockClicks)}");
        Check.That(lockClicks[1] > lockClicks[0], "and is the later read");
    }
}
