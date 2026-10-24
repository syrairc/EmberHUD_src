using System;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace EmberAuras;

public sealed class SettingsUiTests
{
    static Settings TwoGroups() => new() { Groups = [new Group { Name = "a" }, new Group { Name = "b" }] };

    static Settings AllKinds()
    {
        var conditions = Enum.GetValues<ConditionKind>().Select(kind => new Condition { Kind = kind, Text = "x", MinStacks = 2 }).ToList();
        var settings = new Settings { Groups = [new Group { Name = "g", Auras = [new Aura { Name = "icon", Conditions = conditions }, new Aura { Name = "bar", Style = AuraStyle.Bar }] }] };
        settings.Normalize();
        return settings;
    }

    static int Draw(Settings settings) => TestFrame.Vertices(settings.Draw, inWindow: true);

    [Test]
    public void AddGroupSelectsNewLast()
    {
        var settings = TwoGroups();
        Check.Equal(2, SettingsUI.AddGroup(settings));
        Check.Equal(3, settings.Groups.Count);
    }

    [Test]
    public void DeleteGroupClampsSelection()
    {
        var settings = TwoGroups();
        var selected = 1;
        SettingsUI.DeleteGroup(settings, ref selected);
        Check.Equal(1, settings.Groups.Count);
        Check.Equal(0, selected);
    }

    [Test]
    public void DeleteLastGroupLeavesEmptyList()
    {
        var settings = TwoGroups();
        var selected = 0;
        SettingsUI.DeleteGroup(settings, ref selected);
        SettingsUI.DeleteGroup(settings, ref selected);
        SettingsUI.DeleteGroup(settings, ref selected);
        Check.Equal(0, settings.Groups.Count);
        Check.Equal(0, selected);
    }

    [Test]
    public void AddAuraCreatesAllTextFields()
    {
        var group = new Group();
        Check.Equal(0, SettingsUI.AddAura(group));
        Check.Equal(Enum.GetValues<TextField>().Length, group.Auras[0].Texts.Count);
        var selected = 0;
        SettingsUI.DeleteAura(group, ref selected);
        Check.Equal(0, group.Auras.Count);
    }

    [Test]
    public void AddExactIdAppends()
    {
        Check.Equal("^5$", SettingsUI.AddExactId("", 5));
        Check.Equal("a|^5$", SettingsUI.AddExactId("a", 5));
    }

    [Test]
    public void EmptyAndFullSettingsDraw()
    {
        TestGame.Set(GameState.Empty);
        Check.That(Draw(new Settings()) > 0);
        SettingsUI.Select(0, 0);
        Check.That(Draw(AllKinds()) > 0);
        SettingsUI.Select(50, 50);
        Check.That(Draw(AllKinds()) > 0);
    }

    [Test]
    public void NewConditionIsHealth() => Check.Equal(ConditionKind.Health, SettingsUI.NewCondition().Kind);
}
