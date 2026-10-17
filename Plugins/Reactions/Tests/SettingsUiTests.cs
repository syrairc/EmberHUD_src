using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Reactions;

public sealed class SettingsUiTests
{
    static Settings AllKinds() => new()
    {
        Reactions =
        [
            new Reaction { Name = "Heal", Trigger = new Hotkey(Key.D1), Conditions = [new Condition { Kind = ConditionKind.Health }, new Condition { Kind = ConditionKind.Shield }, new Condition { Kind = ConditionKind.HealthAndShield }] },
            new Reaction { Name = "Buff", Conditions = [new Condition { Kind = ConditionKind.Buff, Text = "shield|1234" }, new Condition { Kind = ConditionKind.Buff, Missing = true, Text = "(" }, new Condition { Kind = ConditionKind.Buff, Ending = true, Text = "x" }] },
            new Reaction { Name = "Skills", Conditions = [new Condition { Kind = ConditionKind.SkillCooldown }, new Condition { Kind = ConditionKind.SkillCharges }, new Condition { Kind = ConditionKind.NearbyEnemies, MinRarity = Rarity.Rare }] },
        ],
    };

    static GameState OnBar(params int[] cfgIds) => new() { Skills = [.. cfgIds.Select((id, slot) => new Skill(id, slot + 1, 0, 0, 0, 0))] };

    static int Draw(Settings settings) => TestFrame.Vertices(settings.Draw, inWindow: true);

    [Test]
    public void EmptySettingsDraw()
    {
        TestGame.Set(GameState.Empty);
        Check.That(Draw(new Settings()) > 0);
    }

    [Test]
    public void EveryConditionKindDraws()
    {
        TestGame.Set(OnBar(10, 20));
        var settings = AllKinds();
        for (var i = 0; i < settings.Reactions.Count; i++)
        {
            SettingsUI.Select(i);
            Check.That(Draw(settings) > 0, $"reaction {i}");
        }
    }

    [Test]
    public void NullRiddenSettingsDrawWithoutThrowing()
    {
        TestGame.Set(new GameState { Skills = null });
        var settings = new Settings { Reactions = [null!, new Reaction { Name = null!, Conditions = null! }, new Reaction { Conditions = [null!, new Condition { Kind = ConditionKind.Buff, Text = null! }, new Condition { Kind = (ConditionKind)99 }] }] };
        for (var i = 0; i < 3; i++)
        {
            SettingsUI.Select(i);
            Check.That(Draw(settings) > 0);
        }
        Check.That(Draw(new Settings { Reactions = null! }) > 0);
    }

    [Test]
    public void SelectionPastTheEndIsClamped()
    {
        TestGame.Set(GameState.Empty);
        SettingsUI.Select(50);
        Check.That(Draw(AllKinds()) > 0);
    }

    [Test]
    public void AddReactionAppendsEnabledNamedReaction()
    {
        var settings = new Settings();
        var index = SettingsUI.AddReaction(settings);
        Check.Equal(0, index);
        Check.That(settings.Reactions[0].Enabled && settings.Reactions[0].Name == "Reaction 1" && settings.Reactions[0].Conditions.Count == 0);
        Check.Equal(1, SettingsUI.AddReaction(settings));
    }

    [Test]
    public void AddAndDeleteConditionChangeTheList()
    {
        var reaction = new Reaction();
        var added = SettingsUI.NewCondition();
        reaction.Conditions.Add(added);
        reaction.Conditions.Add(SettingsUI.NewCondition());
        Check.Equal(2, reaction.Conditions.Count);
        Check.Equal(ConditionKind.Health, added.Kind);
        reaction.Conditions.Remove(added);
        Check.Equal(1, reaction.Conditions.Count);
    }

    [Test]
    public void DeleteReactionKeepsSelectionInRange()
    {
        var settings = AllKinds();
        var selected = 2;
        SettingsUI.DeleteReaction(settings, ref selected);
        Check.Equal(2, settings.Reactions.Count);
        Check.Equal(1, selected);
        SettingsUI.DeleteReaction(settings, ref selected);
        SettingsUI.DeleteReaction(settings, ref selected);
        SettingsUI.DeleteReaction(settings, ref selected);
        Check.Equal(0, settings.Reactions.Count);
        Check.Equal(0, selected);
    }

    [Test]
    public void SkillMissingFromBarIsFlagged()
    {
        TestGame.Set(OnBar(10, 20));
        Check.That(!SettingsUI.SkillMissing(10));
        Check.That(SettingsUI.SkillMissing(99));
        TestGame.Set(new GameState { Skills = [new Skill(10, -1, 0, 0, 0, 0)] });
        Check.That(SettingsUI.SkillMissing(10));
        TestGame.Set(OnBar(10, 20));
        TestGame.Set(new GameState { Skills = null });
        Check.That(SettingsUI.SkillMissing(10));
    }

    [Test]
    public void TheSkillPickerAsksForCharacterData()
    {
        var asked = typeof(Game).GetProperty("CharacterAsked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var settings = new Settings { Reactions = [new Reaction { Name = "r", Conditions = [new Condition { Kind = ConditionKind.SkillCooldown }] }] };
        SettingsUI.Select(0);
        TestGame.Set(GameState.Empty);
        asked.SetValue(null, 0L);
        TestFrame.Run(settings.Draw);
        Check.That((long)asked.GetValue(null)! != 0, "WantCharacter was not called");
    }

    [Test]
    public void MissingSkillDrawsTheWarningRow()
    {
        var settings = new Settings { Reactions = [new Reaction { Name = "r", Conditions = [new Condition { Kind = ConditionKind.SkillCooldown, SkillCfgId = 10 }] }] };
        SettingsUI.Select(0);
        TestGame.Set(OnBar(20, 30));
        Check.That(SettingsUI.SkillMissing(10));
        TestFrame.Run(settings.Draw);
        TestGame.Set(new GameState { Skills = null });
        TestFrame.Run(settings.Draw);
    }

    [Test]
    public void PickedBuffIdIsAddedAsExactMatch()
    {
        Check.Equal("^1234$", SettingsUI.AddExactId("", 1234));
        Check.Equal("shield|^1234$", SettingsUI.AddExactId("shield", 1234));
        Check.That(Conditions.BuffMatches(SettingsUI.AddExactId("", 12), new Buff(12, "x", 0, 0)));
        Check.That(!Conditions.BuffMatches(SettingsUI.AddExactId("", 12), new Buff(123, "x", 0, 0)));
    }
}
