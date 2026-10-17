using System.Collections.Generic;
using EmberHUD;

namespace Reactions;

public enum ConditionKind { Health, Shield, HealthAndShield, Buff, SkillCooldown, SkillCharges, NearbyEnemies }

public sealed class Condition
{
    public ConditionKind Kind;
    public float Percent = 50;
    public string Text = "";
    public bool Missing;
    public int SkillCfgId;
    public bool Ready = true;
    public int Charges = 1;
    public float Radius = 300;
    public int Count = 1;
    public Rarity MinRarity;
}

public sealed class Reaction
{
    public bool Enabled = true;
    public string Name = "";
    public Hotkey Trigger;
    public int CooldownMs = 1000;
    public List<Condition> Conditions = [];
}

public sealed class Settings : PluginSettings
{
    public int GlobalCooldownMs = 250;
    public List<Reaction> Reactions = [];

    public void Normalize()
    {
        Reactions ??= [];
        Reactions.RemoveAll(reaction => reaction is null);
        foreach (var reaction in Reactions)
        {
            reaction.Name ??= "";
            reaction.Conditions ??= [];
            reaction.Conditions.RemoveAll(condition => condition is null);
            foreach (var condition in reaction.Conditions) condition.Text ??= "";
        }
    }

    public override void Draw() => SettingsUI.Draw(this);
}
