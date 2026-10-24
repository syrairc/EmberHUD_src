using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace EmberAuras;

public sealed class SettingsTests
{
    static readonly Type SettingsJson = typeof(PluginSettings).Assembly.GetType("EmberHUD.SettingsJson")!;

    static string Serialize(Settings settings) => (string)SettingsJson.GetMethod("Serialize")!.Invoke(null, [settings])!;

    static Settings Load(string json) => (Settings)SettingsJson.GetMethod("Load")!.Invoke(null, [typeof(Settings), null, json, new Action<string>(message => throw new Exception(message))])!;

    static Settings WithAura(Aura aura) => new() { Groups = [new Group { Auras = [aura] }] };

    [Test]
    public void NormalizeFillsMissingTextFields()
    {
        var settings = WithAura(new Aura { Texts = [] });
        settings.Normalize();
        var texts = settings.Groups[0].Auras[0].Texts;
        Check.Equal(Enum.GetValues<TextField>().Length, texts.Count);
        Check.That(texts.Single(text => text.Field == TextField.Name) is { Enabled: false, Anchor: Anchor.TopLeft });
        Check.That(texts.Single(text => text.Field == TextField.Stacks) is { Enabled: true, Anchor: Anchor.BottomRight });
        Check.That(texts.Single(text => text.Field == TextField.Duration) is { Enabled: true, Anchor: Anchor.Center });
        Check.That(texts.Single(text => text.Field == TextField.Charges) is { Enabled: true, Anchor: Anchor.BottomRight });
        Check.That(texts.Single(text => text.Field == TextField.Percent) is { Enabled: true, Anchor: Anchor.Center });
    }

    [Test]
    public void NormalizeDropsDuplicateTextFields()
    {
        var settings = WithAura(new Aura
        {
            Texts = [new AuraText { Field = TextField.Name, Enabled = true, Anchor = Anchor.Top }, new AuraText { Field = TextField.Name, Enabled = false, Anchor = Anchor.Bottom }],
        });
        settings.Normalize();
        var texts = settings.Groups[0].Auras[0].Texts;
        Check.Equal(Enum.GetValues<TextField>().Length, texts.Count);
        var name = texts.Single(text => text.Field == TextField.Name);
        Check.That(name.Enabled && name.Anchor == Anchor.Top);
    }

    [Test]
    public void NormalizeRepairsNullLists()
    {
        var settings = new Settings { Groups = null! };
        settings.Normalize();
        Check.Equal(0, settings.Groups.Count);

        settings = new Settings { Groups = [null!, new Group { Auras = null! }, new Group { Auras = [null!, new Aura { Conditions = null!, Texts = null! }, new Aura { Conditions = [null!, new Condition { Text = null! }] }] }] };
        settings.Normalize();
        Check.Equal(2, settings.Groups.Count);
        Check.Equal(0, settings.Groups[0].Auras.Count);
        var auras = settings.Groups[1].Auras;
        Check.Equal(2, auras.Count);
        Check.Equal(0, auras[0].Conditions.Count);
        Check.Equal(1, auras[1].Conditions.Count);
        Check.Equal("", auras[1].Conditions[0].Text);
        Check.Equal(Enum.GetValues<TextField>().Length, auras[0].Texts.Count);
    }

    [Test]
    public void NormalizeKeepsCleanListAndRepairsBrokenOne()
    {
        var settings = WithAura(new Aura { Texts = [] });
        settings.Normalize();
        var aura = settings.Groups[0].Auras[0];
        var list = aura.Texts;
        settings.Normalize();
        Check.That(ReferenceEquals(list, aura.Texts));

        (list[0], list[1]) = (list[1], list[0]);
        settings.Normalize();
        Check.That(!ReferenceEquals(list, aura.Texts));
        Check.That(aura.Texts.Select(text => text.Field).SequenceEqual(Enum.GetValues<TextField>()));

        aura.Texts[2] = null!;
        settings.Normalize();
        Check.That(aura.Texts.Select(text => text.Field).SequenceEqual(Enum.GetValues<TextField>()));
    }

    [Test]
    public void NormalizeIsIdempotent()
    {
        var settings = WithAura(new Aura { Texts = [new AuraText { Field = TextField.Stacks, Enabled = false, Anchor = Anchor.Top }] });
        settings.Normalize();
        var once = Serialize(settings);
        settings.Normalize();
        Check.Equal(once, Serialize(settings));
    }

    [Test]
    public void JsonRoundTripKeepsEverything()
    {
        var aura = new Aura
        {
            Name = "Haste", Style = AuraStyle.Bar, BarIcon = BarIcon.Right, IconSize = 33, BarWidth = 111, BarHeight = 12, Icon = "buff/haste",
            FillColor = Color.FromArgb(0x80, 1, 2, 3), WhenInactive = WhenInactive.Dim,
            Conditions = [new Condition { Kind = ConditionKind.SkillCharges, Percent = 12, Direction = Direction.Above, Text = "x", Missing = true, Ending = true, Seconds = 2.5f, MinStacks = 3, SkillCfgId = 77, Ready = false, Charges = 2 }],
        };
        var settings = new Settings { Lock = false, HideOnFullScreen = false, Groups = [new Group { Name = "G", X = 1, Y = 2, Growth = Growth.OutwardVertical, Align = GroupAlign.End, Spacing = 9, Enabled = false, Auras = [aura] }] };
        settings.Normalize();
        var loaded = Load(Serialize(settings));
        Check.That(!loaded.Lock && !loaded.HideOnFullScreen);
        var group = loaded.Groups.Single();
        Check.That(group is { Name: "G", X: 1, Y: 2, Growth: Growth.OutwardVertical, Align: GroupAlign.End, Spacing: 9, Enabled: false });
        var back = group.Auras.Single();
        Check.That(back is { Name: "Haste", Style: AuraStyle.Bar, BarIcon: BarIcon.Right, IconSize: 33, BarWidth: 111, BarHeight: 12, Icon: "buff/haste", WhenInactive: WhenInactive.Dim });
        Check.Equal(Color.FromArgb(0x80, 1, 2, 3).ToArgb(), back.FillColor.ToArgb());
        Check.That(back.Conditions.Single() is { Kind: ConditionKind.SkillCharges, Percent: 12, Direction: Direction.Above, Text: "x", Missing: true, Ending: true, Seconds: 2.5f, MinStacks: 3, SkillCfgId: 77, Ready: false, Charges: 2 });
        Check.Equal(Enum.GetValues<TextField>().Length, back.Texts.Count);
    }
}
