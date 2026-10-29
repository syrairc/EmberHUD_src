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
    public void NearbyEnemiesSkipsMinionArchetype()
    {
        var state = new GameState
        {
            Player = new Player(Vector3.Zero, Vector2.UnitX),
            Entities =
            [
                new Entity(1, 100, new Vector3(10, 0, 0), EntityKind.Monster, Rarity.Normal) { Address = 0x1000, Info = new FightInfo(FightArchetype.Minion, 2, 1, 0) },
                new Entity(2, 100, new Vector3(20, 0, 0), EntityKind.Monster, Rarity.Normal) { Address = 0x2000, Info = new FightInfo(FightArchetype.Monster, 1, 1, 0) },
            ],
        };
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 50, Count = 1 }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 50, Count = 2 }, state));
    }

    static Projectile Shot(int id, float x) => new(id, 0, Rarity.Normal, new Vector2(x, 0), Vector2.Zero, Vector2.UnitX, 5, 100);

    static FightEntity Fighter(int id, bool hasTarget, int targetId = 0) =>
        new(id, Vector3.Zero, Vector2.UnitX, FightArchetype.Bullet, []) { Track = new FightTrack(hasTarget, targetId, false, false, 0) };

    static FightEntity PlayerFighter(int id, float x = 0) =>
        new(id, new Vector3(x, 0, 0), Vector2.UnitX, FightArchetype.Player, []);

    static GameState Homing(Projectile[] projectiles, FightEntity[]? fighters) =>
        new() { Player = new Player(Vector3.Zero, Vector2.UnitX), Projectiles = projectiles, Fighters = fighters };

    [Test]
    public void HomingCountsTargetingProjectilesInRadius()
    {
        var state = Homing([Shot(1, 10), Shot(2, 20), Shot(3, 30), Shot(4, 900)], [Fighter(1, true), Fighter(2, true), Fighter(3, false), Fighter(4, true)]);
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100, Count = 2 }, state));
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100, Count = 3 }, state), "untargeting and far ones do not count");
        Check.That(!Conditions.Met(new Condition { Kind = ConditionKind.HomingProjectile, Radius = 15, Count = 2 }, state), "radius");
        Check.That(Conditions.Met(new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100, Count = 0 }, state), "count 0 needs one");
    }

    [Test]
    public void HomingWithoutFighterOrFightersIsFalse()
    {
        var condition = new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100 };
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(9, true)])), "no fighter with the projectile id");
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], null)), "fighters unread");
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [new FightEntity(1, Vector3.Zero, Vector2.UnitX, FightArchetype.Bullet, [])])), "no track");
        Check.That(!Conditions.Met(condition, new GameState { Projectiles = [Shot(1, 10)], Fighters = [Fighter(1, true)] }), "no player");
    }

    [Test]
    public void HomingOnlyTargetingMeNeedsThePlayerFighter()
    {
        var condition = new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100, OnlyTargetingMe = true };
        Check.That(Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 77), PlayerFighter(77, 5)])));
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 88), PlayerFighter(77, 5)])), "targets someone else");
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 77)])), "player fighter missing");
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 77), PlayerFighter(77, 500)])), "player fighter too far");
        Check.That(!Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 0), PlayerFighter(77, 5)])), "target 0 is not the player");
    }

    [Test]
    public void HomingOnlyTargetingMePicksTheNearestPlayerFighter()
    {
        var condition = new Condition { Kind = ConditionKind.HomingProjectile, Radius = 100, OnlyTargetingMe = true };
        Check.That(Conditions.Met(condition, Homing([Shot(1, 10)], [Fighter(1, true, 77), PlayerFighter(88, 40), PlayerFighter(77, 5)])));
    }

    static GameState InHazard(params GroundHazard[] hazards) =>
        new() { Player = new Player(new Vector3(10, 0, 0), Vector2.UnitX), GroundHazards = hazards };

    [Test]
    public void GroundEffectIsMetInsideAHazard()
    {
        var condition = new Condition { Kind = ConditionKind.GroundEffect };
        Check.That(Conditions.Met(condition, InHazard(new GroundHazard(1, HazardKind.Area, new Vector2(0, 0), 10))), "edge counts");
        Check.That(!Conditions.Met(condition, InHazard(new GroundHazard(1, HazardKind.Area, new Vector2(0, 0), 9))));
        Check.That(!Conditions.Met(condition, InHazard()));
        Check.That(!Conditions.Met(condition, new GameState { GroundHazards = [new GroundHazard(1, HazardKind.Area, Vector2.Zero, 99)] }), "no player");
    }

    [Test]
    public void GroundEffectInnerRadiusIsAHole()
    {
        var condition = new Condition { Kind = ConditionKind.GroundEffect };
        Check.That(!Conditions.Met(condition, InHazard(new GroundHazard(1, HazardKind.Damage, Vector2.Zero, 50) { InnerRadius = 20 })));
        Check.That(Conditions.Met(condition, InHazard(new GroundHazard(1, HazardKind.Damage, Vector2.Zero, 50) { InnerRadius = 5 })));
    }

    [Test]
    public void GroundEffectDamageOnlyIgnoresAreas()
    {
        var area = new GroundHazard(1, HazardKind.Area, Vector2.Zero, 50);
        var damage = new GroundHazard(2, HazardKind.Damage, Vector2.Zero, 50);
        var condition = new Condition { Kind = ConditionKind.GroundEffect, DamageOnly = true };
        Check.That(!Conditions.Met(condition, InHazard(area)));
        Check.That(Conditions.Met(condition, InHazard(area, damage)));
    }

    [Test]
    public void OldNumericKindsKeepTheirMeaning()
    {
        Check.Equal(6, (int)ConditionKind.NearbyEnemies);
        Check.Equal(7, (int)ConditionKind.HomingProjectile);
        Check.Equal(8, (int)ConditionKind.GroundEffect);
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
