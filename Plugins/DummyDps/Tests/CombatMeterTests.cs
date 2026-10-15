using System;
using EmberHUD.Testing;

namespace DummyDpsPlugin;

public sealed class CombatMeterTests
{
    static void Near(double expected, double actual) => Check.Equal(Math.Round(expected, 3), Math.Round(actual, 3));

    static CombatMeter Fed(params (double Seconds, double Total)[] samples)
    {
        var meter = new CombatMeter();
        foreach (var (seconds, total) in samples) meter.Add(seconds, total, 1, 5);
        return meter;
    }

    [Test]
    public void NothingUntilDamageArrives()
    {
        var meter = Fed((0, 0), (1, 0));
        Check.That(!meter.HasFight, "no damage, no fight");
    }

    [Test]
    public void FirstDamageStartsAFight()
    {
        var meter = Fed((0, 0), (1, 1000));
        Check.That(meter.HasFight, "fight started");
        Near(1000, meter.Total);
        Near(1000, meter.Dps);
        Check.That(meter.Active, "active");
    }

    [Test]
    public void TheWindowSlidesPastTheFightStart()
    {
        var meter = new CombatMeter();
        meter.Add(0, 0, 1, 5);
        for (var t = 1; t <= 5; t++) meter.Add(t, 1000 * t, 1, 5);
        meter.Add(6, 6000, 1, 5);
        Near(1200, meter.Dps);
        meter.Add(7, 7000, 1, 5);
        Near(1000, meter.Dps);
    }

    [Test]
    public void DpsIsTheDamageInTheWindowOverTheWindow()
    {
        var meter = new CombatMeter();
        meter.Add(0, 0, 1, 5);
        for (var t = 1; t <= 10; t++) meter.Add(t, 1000 * t, 1, 5);
        Near(1000, meter.Dps);
    }

    [Test]
    public void ShortFightsDivideByAtLeastOneSecond()
    {
        var meter = Fed((0, 0), (0.1, 500));
        Near(500, meter.Dps);
        Check.That(double.IsFinite(meter.Dps), "finite");
    }

    [Test]
    public void PeakKeepsTheHighestDpsOfTheFight()
    {
        var meter = Fed((0, 0), (1, 5000), (2, 5000), (3, 5000));
        Near(5000, meter.Peak);
        Check.That(meter.Dps < meter.Peak, "dps dropped below the peak");
    }

    [Test]
    public void AverageIsTotalOverTheFightDuration()
    {
        var meter = Fed((0, 0), (0.5, 0), (1, 1000), (2, 2000), (4, 3000));
        Near(1000, meter.Average);
        Check.Equal(3, meter.FightSeconds);
    }

    [Test]
    public void IdleLongerThanFiveSecondsStartsANewFight()
    {
        var meter = Fed((0, 0), (1, 5000), (5, 5000), (20, 5000), (21, 5200));
        Near(200, meter.Total);
        Near(200, meter.Peak);
    }

    [Test]
    public void AreaChangeResets()
    {
        var meter = Fed((0, 0), (1, 1000));
        meter.Add(30, 0, 2, 5);
        Check.That(!meter.HasFight, "new area clears the fight");
    }

    [Test]
    public void ADecreasingTotalResets()
    {
        var meter = Fed((0, 0), (5, 5000));
        meter.Add(6, 100, 1, 5);
        Check.That(!meter.HasFight, "lower total clears the fight");
    }

    [Test]
    public void ActiveTurnsFalseAfterTheIdleGap()
    {
        var meter = Fed((0, 0), (1, 1000));
        meter.Add(5, 1000, 1, 5);
        Check.That(meter.Active, "still active four seconds after the last hit");
        meter.Add(7, 1000, 1, 5);
        Check.That(!meter.Active, "idle past the gap");
        Check.That(meter.HasFight, "fight kept");
        Near(1000, meter.Total);
    }

    [Test]
    public void RepeatedSecondsDoNotGrowTheSamples()
    {
        var meter = Fed((0, 0), (1, 1000));
        var before = meter.SampleCount;
        for (var i = 0; i < 1000; i++) meter.Add(1, 1000, 1, 5);
        Check.Equal(before, meter.SampleCount);
    }
}
