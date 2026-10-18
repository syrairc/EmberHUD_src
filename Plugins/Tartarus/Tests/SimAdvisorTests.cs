using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Tartarus.Sim;

namespace Tartarus;

public sealed class SimAdvisorTests
{
    static readonly Settings Defaults = new();

    static readonly Run AnyRun = Boards.At(Phase.Patrol, 0);

    static TartarusOption Door(int id, int cell) => new(id, TartarusOptionKind.Door, cell, 0);

    static TartarusState Doors(params int[] cells) =>
        new(1, 16, 5, 20, [], new TartarusChoice(1, TartarusChoiceKind.Doors, [.. cells.Select((cell, i) => Door(i + 1, cell))], true), [], 0);

    static Ranked Rank(int cell, double value, double spread = 0.05) => new(new Move(MoveKind.Door, cell), value, spread);

    static MapResult Mapped(params string[] assumed) => new(AnyRun, assumed, null);

    static int[] Glowing(Advice advice) => advice.Options.Where(o => o.Glow).Select(o => o.OptionId).ToArray();

    [Test]
    public void ASingleOptionNeverGlows()
    {
        var advice = SimAdvisor.Advise(Doors(5), Mapped(), [Rank(5, 2.0)], Defaults);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal("only option", advice.Summary);
    }

    [Test]
    public void HandEditedSettingsAreClampedOnUse()
    {
        var rules = new Settings { ChromaticCap = -5, Playouts = 0 }.ToRules();
        Check.Equal(1, rules.ChromaticCap);
        Check.Equal(20, new Settings { Playouts = 0 }.ClampedPlayouts);
    }

    [Test]
    public void GlowsTheBestOptionOfARankedBoard()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5, 7), Mapped(), [Rank(5, 2.0), Rank(3, 1.0), Rank(7, 0.5)], Defaults);
        Check.Equal("2", string.Join(',', Glowing(advice)));
        Check.Equal("1 best", advice.Summary);
        Check.That(System.Math.Abs(advice.Options[1].Value.Low - 1.95) < 1e-9 && System.Math.Abs(advice.Options[1].Value.High - 2.05) < 1e-9, advice.Options[1].Value.ToString());
        Check.Equal("2.00", advice.Options[1].Reason);
    }

    [Test]
    public void CloseOptionsBothGlow()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5, 7), Mapped(), [Rank(5, 2.0), Rank(3, 1.97), Rank(7, 0.5)], Defaults);
        Check.Equal("1,2", string.Join(',', Glowing(advice)));
        Check.Equal("2 best", advice.Summary);
        Check.Equal("2", string.Join(',', advice.Options.Where(o => o.Top).Select(o => o.OptionId)));
    }

    [Test]
    public void EqualOptionsAllGlowAndOneIsTop()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5, 7), Mapped(), [Rank(5, 1.0), Rank(3, 1.0), Rank(7, 1.0)], Defaults);
        Check.Equal(3, Glowing(advice).Length);
        Check.Equal(1, advice.Options.Count(o => o.Top));
        Check.Equal("3 best", advice.Summary);
    }

    [Test]
    public void MismatchGivesNoGlowAndSaysWhy()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5), new MapResult(null, [], "The board and the simulator disagree."), [Rank(5, 2.0), Rank(3, 1.0)], Defaults);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal("mismatch: The board and the simulator disagree.", advice.Summary);
        Check.Equal(2, advice.Options.Length);
    }

    [Test]
    public void ChoiceNotSimulatedYetSaysSoWithoutAMismatch()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5), new MapResult(null, [], null, "An Other choice is not simulated yet."), null, Defaults);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal("An Other choice is not simulated yet.", advice.Summary);
    }

    [Test]
    public void AssumedPartsAreShownNotHidden()
    {
        var ranked = new[] { Rank(5, 2.0), Rank(3, 1.0) };
        var advice = SimAdvisor.Advise(Doors(3, 5), Mapped("Tool uses left are assumed to be the defaults.", "Second guess."), ranked, Defaults);
        Check.That(advice.Summary.StartsWith("assumed: Tool uses left"), advice.Summary);
        Check.That(advice.Summary.Contains("Second guess."), advice.Summary);
        Check.That(advice.Summary.EndsWith("1 best"), advice.Summary);
        Check.Equal("2", string.Join(',', Glowing(advice)));
        Check.Equal("1 best", SimAdvisor.Advise(Doors(3, 5), Mapped("x"), ranked, new Settings { ShowAssumed = false }).Summary);
    }

    [Test]
    public void ThinkingShowsTheStrategyPickAtOnce()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5, 7), Mapped(), null, Defaults, new Move(MoveKind.Door, 5));
        Check.Equal("thinking", advice.Summary);
        Check.Equal("2", string.Join(',', Glowing(advice)));
        Check.Equal("2", string.Join(',', advice.Options.Where(o => o.Top).Select(o => o.OptionId)));
    }

    [Test]
    public void ThinkingWithAPickNoOptionMatchesShowsNothing()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5), Mapped(), null, Defaults, new Move(MoveKind.Hunt, 4));
        Check.Equal("thinking", advice.Summary);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal(0, advice.Options.Count(o => o.Top));
    }

    [Test]
    public void AStrategyPickWithoutAnOptionHasNoTopButCloseOptionsStillGlow()
    {
        var hunt = new Ranked(new Move(MoveKind.Hunt, 4), 2.0, 0.05, 0, true);
        var advice = SimAdvisor.Advise(Doors(3, 5), Mapped(), [hunt, Rank(5, 1.99), Rank(3, 0.5)], Defaults);
        Check.Equal("2", string.Join(',', Glowing(advice)));
        Check.Equal(0, advice.Options.Count(o => o.Top));
    }

    [Test]
    public void ThinkingWhileNoRankingYet()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5), Mapped(), null, Defaults);
        Check.Equal("thinking", advice.Summary);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal(2, advice.Options.Length);
    }

    [Test]
    public void AnOptionTheSimulatorDidNotRankMakesTheAdviceUnsure()
    {
        var advice = SimAdvisor.Advise(Doors(3, 5), Mapped(), [Rank(5, 2.0)], Defaults);
        Check.Equal(0, Glowing(advice).Length);
        Check.That(advice.Summary.StartsWith("unsure"), advice.Summary);
    }

    [Test]
    public void InstrumentAndRewardOptionsMatchTheirMoves()
    {
        var state = Doors(3) with
        {
            Choice = new TartarusChoice(2, TartarusChoiceKind.Instrument, [new(1, TartarusOptionKind.Instrument, -1, 106), new(2, TartarusOptionKind.Instrument, -1, 131)], true),
        };
        var advice = SimAdvisor.Advise(state, Mapped(), [new(new Move(MoveKind.Instrument, Param: (int)ToolId.ShortChain), 1.5, 0.05), new(new Move(MoveKind.Instrument, Param: (int)ToolId.Whip), 1.0, 0.05)], Defaults);
        Check.Equal("2", string.Join(',', Glowing(advice)));
        Check.Equal("2", string.Join(',', Glowing(SimAdvisor.Advise(Doors(3) with
        {
            Choice = new TartarusChoice(3, TartarusChoiceKind.Reward, [new(1, TartarusOptionKind.Reward, -1, 1), new(2, TartarusOptionKind.Reward, -1, 3)], true),
        }, Mapped(), [new(new Move(MoveKind.Reward, Param: 2), 1.5, 0.05), new(new Move(MoveKind.Reward, Param: 0), 0.5, 0.05)], Defaults))));
    }

    static Advice ReplaceOrEnd(double end, double replace, double gapSe)
    {
        var run = Boards.At(Phase.Manage, 1, (1, Beast.Wolf, 1));
        var state = Doors(3) with { Choice = new TartarusChoice(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Replace, -1, 0), new(2, TartarusOptionKind.End, -1, 0)], true) };
        return SimAdvisor.Advise(state, new MapResult(run, [], null), [new(new Move(MoveKind.End), end, 0.05), new(new Move(MoveKind.Replace), replace, 0.05, gapSe, true)], Defaults);
    }

    static string Top(Advice advice) => string.Join(',', advice.Options.Where(o => o.Top).Select(o => o.OptionId));

    [Test]
    public void TheStrategyPickStandsWhileTheBestMoveLeadsByLessThanItsPairedMarginOrFiveHundredths()
    {
        var noise = ReplaceOrEnd(4.01, 3.97, 0.01);
        Check.Equal("1", Top(noise));
        Check.Equal("1,2", string.Join(',', Glowing(noise)));
        Check.Equal("1", Top(ReplaceOrEnd(4.2, 3.97, 0.2)));
    }

    [Test]
    public void AClearlyBetterMoveReplacesTheStrategyPick()
    {
        var clear = ReplaceOrEnd(4.1, 3.97, 0.03);
        Check.Equal("2", Top(clear));
        Check.Equal("2", string.Join(',', Glowing(clear)));
    }

    [Test]
    public void GarbageStateGivesNoAdviceAndNoException()
    {
        var state = Doors(3, 5);
        Check.Equal(0, SimAdvisor.Advise(state with { Choice = null }, Mapped(), null, Defaults).Options.Length);
        Check.Equal(0, SimAdvisor.Advise(state with { Choice = state.Choice! with { Options = null! } }, Mapped(), null, Defaults).Options.Length);
        Check.Equal(0, SimAdvisor.Advise(null!, Mapped(), null, Defaults).Options.Length);
        Check.Equal(0, SimAdvisor.Advise(state, null!, null, Defaults).Options.Length);
        Check.Equal(0, Glowing(SimAdvisor.Advise(state, Mapped(), [], Defaults)).Length);
    }

    [Test]
    public void MaxDayThirtyIsAdvisedLikeTwenty()
    {
        foreach (var maxDay in new[] { 20, 30 })
        {
            var cells = Enumerable.Range(0, 8).Select(index => index is 1 or 2
                ? new TartarusCell(index, true, 3, index == 1 ? TartarusBeast.Lion : TartarusBeast.Wolf, index == 1, false)
                : new TartarusCell(index, false, -1, TartarusBeast.Unknown, false, false)).ToArray();
            var choice = new TartarusChoice(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Consume, 2, 0), new(2, TartarusOptionKind.End, -1, 0)], true);
            var state = new TartarusState(1, 8, 5, maxDay, cells, choice, [], 0);
            var map = StateMapper.Map(state, new TrackedRun { Day = 5, Size = 8 }, Defaults.ToRules());
            Check.That(map.Run is not null, $"{maxDay}: {map.Mismatch}");
            var advice = SimAdvisor.Advise(state, map, Solver.Rank(map.Run!, 60, new Rng(1)), Defaults);
            Check.That(advice.Summary.EndsWith("best"), $"{maxDay}: {advice.Summary}");
            Check.That(!advice.Summary.Contains("unsure"), $"{maxDay}: {advice.Summary}");
        }
    }

    [Test]
    public void AChainBoardIsRankedIncludingStopChain()
    {
        TartarusCell Cell(int index) => index switch
        {
            1 => new TartarusCell(1, true, 4, TartarusBeast.Lion, true, false),
            2 => new TartarusCell(2, true, 4, TartarusBeast.Wolf, false, false),
            _ => new TartarusCell(index, false, -1, TartarusBeast.Unknown, false, false),
        };
        var choice = new TartarusChoice(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Consume, 2, 0), new(2, TartarusOptionKind.StopChain, -1, 0)], true);
        var state = new TartarusState(1, 8, 5, 20, [.. Enumerable.Range(0, 8).Select(Cell)], choice, [], 0);
        var map = StateMapper.Map(state, new TrackedRun { Day = 5, Size = 8 }, Defaults.ToRules());
        Check.That(map.Run is not null, map.Mismatch);
        var advice = SimAdvisor.Advise(state, map, Solver.Rank(map.Run!, 60, new Rng(1)), Defaults);
        Check.That(advice.Summary.EndsWith("best"), advice.Summary);
        Check.That(advice.Options.All(option => option.Reason != ""));
    }
}
