using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class ProjectileReader(IReadBackend memory, nint moduleBase)
{
    private readonly FightComponents _fight = new(memory, moduleBase);

    public Projectile[] Read(Entity[] entities)
    {
        if (entities.Length == 0 || !_fight.TryReadWorld(out var world)) return [];

        var owners = new Dictionary<int, Rarity>(entities.Length);
        foreach (var entity in entities)
            if (entity.Kind == EntityKind.Monster && !entity.Friendly) owners[entity.Id] = entity.Rarity;

        var found = new List<Projectile>(16);
        memory.TryWalkBuckets(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var fighter = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (fighter != 0 && TryReadProjectile(fighter, (int)BitConverter.ToUInt32(node[Offsets.NodeKey..]), owners, out var projectile))
                    found.Add(projectile);
                return true;
            });
        return [.. found];
    }

    private bool TryReadProjectile(nint fighter, int id, Dictionary<int, Rarity> owners, out Projectile projectile)
    {
        projectile = default;
        Span<double> pair = stackalloc double[2];
        if (!_fight.TryReadComponent(fighter, Offsets.BulletComponentId, out var bullet) || bullet == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.TransformComponentId, out var transform) || transform == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.ShapeComponentId, out var shape) || shape == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.MoveComponentId, out var move) || move == 0 ||
            !memory.TryRead(transform + Offsets.TransformPosition, pair) || !IsFinite(pair, out var position) ||
            !memory.TryRead(transform + Offsets.TransformDirection, pair) || !IsFinite(pair, out var direction) ||
            !memory.TryRead(bullet + Offsets.BulletOrigin, pair) || !IsFinite(pair, out var origin) ||
            !memory.TryRead(shape + Offsets.ShapeRadius, out uint radius) || radius == 0 || radius > Offsets.MaxProjectileRadius ||
            !memory.TryRead(move + Offsets.MoveSpeed, out double speed) || !double.IsFinite(speed) || speed < 0 || speed > Offsets.MaxProjectileSpeed ||
            !_fight.TryReadComponent(fighter, Offsets.AttachmentComponentId, out var attachment) || attachment == 0 ||
            !memory.TryRead(attachment + Offsets.AttachmentOwnerId, out int ownerId) || !owners.TryGetValue(ownerId, out var rarity))
            return false;

        var length = direction.Length();
        if (!float.IsFinite(length) || length < 0.5f) return false;

        projectile = new Projectile(id, ownerId, rarity, position, origin, direction / length, radius, (float)speed);
        return true;
    }

    private static bool IsFinite(ReadOnlySpan<double> pair, out Vector2 value)
    {
        value = new Vector2((float)pair[0], (float)pair[1]);
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
