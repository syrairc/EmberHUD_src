using System.Collections.Generic;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Reactions;

public sealed class ConditionsTests
{
    static GameState Pools(double life, double maxLife, double shield = 0, double maxShield = 0) =>
        new() { Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = life, MaxLife = maxLife, Shield = shield, MaxShield = maxShield } };

    static GameState WithBuffs(params Buff[] buffs) => new() { Buffs = buffs };

    static GameState WithSkills(params Skill[] skills) => new() { Skills = skills };

    [Test]
    public void HealthBelowPercentIsTrue() =>
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.Health, Percent = 50 }, Pools(49, 100)));

    [Test]
    public void HealthAtPercentIsFalse() =>
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Health, Percent = 50 }, Pools(50, 100)));

    [Test]
    public void ShieldBelowPercentIsTrueAndAboveIsFalse()
    {
        var condition = new Condition { Kind = ConditionKind.Shield, Percent = 30 };
        Check.That(Conditions.Met(condition, Pools(100, 100, 20, 100)));
        Check.That(!Conditions.Met(condition, Pools(0, 100, 40, 100)));
    }

    [Test]
    public void CombinedPercentUsesSummedPools()
    {
        var condition = new Condition { Kind = ConditionKind.HealthAndShield, Percent = 50 };
        Check.That(Conditions.Met(condition, Pools(10, 100, 80, 100)));
        Check.That(!Conditions.Met(condition, Pools(90, 100, 20, 100)));
    }

    [Test]
    public void ZeroMaxLifeIsFalse()
    {
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Health, Percent = 50 }, Pools(0, 0)));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Shield, Percent = 50 }, Pools(0, 100)));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.HealthAndShield, Percent = 50 }, Pools(0, 0)));
    }

    [Test]
    public void NanAndNoPlayerAreFalse()
    {
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Health, Percent = 50 }, Pools(double.NaN, 100)));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Health, Percent = 50 }, new GameState()));
    }

    [Test]
    public void BuffPresentIsTrueWhenAnyMatches()
    {
        var condition = new Condition { Kind = ConditionKind.Buff, Text = "Euph" };
        Check.That(Conditions.Met(condition, WithBuffs(new Buff(1, "Other", 1, 1), new Buff(2, "Euphoria", 1, 1))));
        Check.That(!Conditions.Met(condition, WithBuffs(new Buff(1, "Other", 1, 1))));
    }

    [Test]
    public void MissingIsTrueWhenNoBuffMatches()
    {
        var condition = new Condition { Kind = ConditionKind.Buff, Text = "Euph", Missing = true };
        Check.That(Conditions.Met(condition, WithBuffs(new Buff(1, "Other", 1, 1))));
        Check.That(!Conditions.Met(condition, WithBuffs(new Buff(2, "Euphoria", 1, 1))));
    }

    [Test]
    public void MissingIsFalseWhenBuffsAreNull()
    {
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Buff, Text = "Euph", Missing = true }, new GameState()));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Buff, Text = "Euph" }, new GameState()));
    }

    [Test]
    public void EmptyOrNullBuffTextIsFalseInBothModes()
    {
        foreach (var text in new[] { "", null! })
            foreach (var missing in new[] { false, true })
            {
                var condition = new Condition { Kind = ConditionKind.Buff, Text = text, Missing = missing };
                Check.That(!Conditions.Met(condition, WithBuffs()));
                Check.That(!Conditions.Met(condition, WithBuffs(new Buff(1, "Euphoria", 1, 1))));
            }
    }

    [Test]
    public void EndingIsTrueWhenTheLongestMatchIsUnderTheSecondsOrGone()
    {
        var condition = new Condition { Kind = ConditionKind.Buff, Text = "Euph", Ending = true, Seconds = 1 };
        Check.That(Conditions.Met(condition, WithBuffs(new Buff(1, "Euphoria", 0.9, 4))));
        Check.That(!Conditions.Met(condition, WithBuffs(new Buff(1, "Euphoria", 1.5, 4))));
        Check.That(!Conditions.Met(condition, WithBuffs(new Buff(1, "Euphoria", 0.5, 4), new Buff(1, "Euphoria", 3, 4))));
        Check.That(Conditions.Met(condition, WithBuffs(new Buff(2, "Other", 0.5, 4))), "gone counts as 0 s left");
        Check.That(Conditions.Met(condition, WithBuffs()));
        Check.That(!Conditions.Met(condition, new GameState()), "unread buffs never fire");
    }

    [Test]
    public void EndingIgnoresUntimedBuffs() =>
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.Buff, Text = "Euph", Ending = true, Seconds = 2 }, WithBuffs(new Buff(1, "Euphoria", 1, 1))));

    [Test]
    public void BuffMatchesNameByRegex() =>
        Check.That(Conditions.BuffMatches("Euph.*", new Buff(1, "Euphoria", 1, 1)));

    [Test]
    public void BuffMatchesIdByRegex()
    {
        Check.That(Conditions.BuffMatches("^320$", new Buff(320, null, 1, 1)));
        Check.That(!Conditions.BuffMatches("^320$", new Buff(3200, null, 1, 1)));
    }

    [Test]
    public void InvalidRegexFallsBackToExactMatch()
    {
        Check.That(Conditions.BuffMatches("(unclosed", new Buff(1, "(unclosed", 1, 1)));
        Check.That(!Conditions.BuffMatches("(unclosed", new Buff(1, "unclosed", 1, 1)));
    }

    [Test]
    public void EmptyTextNeverMatches()
    {
        Check.That(!Conditions.BuffMatches("", new Buff(1, "Euphoria", 1, 1)));
        Check.That(!Conditions.BuffMatches("", new Buff(1, null, 1, 1)));
    }

    [Test]
    public void CatastrophicRegexTimesOutWithoutThrowing()
    {
        var name = new string('a', 60) + "!";
        Check.That(!Conditions.BuffMatches("(a+)+$", new Buff(1, name, 1, 1)));
    }

    [Test]
    public void SkillCooldownComparesReady()
    {
        var state = WithSkills(new Skill(7, 0, 0, 5, 0, 0), new Skill(8, 1, 3, 5, 0, 0));
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 7, Ready = true }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 8, Ready = true }, state));
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 8, Ready = false }, state));
    }

    [Test]
    public void SkillNotReadyIsFalseWhenSkillsAreNull() =>
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 7, Ready = false }, new GameState()));

    [Test]
    public void SkillMissingFromListIsFalse()
    {
        var state = WithSkills(new Skill(7, 0, 0, 5, 0, 0));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 99, Ready = true }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCharges, SkillCfgId = 99, Charges = 1 }, state));
    }

    [Test]
    public void OffBarSkillFailsClosed()
    {
        var state = WithSkills(new Skill(7, -1, 0, 5, 3, 3));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 7, Ready = true }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 7, Ready = false }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCharges, SkillCfgId = 7, Charges = 1 }, state));
    }

    [Test]
    public void NearbyEnemiesCountZeroNeedsAtLeastOne() =>
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 50, Count = 0 }, new GameState { Player = new Player(Vector3.Zero, Vector2.UnitX), Entities = [] }));

    [Test]
    public void ChargesAtLeast()
    {
        var state = WithSkills(new Skill(7, 0, 0, 5, 2, 3));
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.SkillCharges, SkillCfgId = 7, Charges = 2 }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCharges, SkillCfgId = 7, Charges = 3 }, state));
    }

    [Test]
    public void ChargesConditionFalseOnSkillWithoutCharges() =>
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.SkillCharges, SkillCfgId = 7, Charges = 0 }, WithSkills(new Skill(7, 0, 0, 5, 0, 0))));

    [Test]
    public void NearbyEnemiesUsesCountNear()
    {
        var state = new GameState
        {
            Player = new Player(Vector3.Zero, Vector2.UnitX),
            Entities =
            [
                new Entity(1, 100, new Vector3(10, 0, 0), EntityKind.Monster, Rarity.Rare) { Address = 0x1000 },
                new Entity(2, 100, new Vector3(20, 0, 0), EntityKind.Monster, Rarity.Normal) { Address = 0x2000 },
                new Entity(3, 100, new Vector3(900, 0, 0), EntityKind.Monster, Rarity.Rare) { Address = 0x3000 },
            ],
        };
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 50, Count = 2 }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 50, Count = 2, MinRarity = Rarity.Rare }, state));
    }

    [Test]
    public void UnknownKindIsFalse() =>
        Check.That(!Conditions.Met(new Condition { Kind = (ConditionKind)99 }, Pools(0, 100)));

    [Test]
    public void EmptyConditionListIsNeverMet() =>
        Check.That(!Conditions.AllMet(new Reaction { Conditions = [] }, Pools(0, 100)));

    [Test]
    public void AllMetNeedsEveryCondition()
    {
        var reaction = new Reaction
        {
            Conditions = [new Condition { Kind = ConditionKind.Health, Percent = 50 }, new Condition { Kind = ConditionKind.Buff, Text = "Euph" }],
        };
        var state = Pools(10, 100) with { Buffs = [new Buff(1, "Euphoria", 1, 1)] };
        Check.That(Conditions.AllMet(reaction, state));
        Check.That(!Conditions.AllMet(reaction, state with { Buffs = [] }));
    }

    [Test]
    public void NormalizeRepairsNullsFromHandEditedJson()
    {
        var settings = new Settings
        {
            Reactions = [null!, new Reaction { Name = null!, Conditions = null! }, new Reaction { Name = "x", Conditions = [null!, new Condition { Text = null! }] }],
        };
        settings.Normalize();
        Check.Equal(2, settings.Reactions.Count);
        Check.Equal("", settings.Reactions[0].Name);
        Check.Equal(0, settings.Reactions[0].Conditions.Count);
        Check.Equal(1, settings.Reactions[1].Conditions.Count);
        Check.Equal("", settings.Reactions[1].Conditions[0].Text);
        var nullList = new Settings { Reactions = null! };
        nullList.Normalize();
        Check.Equal(0, nullList.Reactions.Count);
    }
}
