using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace EmberAuras;

public sealed class AuraEvalTests
{
    static GameState Pools(double life, double maxLife, double shield = 0, double maxShield = 0) =>
        new() { Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = life, MaxLife = maxLife, Shield = shield, MaxShield = maxShield } };

    static Aura BuffAura(string text = "Euph", bool missing = false, WhenInactive whenInactive = WhenInactive.Hide) => new()
    {
        WhenInactive = whenInactive,
        Conditions = [new Condition { Kind = ConditionKind.Buff, Text = text, Missing = missing }],
    };

    static GameState WithBuff(double remaining, double total, int stacks = 1) =>
        new() { Buffs = [new Buff(5, "Euphoria", remaining, total, null, stacks)] };

    static Aura SkillAura(ConditionKind kind = ConditionKind.SkillCooldown) =>
        new() { Conditions = [new Condition { Kind = kind, SkillCfgId = 7, Ready = false, Charges = 1 }] };

    static Aura LifeAura() => new() { Conditions = [new Condition { Kind = ConditionKind.Health, Percent = 100 }] };

    [Test]
    public void DisabledAuraIsHidden()
    {
        var aura = BuffAura();
        aura.Enabled = false;
        Check.That(AuraEval.Evaluate(aura, WithBuff(2, 8)).Visibility == Visibility.Hidden);
    }

    [Test]
    public void MetAuraIsShown() =>
        Check.That(AuraEval.Evaluate(BuffAura(), WithBuff(2, 8)).Visibility == Visibility.Shown);

    [Test]
    public void UnmetHideIsHidden() =>
        Check.That(AuraEval.Evaluate(BuffAura(), new GameState { Buffs = [] }).Visibility == Visibility.Hidden);

    [Test]
    public void UnmetDimIsDim()
    {
        var view = AuraEval.Evaluate(BuffAura("Euph", true, WhenInactive.Dim), WithBuff(2, 8));
        Check.That(view.Visibility == Visibility.Dim);
        Check.Equal(0.25f, view.Fill, "a dimmed aura keeps real values");
    }

    [Test]
    public void BuffTimedFillIsRemainingOverTotal()
    {
        var view = AuraEval.Evaluate(BuffAura(), WithBuff(2, 8));
        Check.Equal(0.25f, view.Fill);
        Check.Equal(5, view.BuffId);
        Check.Equal(2.0, view.Seconds);
    }

    [Test]
    public void UntimedBuffFillIsOne()
    {
        var view = AuraEval.Evaluate(BuffAura(), WithBuff(1, 1));
        Check.Equal(1f, view.Fill);
        Check.That(view.Seconds is null);
    }

    [Test]
    public void MissingBuffFillIsZero()
    {
        var view = AuraEval.Evaluate(BuffAura(), new GameState { Buffs = [] });
        Check.Equal(0f, view.Fill);
        Check.That(view.BuffId is null);
        Check.Equal(0, view.Stacks);
        Check.That(view.Seconds is null);
    }

    [Test]
    public void BuffWithZeroTotalFillIsZeroNotNaN()
    {
        Check.Equal(0f, AuraEval.Evaluate(BuffAura(), WithBuff(0, 0)).Fill);
        Check.Equal(0f, AuraEval.Evaluate(BuffAura(), WithBuff(double.NaN, 8)).Fill);
        Check.Equal(0f, AuraEval.Evaluate(BuffAura(), WithBuff(1, double.NaN)).Fill);
    }

    [Test]
    public void RemainingAboveTotalClampsToOne() =>
        Check.Equal(1f, AuraEval.Evaluate(BuffAura(), WithBuff(9, 8)).Fill);

    [Test]
    public void SkillCooldownFill()
    {
        var state = new GameState { Skills = [new Skill(7, 0, 3, 12, 0, 0)] };
        var view = AuraEval.Evaluate(SkillAura(), state);
        Check.Equal(0.75f, view.Fill);
        Check.Equal(7, view.SkillCfgId);
        Check.Equal(3.0, view.Seconds);
        Check.Equal(1f, AuraEval.Evaluate(SkillAura(), new GameState { Skills = [new Skill(7, 0, 0, 0, 0, 0)] }).Fill);
        Check.Equal(0f, AuraEval.Evaluate(SkillAura(), new GameState { Skills = [new Skill(7, 0, 20, 12, 0, 0)] }).Fill);
    }

    [Test]
    public void SkillChargesFill()
    {
        var state = new GameState { Skills = [new Skill(7, 0, 0, 5, 2, 4)] };
        var view = AuraEval.Evaluate(SkillAura(ConditionKind.SkillCharges), state);
        Check.Equal(0.5f, view.Fill);
        Check.Equal(2, view.Charges);
        Check.Equal(4, view.MaxCharges);
    }

    [Test]
    public void LifeFillIsFractionAndZeroMaxIsZero()
    {
        Check.Equal(0.25f, AuraEval.Evaluate(LifeAura(), Pools(25, 100)).Fill);
        Check.Equal(0f, AuraEval.Evaluate(LifeAura(), Pools(25, 0)).Fill);
        Check.Equal(0f, AuraEval.Evaluate(LifeAura(), Pools(double.NaN, 100)).Fill);
        Check.Equal(1f, AuraEval.Evaluate(LifeAura(), Pools(500, 100)).Fill);
        Check.Equal(0f, AuraEval.Evaluate(LifeAura(), new GameState()).Fill);
    }

    [Test]
    public void ShieldAndCombinedFill()
    {
        var shield = new Aura { Conditions = [new Condition { Kind = ConditionKind.Shield, Percent = 100 }] };
        var both = new Aura { Conditions = [new Condition { Kind = ConditionKind.HealthAndShield, Percent = 100 }] };
        Check.Equal(0.5f, AuraEval.Evaluate(shield, Pools(10, 100, 50, 100)).Fill);
        Check.Equal(0.3f, AuraEval.Evaluate(both, Pools(10, 100, 50, 100)).Fill, "(10 + 50) / 200");
    }

    [Test]
    public void NoConditionsIsHiddenWithZeroFill()
    {
        var view = AuraEval.Evaluate(new Aura { Conditions = [] }, Pools(10, 100));
        Check.That(view.Visibility == Visibility.Hidden);
        Check.Equal(0f, view.Fill);
    }

    [Test]
    public void StacksTextOnlyAboveOne()
    {
        Check.That(AuraEval.Evaluate(BuffAura(), WithBuff(2, 8, 1)).TextFor(TextField.Stacks) is null);
        Check.Equal("3", AuraEval.Evaluate(BuffAura(), WithBuff(2, 8, 3)).TextFor(TextField.Stacks));
    }

    [Test]
    public void DurationTextFormats()
    {
        Check.Equal("4.2", AuraEval.Evaluate(BuffAura(), WithBuff(4.2, 20)).TextFor(TextField.Duration));
        Check.Equal("12", AuraEval.Evaluate(BuffAura(), WithBuff(12.4, 20)).TextFor(TextField.Duration));
        Check.That(AuraEval.Evaluate(BuffAura(), WithBuff(1, 1)).TextFor(TextField.Duration) is null);
    }

    [Test]
    public void ChargesTextShowsOfMax()
    {
        var state = new GameState { Skills = [new Skill(7, 0, 0, 5, 2, 4)] };
        Check.Equal("2/4", AuraEval.Evaluate(SkillAura(ConditionKind.SkillCharges), state).TextFor(TextField.Charges));
        Check.That(AuraEval.Evaluate(BuffAura(), WithBuff(2, 8)).TextFor(TextField.Charges) is null);
    }

    [Test]
    public void PercentTextForLife()
    {
        Check.Equal("25%", AuraEval.Evaluate(LifeAura(), Pools(25, 100)).TextFor(TextField.Percent));
        Check.That(AuraEval.Evaluate(BuffAura(), WithBuff(2, 8)).TextFor(TextField.Percent) is null);
    }

    [Test]
    public void NameTextPrefersAuraName()
    {
        var aura = BuffAura();
        Check.Equal("Euphoria", AuraEval.Evaluate(aura, WithBuff(2, 8)).TextFor(TextField.Name));
        aura.Name = "Mine";
        Check.Equal("Mine", AuraEval.Evaluate(aura, WithBuff(2, 8)).TextFor(TextField.Name));
    }
}
