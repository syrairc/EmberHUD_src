using System;
using System.Linq;
using EmberHUD;

namespace Reactions;

public sealed class Main : Plugin<Settings>
{
    readonly Engine _engine = new();

    public override void Initialize()
    {
        Settings.Normalize();
    }

    public override void Tick()
    {
        Settings.Normalize();
        if (Settings.Reactions.Any(reaction => reaction.Enabled && reaction.Conditions.Any(condition => condition.Kind is ConditionKind.Buff or ConditionKind.SkillCooldown or ConditionKind.SkillCharges)))
            Game.WantCharacter();
        _engine.Step(Settings, Game.State, Environment.TickCount64, Send.Press);
    }

    public override void OnDisable() => Send.ReleaseAll();

    public override void Dispose() => Send.ReleaseAll();
}
