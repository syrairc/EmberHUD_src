using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class GroundHazardReader(IReadBackend memory, nint moduleBase)
{
    private readonly FightComponents _fight = new(memory, moduleBase);

    public GroundHazard[] Read(Entity[] entities)
    {
        if (!_fight.TryReadWorld(out var world)) return [];

        var monsters = new List<Vector2>(entities.Length);
        foreach (var entity in entities)
            if (entity.Kind == EntityKind.Monster) monsters.Add(new Vector2(entity.Position.X, entity.Position.Y));

        var found = new List<GroundHazard>(16);
        memory.TryWalkBuckets(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var fighter = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (fighter != 0 && TryReadHazard(world, fighter, (int)BitConverter.ToUInt32(node[Offsets.NodeKey..]), monsters, out var hazard) && !found.Exists(h => Same(h, hazard)))
                    found.Add(hazard);
                return true;
            });
        return [.. found];
    }

    private static bool Same(GroundHazard a, GroundHazard b) =>
        a.Kind == b.Kind && Math.Abs(a.Radius - b.Radius) <= Offsets.HazardStackTolerance &&
        Math.Abs(a.Centre.X - b.Centre.X) <= Offsets.HazardStackTolerance && Math.Abs(a.Centre.Y - b.Centre.Y) <= Offsets.HazardStackTolerance;

    private bool TryReadHazard(nint world, nint fighter, int id, List<Vector2> monsters, out GroundHazard hazard)
    {
        hazard = default;
        Span<double> pair = stackalloc double[2];
        if (!_fight.TryReadComponent(fighter, Offsets.ShapeComponentId, out var shape) || shape == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.TransformComponentId, out var transform) || transform == 0 ||
            !memory.TryRead(shape + Offsets.ShapeKind, out uint kind) ||
            !memory.TryRead(shape + Offsets.ShapeRadius, out uint radius) || radius == 0 || radius > Offsets.MaxHazardSize ||
            !memory.TryRead(transform + Offsets.TransformPosition, pair)) return false;

        var ring = (kind & 0xFF) == Offsets.ShapeRing;
        if (kind != Offsets.ShapeCircle && !ring) return false;

        var centre = new Vector2((float)pair[0], (float)pair[1]);
        if (!float.IsFinite(centre.X) || !float.IsFinite(centre.Y) || !TryReadScale(fighter, out var scale)) return false;

        var scaled = (float)(radius * scale);
        if (!float.IsFinite(scaled) || scaled <= 0 || scaled > Offsets.MaxHazardSize) return false;

        if (ring) return TryReadRing(world, fighter, id, shape, centre, scaled, scale, out hazard);

        var hasArea = _fight.TryReadComponent(fighter, Offsets.BuffAreaComponentId, out var area) && area != 0;
        if (hasArea)
        {
            if (!_fight.TryReadComponent(fighter, Offsets.SideComponentId, out var side) || side == 0 ||
                !memory.TryRead(side + Offsets.SideCamp, out byte camp) || camp != Offsets.HostileCamp && !OwnedByHostile(world, fighter) ||
                monsters.Exists(m => Vector2.Distance(m, centre) <= Offsets.AuraTolerance)) return false;
        }
        else if (!IsRock(world, fighter)) return false;

        hazard = new GroundHazard(id, hasArea ? HazardKind.Area : HazardKind.Damage, centre, scaled);
        return true;
    }

    private bool TryReadRing(nint world, nint fighter, int id, nint shape, Vector2 centre, float outer, double scale, out GroundHazard hazard)
    {
        hazard = default;
        if (_fight.TryReadComponent(fighter, Offsets.BulletComponentId, out var bullet) && bullet != 0 ||
            !memory.TryRead(shape + Offsets.ShapeRingWidth, out uint width) || width == 0 ||
            !OwnedByHostile(world, fighter)) return false;

        var inner = Math.Max(0f, outer - (float)(width * scale));
        hazard = new GroundHazard(id, HazardKind.Damage, centre, outer) { InnerRadius = float.IsFinite(inner) ? inner : 0 };
        return true;
    }

    private bool IsRock(nint world, nint fighter) =>
        _fight.TryReadComponent(fighter, Offsets.DamageDataComponentId, out var damage) && damage != 0 &&
        !(_fight.TryReadComponent(fighter, Offsets.BulletComponentId, out var bullet) && bullet != 0) &&
        OwnedByHostile(world, fighter);

    private bool OwnedByHostile(nint world, nint fighter) =>
        _fight.TryReadComponent(fighter, Offsets.AttachmentComponentId, out var attachment) && attachment != 0 &&
        memory.TryRead(attachment + Offsets.AttachmentOwnerId, out int ownerId) && _fight.IsHostile(world, ownerId);

    private bool TryReadScale(nint fighter, out double scale)
    {
        scale = 1;
        if (!_fight.TryReadComponent(fighter, Offsets.ScaleComponentId, out var component) || component == 0) return true;
        return memory.TryRead(component + Offsets.ScaleValue, out scale) && double.IsFinite(scale) && scale > 0;
    }
}
