using System;
using System.Text.RegularExpressions;
using EmberHUD;

namespace Reactions;

public static class Conditions
{
    public static bool AllMet(Reaction reaction, GameState state)
    {
        if (reaction.Conditions is not { Count: > 0 } conditions) return false;
        foreach (var condition in conditions)
            if (condition is null || !Met(condition, state)) return false;
        return true;
    }

    public static bool Met(Condition condition, GameState state) => condition.Kind switch
    {
        ConditionKind.Health => state.Player is { } p && Below(p.Life, p.MaxLife, condition.Percent),
        ConditionKind.Shield => state.Player is { } p && Below(p.Shield, p.MaxShield, condition.Percent),
        ConditionKind.HealthAndShield => state.Player is { } p && Below(p.Life + p.Shield, p.MaxLife + p.MaxShield, condition.Percent),
        ConditionKind.Buff => !string.IsNullOrEmpty(condition.Text) && state.Buffs is { } buffs && Array.Exists(buffs, buff => BuffMatches(condition.Text, buff)) != condition.Missing,
        ConditionKind.SkillCooldown => FindSkill(state, condition.SkillCfgId) is { } skill && skill.Ready == condition.Ready,
        ConditionKind.SkillCharges => FindSkill(state, condition.SkillCfgId) is { MaxCharges: > 0 } skill && skill.Charges >= condition.Charges,
        ConditionKind.NearbyEnemies => state.CountNear(condition.Radius, condition.MinRarity) >= Math.Max(condition.Count, 1),
        _ => false,
    };

    public static bool BuffMatches(string text, Buff buff)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var name = buff.Name ?? "";
        var id = buff.Id.ToString();
        try
        {
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var timeout = TimeSpan.FromMilliseconds(50);
            return Regex.IsMatch(name, text, options, timeout) || Regex.IsMatch(id, text, options, timeout);
        }
        catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException)
        {
            return name.Equals(text, StringComparison.OrdinalIgnoreCase) || id.Equals(text, StringComparison.OrdinalIgnoreCase);
        }
    }

    static bool Below(double value, double max, float percent) => max > 0 && value / max * 100 < percent;

    internal static Skill? FindSkill(GameState state, int cfgId)
    {
        if (state.Skills is not { } skills) return null;
        foreach (var skill in skills)
            if (skill.CfgId == cfgId && skill.Slot >= 0) return skill;
        return null;
    }
}
