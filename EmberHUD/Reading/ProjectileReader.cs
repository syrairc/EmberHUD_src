using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class ProjectileReader(IReadBackend memory, nint moduleBase)
{
    private readonly FightComponents _fight = new(memory, moduleBase);
    private Dictionary<int, ProjectileTrack> _tracks = [];

    public Projectile[] Read(Entity[] entities)
    {
        if (entities.Length == 0 || !_fight.TryReadWorld(out var world))
        {
            _tracks = [];
            return [];
        }

        var owners = new Dictionary<int, (Rarity Rarity, Vector2 Position)>(entities.Length);
        foreach (var entity in entities)
            if (entity.Kind == EntityKind.Monster) owners[entity.Id] = (entity.Rarity, new Vector2(entity.Position.X, entity.Position.Y));

        var found = new List<Projectile>(16);
        var tracks = new Dictionary<int, ProjectileTrack>(_tracks.Count);
        memory.TryWalkBuckets(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var fighter = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (fighter != 0 && TryReadProjectile(world, fighter, (int)BitConverter.ToUInt32(node[Offsets.NodeKey..]), owners, tracks, out var projectile))
                    found.Add(projectile);
                return true;
            });
        _tracks = tracks;
        return [.. found];
    }

    private bool TryReadProjectile(nint world, nint fighter, int id, Dictionary<int, (Rarity Rarity, Vector2 Position)> owners, Dictionary<int, ProjectileTrack> tracks, out Projectile projectile)
    {
        projectile = default;
        Span<double> pair = stackalloc double[2];
        _fight.TryReadComponent(fighter, Offsets.BulletComponentId, out var bullet);
        var laser = bullet == 0;
        if (!_fight.TryReadComponent(fighter, Offsets.TransformComponentId, out var transform) || transform == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.ShapeComponentId, out var shape) || shape == 0 ||
            !_fight.TryReadComponent(fighter, Offsets.MoveComponentId, out var move) || move == 0 ||
            !memory.TryRead(transform + Offsets.TransformPosition, pair) || !IsFinite(pair, out var position) ||
            !memory.TryRead(transform + Offsets.TransformDirection, pair) || !IsFinite(pair, out var direction) ||
            !TryReadOrigin(bullet, position, out var origin) ||
            !TryReadRadius(shape, out var radius) || radius == 0 || radius > Offsets.MaxProjectileRadius ||
            !memory.TryRead(move + Offsets.MoveSpeed, out double speed) || !double.IsFinite(speed) || speed < 0 || speed > Offsets.MaxProjectileSpeed ||
            laser && !IsHostileRect(fighter, shape) || !laser && speed < 1 && !IsRect(shape) && !HasMoveDirection(move) ||
            !_fight.TryReadComponent(fighter, Offsets.AttachmentComponentId, out var attachment) || attachment == 0 ||
            !memory.TryRead(attachment + Offsets.AttachmentOwnerId, out int ownerId) || !owners.TryGetValue(ownerId, out var owner) || !_fight.IsHostile(world, ownerId))
            return false;

        var length = direction.Length();
        if (!float.IsFinite(length) || length < 0.5f) return false;

        var heading = direction / length;
        Vector2? orbitCentre = null;
        if (!laser)
        {
            if (_tracks.TryGetValue(id, out var track)) track.Update(position, owner.Position);
            else track = new ProjectileTrack(position, heading);
            tracks[id] = track;
            heading = track.Heading;
            if (track.Orbiting) orbitCentre = owner.Position;
        }

        projectile = new Projectile(id, ownerId, owner.Rarity, position, origin, heading, radius, (float)speed) { OrbitCentre = orbitCentre };
        return true;
    }

    private bool TryReadOrigin(nint bullet, Vector2 position, out Vector2 origin)
    {
        origin = position;
        if (bullet == 0) return true;
        Span<double> pair = stackalloc double[2];
        return memory.TryRead(bullet + Offsets.BulletOrigin, pair) && IsFinite(pair, out origin);
    }

    private bool HasMoveDirection(nint move)
    {
        Span<double> pair = stackalloc double[2];
        return memory.TryRead(move + Offsets.MoveDirection, pair) && IsFinite(pair, out var direction) && direction.Length() >= 0.5f;
    }

    private bool IsRect(nint shape) => memory.TryRead(shape + Offsets.ShapeKind, out uint kind) && kind == Offsets.ShapeRect;

    private bool IsHostileRect(nint fighter, nint shape) =>
        IsRect(shape) && _fight.TryReadComponent(fighter, Offsets.DamageDataComponentId, out var damage) && damage != 0;

    private bool TryReadRadius(nint shape, out uint radius)
    {
        radius = 0;
        if (!memory.TryRead(shape + Offsets.ShapeKind, out uint kind)) return false;
        if (kind != Offsets.ShapeRect) return memory.TryRead(shape + Offsets.ShapeRadius, out radius);
        if (!memory.TryRead(shape + Offsets.ShapeExtentA, out uint a) || !memory.TryRead(shape + Offsets.ShapeExtentB, out uint b)) return false;
        radius = Math.Min(a, b);
        return true;
    }

    private static bool IsFinite(ReadOnlySpan<double> pair, out Vector2 value)
    {
        value = new Vector2((float)pair[0], (float)pair[1]);
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
