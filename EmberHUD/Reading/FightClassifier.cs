namespace EmberHUD.Host.Reading;

internal static class FightClassifier
{
    public static FightArchetype Classify(IReadOnlySet<int> components)
    {
        if (components.Contains(Offsets.PlayerComponentId)) return FightArchetype.Player;
        if (components.Contains(Offsets.ServantComponentId)) return FightArchetype.Minion;
        if (components.Contains(Offsets.NpcComponentId)) return FightArchetype.Monster;
        if (components.Contains(Offsets.BulletComponentId)) return FightArchetype.Bullet;
        if (components.Contains(Offsets.LaserComponentId)) return FightArchetype.Laser;
        if (components.Contains(Offsets.LastedEffectComponentId)) return FightArchetype.LastedEffect;
        if (components.Contains(Offsets.DirectHitComponentId)) return FightArchetype.DirectHit;
        if (components.Contains(Offsets.ThunderCloudComponentId)) return FightArchetype.ThunderCloud;
        if (components.Contains(Offsets.GroundEffectComponentId) && components.Contains(Offsets.AiComponentId)) return FightArchetype.GroundEffect;
        if (components.Contains(Offsets.BuffAreaComponentId)) return FightArchetype.BuffArea;
        if (components.Contains(Offsets.TriggerAreaComponentId)) return FightArchetype.TriggerArea;
        if (components.Contains(Offsets.MapTrapComponentId)) return FightArchetype.MapTrap;
        return IsAnchor(components) ? FightArchetype.Anchor : FightArchetype.Unknown;
    }

    private static bool IsAnchor(IReadOnlySet<int> components) =>
        components.All(id => id == Offsets.ScaleComponentId || id == Offsets.ShapeComponentId || id == Offsets.TransformComponentId) &&
        (components.Contains(Offsets.ShapeComponentId) || components.Contains(Offsets.ScaleComponentId));
}
