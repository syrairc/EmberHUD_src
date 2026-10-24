using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class FightProbeReader(IReadBackend memory, nint moduleBase)
{
    private readonly FightComponents _fight = new(memory, moduleBase);

    public FightProbe[] Near(Vector2 at, float radius)
    {
        if (!_fight.TryReadWorld(out var world)) return [];

        var found = new List<FightProbe>();
        memory.TryWalkBuckets(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var fighter = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (fighter != 0 && TryProbe(fighter, (int)BitConverter.ToUInt32(node[Offsets.NodeKey..]), at, radius, out var probe)) found.Add(probe);
                return true;
            });
        return [.. found.OrderBy(probe => Vector2.Distance(probe.Position, at))];
    }

    private bool TryProbe(nint fighter, int id, Vector2 at, float radius, out FightProbe probe)
    {
        probe = default;
        Span<double> pair = stackalloc double[2];
        if (!_fight.TryReadComponent(fighter, Offsets.TransformComponentId, out var transform) || transform == 0 ||
            !memory.TryRead(transform + Offsets.TransformPosition, pair)) return false;

        var position = new Vector2((float)pair[0], (float)pair[1]);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || Vector2.Distance(position, at) > radius) return false;

        var components = new List<int>();
        for (var component = 0; component < Offsets.FightComponentCount; component++)
            if (_fight.TryReadComponent(fighter, component, out var address) && address != 0) components.Add(component);

        uint kind = 0, shapeRadius = 0, extentA = 0, extentB = 0;
        if (_fight.TryReadComponent(fighter, Offsets.ShapeComponentId, out var shape) && shape != 0)
        {
            memory.TryRead(shape + Offsets.ShapeKind, out kind);
            memory.TryRead(shape + Offsets.ShapeRadius, out shapeRadius);
            memory.TryRead(shape + Offsets.ShapeExtentA, out extentA);
            memory.TryRead(shape + Offsets.ShapeExtentB, out extentB);
        }

        var scale = double.NaN;
        if (_fight.TryReadComponent(fighter, Offsets.ScaleComponentId, out var scaleComponent) && scaleComponent != 0) memory.TryRead(scaleComponent + Offsets.ScaleValue, out scale);

        var camp = -1;
        if (_fight.TryReadComponent(fighter, Offsets.SideComponentId, out var side) && side != 0 && memory.TryRead(side + Offsets.SideCamp, out byte sideCamp)) camp = sideCamp;

        var owner = 0;
        if (_fight.TryReadComponent(fighter, Offsets.AttachmentComponentId, out var attachment) && attachment != 0) memory.TryRead(attachment + Offsets.AttachmentOwnerId, out owner);

        probe = new FightProbe(id, position, [.. components], kind, shapeRadius, extentA, extentB, scale, camp, owner) { Buffs = ReadBuffs(fighter) };
        return true;
    }

    private FightBuff[] ReadBuffs(nint fighter)
    {
        if (!_fight.TryReadComponent(fighter, Offsets.ComponentBuff, out var component) || component == 0) return [];

        var buffs = new List<FightBuff>();
        memory.TryWalkBuckets(component + Offsets.BuffMapCount, component + Offsets.BuffMap, Offsets.MaxBuckets, Offsets.NodeNext + sizeof(long), Offsets.NodeNext, Offsets.MaxBuffs, (_, node) =>
        {
            var instance = (nint)BitConverter.ToInt64(node[Offsets.FightNodeValue..]);
            if (instance != 0 && memory.TryRead(instance + Offsets.BuffId, out int buffId) && buffId > 0 && memory.TryRead(instance + Offsets.BuffRemaining, out double remaining) && double.IsFinite(remaining))
            {
                var stacks = memory.TryRead(instance + Offsets.BuffStackable, out byte stackable) && stackable != 0 && memory.TryRead(instance + Offsets.BuffStacks, out int count) && count > 0 ? count : 1;
                buffs.Add(new FightBuff(buffId, stacks, remaining));
            }
            return true;
        });
        return [.. buffs];
    }
}
