using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class AdvisorTests
{
    static readonly Settings Defaults = new();

    static TartarusCell Open(int index, int tier, bool devour = false) => new(index, true, tier, TartarusBeast.Mammoth, devour, false);

    static TartarusCell Locked(int index, int tier = -1, bool marker = false) => new(index, false, tier, TartarusBeast.Unknown, false, marker);

    static TartarusOption Door(int id, int cell) => new(id, TartarusOptionKind.Door, cell, 0);

    static TartarusState State(TartarusChoice? choice, TartarusCell[] cells, int position = 1, int day = 5, int maxDay = 20) =>
        new(position, 16, day, maxDay, cells, choice, [], 0);

    static TartarusChoice Choice(TartarusChoiceKind kind, params TartarusOption[] options) => new(1, kind, options, true);

    static Advice Doors(TartarusCell[] cells, params int[] targets) =>
        Advisor.Advise(State(Choice(TartarusChoiceKind.Doors, targets.Select((cell, i) => Door(i + 1, cell)).ToArray()), cells), Defaults);

    static int[] Glowing(Advice advice) => advice.Options.Where(o => o.Glow).Select(o => o.OptionId).ToArray();

    [Test]
    public void DevourDoorBeatsAKnownDoorWithNothingToEat()
    {
        var advice = Doors([Open(1, 2, true), Locked(2, 1), Open(3, 2)], 1, 2, 3);
        Check.Equal("1", string.Join(',', Glowing(advice)));
        Check.Equal("1 best", advice.Summary);
    }

    [Test]
    public void ChromaticReachDominates()
    {
        var advice = Doors([Open(1, 4, true), Locked(2), Locked(3)], 1, 2, 3);
        Check.Equal("1", string.Join(',', Glowing(advice)));
        Check.Equal(202.5, advice.Options[0].Value.Low);
    }

    [Test]
    public void PastTheChromaticCapReachingItIsWorthNothing()
    {
        var cells = new[] { Open(1, 4, true), Locked(2), Locked(3) };
        var capped = Advisor.Advise(State(Choice(TartarusChoiceKind.Doors, Door(1, 1)), cells) with { Chromatics = Defaults.ChromaticCap }, Defaults);
        Check.Equal(-81.0, capped.Options[0].Value.Low);
        var below = Advisor.Advise(State(Choice(TartarusChoiceKind.Doors, Door(1, 1)), cells) with { Chromatics = Defaults.ChromaticCap - 1 }, Defaults);
        Check.Equal(202.5, below.Options[0].Value.Low);
    }

    [Test]
    public void PastTheChromaticCapBestowCannotLiftAnUltimate()
    {
        var office = Choice(TartarusChoiceKind.Office, new TartarusOption(1, TartarusOptionKind.Bestow, 0, 0));
        var capped = Advisor.Advise(State(office, [Open(1, 4), Open(2, 2)]) with { Chromatics = Defaults.ChromaticCap }, Defaults);
        Check.Equal(0.0, capped.Options[0].Value.Low);
        Check.Equal(18.0, capped.Options[0].Value.High);
    }

    [Test]
    public void TwoCloseDoorsBothGlow()
    {
        var advice = Doors([Open(1, 2, true), Open(2, 2, true), Open(3, 2)], 1, 2, 3);
        Check.Equal("1,2", string.Join(',', Glowing(advice)));
        Check.Equal("2 best", advice.Summary);
    }

    [Test]
    public void ThreeEqualDoorsAreNoClearBest()
    {
        var advice = Doors([Open(1, 2, true), Open(2, 2, true), Open(3, 2, true)], 1, 2, 3);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal("no clear best", advice.Summary);
    }

    [Test]
    public void UnknownLockedDoorIsWide()
    {
        var advice = Doors([Locked(1), Locked(2)], 1, 2);
        Check.Equal(0, Glowing(advice).Length);
        Check.That(advice.Summary.StartsWith("unsure"), advice.Summary);
    }

    [Test]
    public void IncompleteChoiceNeverGlows()
    {
        var choice = new TartarusChoice(1, TartarusChoiceKind.Doors, [Door(1, 1), Door(2, 2)], false);
        var advice = Advisor.Advise(State(choice, [Open(1, 2, true), Open(2, 2)]), Defaults);
        Check.Equal(0, Glowing(advice).Length);
        Check.That(advice.Summary.StartsWith("unsure"), advice.Summary);
    }

    [Test]
    public void BestowIsARangeOverTheInmatesItCouldLift()
    {
        var cells = new[] { Open(1, 4), Open(2, 0), Open(3, 0), Open(4, 0) };
        var choice = Choice(TartarusChoiceKind.Office, new TartarusOption(1, TartarusOptionKind.Instigate, 0, 0), new TartarusOption(2, TartarusOptionKind.Bestow, 0, 0));
        var advice = Advisor.Advise(State(choice, cells), Defaults);
        Check.Equal(new ValueRange(2, 283.5), advice.Options[1].Value);
        Check.Equal(new ValueRange(0, 27), advice.Options[0].Value);
        Check.Equal(0, Glowing(advice).Length);
    }

    [Test]
    public void OfficeDoorIsNotAPointValueSoItDoesNotOutglowADevourDoor()
    {
        var cells = new[] { Open(1, 4), Open(2, 0), Open(3, 0), Open(4, 3, true) };
        var advice = Doors(cells, 0, 4);
        Check.Equal(new ValueRange(0, 283.5), advice.Options[0].Value);
        Check.That(!Glowing(advice).SequenceEqual([1]), "office door alone glows");
    }

    [Test]
    public void ConsumeTargetRangesOverlapSoBothGlow()
    {
        var choice = Choice(TartarusChoiceKind.Operation,
            new TartarusOption(1, TartarusOptionKind.Consume, 2, 0), new TartarusOption(2, TartarusOptionKind.Consume, 3, 0), new TartarusOption(3, TartarusOptionKind.End, 0, 0));
        var advice = Advisor.Advise(State(choice, [Open(1, 2), Open(2, 1), Open(3, 0)]), Defaults);
        Check.Equal(15.0, advice.Options[0].Value.Low);
        Check.Equal(17.0, advice.Options[1].Value.Low);
        Check.Equal("1,2", string.Join(',', Glowing(advice)));
    }

    [Test]
    public void TierZeroEaterReplaceAndEndNeverGlow()
    {
        var choice = Choice(TartarusChoiceKind.Operation, new TartarusOption(1, TartarusOptionKind.Replace, 0, 0), new TartarusOption(2, TartarusOptionKind.End, 0, 0));
        var advice = Advisor.Advise(State(choice, [Open(1, 0)]), Defaults);
        Check.Equal(new ValueRange(0, 2), advice.Options[0].Value);
        Check.Equal(new ValueRange(0, 0), advice.Options[1].Value);
        Check.Equal(0, Glowing(advice).Length);
        Check.Equal("unsure: wide ranges", advice.Summary);
    }

    [Test]
    public void InstrumentAndRewardChoicesNeverGlow()
    {
        foreach (var kind in new[] { TartarusChoiceKind.Instrument, TartarusChoiceKind.Reward })
        {
            var advice = Advisor.Advise(State(Choice(kind, new TartarusOption(1, TartarusOptionKind.Instrument, 0, 0), new TartarusOption(2, TartarusOptionKind.Reward, 0, 0)), [Open(1, 2)]), Defaults);
            Check.Equal(2, advice.Options.Length);
            Check.Equal(0, Glowing(advice).Length);
            Check.Equal("unsure: unmeasured", advice.Summary);
        }
    }

    [Test]
    public void EventChoiceHasNoOptions()
    {
        var advice = Advisor.Advise(State(Choice(TartarusChoiceKind.Event, new TartarusOption(1, TartarusOptionKind.Confirm, 0, 0)), [Open(1, 2)]), Defaults);
        Check.Equal(0, advice.Options.Length);
    }

    [Test]
    public void HorizonDampsUpsideLate()
    {
        var choice = Choice(TartarusChoiceKind.Doors, Door(1, 1));
        var cells = new[] { Open(1, 2, true) };
        var early = Advisor.Advise(State(choice, cells, day: 5), Defaults).Options[0].Value;
        var lastDay = Advisor.Advise(State(choice, cells, day: 20), Defaults).Options[0].Value;
        var overrun = Advisor.Advise(State(choice, cells, day: 21), Defaults).Options[0].Value;
        Check.Equal(new ValueRange(9, 18), early);
        Check.Equal(new ValueRange(9, 12), lastDay);
        Check.Equal(new ValueRange(9, 9), overrun);
    }

    [Test]
    public void HorizonNeverDividesByZero()
    {
        var settings = new Settings { StandardDays = 1, LateDays = 0 };
        var choice = Choice(TartarusChoiceKind.Doors, Door(1, 1));
        var value = Advisor.Advise(State(choice, [Open(1, 2, true)], day: 1, maxDay: 1), settings).Options[0].Value;
        Check.Equal(new ValueRange(9, 18), value);
        settings.LateDays = -1;
        var guarded = Advisor.Advise(State(choice, [Open(1, 2, true)], day: 1, maxDay: 1), settings).Options[0].Value;
        Check.That(double.IsFinite(guarded.High), guarded.ToString());
        foreach (var lateDays in new[] { -2, -1, 0 })
        {
            settings.LateDays = lateDays;
            var overrun = Advisor.Advise(State(choice, [Open(1, 2, true)], day: 4, maxDay: 1), settings).Options[0].Value;
            Check.Equal(new ValueRange(9, 9), overrun, $"LateDays {lateDays}");
        }
    }

    [Test]
    public void HalfLoadedInmateMakesBestowUnsure()
    {
        var cells = new[] { Open(1, 4), Open(2, -1) };
        var office = Advisor.Advise(State(Choice(TartarusChoiceKind.Office, new TartarusOption(1, TartarusOptionKind.Instigate, 0, 0), new TartarusOption(2, TartarusOptionKind.Bestow, 0, 0)), cells), Defaults);
        Check.Equal(0, Glowing(office).Length);
        Check.Equal("unsure: unknown tier", office.Summary);
        var door = Doors(cells.Append(Open(3, 2, true)).ToArray(), 0, 3);
        Check.Equal(0, Glowing(door).Length);
        Check.Equal("unsure: unknown tier", door.Summary);
    }

    [Test]
    public void EmptyOrNullPriorsAreUnsure()
    {
        foreach (var prior in new double[]?[] { [], null })
        {
            var settings = new Settings { DoorPrior = prior!, ReplacePrior = prior! };
            var doors = Advisor.Advise(State(Choice(TartarusChoiceKind.Doors, Door(1, 1), Door(2, 2)), [Locked(1), Open(2, 2, true)]), settings);
            Check.Equal(0, Glowing(doors).Length);
            Check.Equal("unsure: unknown tier", doors.Summary);
            var operation = Advisor.Advise(State(Choice(TartarusChoiceKind.Operation, new TartarusOption(1, TartarusOptionKind.Replace, 0, 0), new TartarusOption(2, TartarusOptionKind.End, 0, 0)), [Open(1, 0)]), settings);
            Check.Equal(0, Glowing(operation).Length);
            Check.Equal("unsure: unknown tier", operation.Summary);
        }
    }

    [Test]
    public void NonStandardDayCountIsUnsure()
    {
        var choice = Choice(TartarusChoiceKind.Doors, Door(1, 1), Door(2, 2));
        var cells = new[] { Open(1, 2, true), Open(2, 0) };
        Check.Equal("1", string.Join(',', Glowing(Advisor.Advise(State(choice, cells), Defaults))));
        var other = Advisor.Advise(State(choice, cells, maxDay: 30), Defaults);
        Check.Equal(0, Glowing(other).Length);
        Check.Equal("unsure: unmeasured mode", other.Summary);
    }

    [Test]
    public void PercentileTierFollowsTheCumulativeShare()
    {
        double[] prior = [0.418, 0.359, 0.114, 0.092, 0.018, 0];
        Check.Equal(0, Advisor.PercentileTier(prior, 0.25));
        Check.Equal(1, Advisor.PercentileTier(prior, 0.75));
        Check.Equal(2, Advisor.PercentileTier(prior, 0.8));
    }
}
