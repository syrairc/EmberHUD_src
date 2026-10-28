using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class FightEntityReader(IReadBackend memory, nint moduleBase)
{
    private const double MaxCoordinate = 1e7;
    private const uint IdLimit = 100_000_000;
    private const int LastMoveCommandType = 12;

    private readonly FightComponents _fight = new(memory, moduleBase);

    private delegate bool TryDecode<T>(nint at, out T value);

    private readonly record struct Candidate(int Id, Vector3 Position, Vector2 Direction, Dictionary<int, nint> Components);

    public FightEntity[] Near(Vector2 at, float radius, int max)
    {
        if (!_fight.TryReadWorld(out var world)) return [];

        var found = new List<Candidate>();
        memory.TryWalkBuckets(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var fighter = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (fighter != 0 && TryCandidate(fighter, (int)BitConverter.ToUInt32(node[Offsets.NodeKey..]), at, radius, out var candidate)) found.Add(candidate);
                return true;
            });
        return [.. found.OrderBy(candidate => Vector2.Distance(Flat(candidate.Position), at)).Take(max).Select(Build)];
    }

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Y);

    private bool TryCandidate(nint fighter, int id, Vector2 at, float radius, out Candidate candidate)
    {
        candidate = default;
        if (!_fight.TryReadComponent(fighter, Offsets.TransformComponentId, out var transform) || transform == 0 ||
            !TryVector3(transform + Offsets.TransformPosition, out var position) ||
            !TryVector2(transform + Offsets.TransformDirection, out var direction) ||
            Vector2.Distance(Flat(position), at) > radius) return false;

        candidate = new Candidate(id, position, direction, _fight.ReadAll(fighter));
        return true;
    }

    private FightEntity Build(Candidate candidate)
    {
        var components = candidate.Components;
        return new FightEntity(candidate.Id, candidate.Position, candidate.Direction, FightClassifier.Classify(components.Keys.ToHashSet()), [.. components.Keys.Order()])
        {
            Motion = Decode<FightMotion>(components, Offsets.MoveComponentId, TryMotion),
            Command = Decode<FightCommand>(components, Offsets.MoveCmdComponentId, TryCommand),
            Bullet = Decode<FightBullet>(components, Offsets.BulletComponentId, TryBullet),
            Track = Decode<FightTrack>(components, Offsets.TrackComponentId, TryTrack),
            Link = Decode<FightLink>(components, Offsets.SkillAttachComponentId, TryLink),
            Timed = Decode<FightTimed>(components, Offsets.LastedEffectComponentId, TryTimed),
            Beam = Decode<FightBeam>(components, Offsets.ViewAssistComponentId, TryBeam),
            Npc = Decode<FightNpc>(components, Offsets.NpcComponentId, TryNpc),
            Role = Decode<FightRole>(components, Offsets.RoleComponentId, TryRole),
            Shape = Decode<FightShape>(components, Offsets.ShapeComponentId, TryShape),
            Scale = Decode<double>(components, Offsets.ScaleComponentId, TryScale),
            Camp = Decode<int>(components, Offsets.SideComponentId, TryCamp),
            OwnerId = Decode<int>(components, Offsets.AttachmentComponentId, TryOwner) ?? 0,
            Lifetime = Decode<double>(components, Offsets.SuicideComponentId, TryLifetime),
            Buffs = ReadBuffs(components),
        };
    }

    private static T? Decode<T>(Dictionary<int, nint> components, int id, TryDecode<T> decode) where T : struct =>
        components.TryGetValue(id, out var at) && decode(at, out var value) ? value : null;

    private bool TryMotion(nint at, out FightMotion motion)
    {
        motion = default;
        if (!TryDouble(at + Offsets.MoveSpeed, out var speed) || !TryDouble(at + Offsets.MoveTickDistance, out var tickDistance) ||
            !TryVector2(at + Offsets.MoveDirection, out var velocity) || !TryVector2(at + Offsets.MoveLastStep, out var lastStep) ||
            !memory.TryRead(at + Offsets.MoveBlocked, out byte blocked) || !memory.TryRead(at + Offsets.MoveActive, out byte moving) ||
            !TryCount(at + Offsets.MoveCommandId, out var commandId) || !TryDouble(at + Offsets.MoveTravelled, out var travelled)) return false;

        motion = new FightMotion(speed, tickDistance, velocity, lastStep, blocked != 0, moving != 0, commandId, travelled);
        return true;
    }

    private bool TryCommand(nint at, out FightCommand command)
    {
        command = default;
        if (!memory.TryReadPtr(at + Offsets.MoveCmdCommand, out var cmd) || !memory.TryRead(cmd + Offsets.CmdType, out byte type) || type > LastMoveCommandType ||
            !TryCount(cmd + Offsets.CmdId, out var id) || !TryDouble(cmd + Offsets.CmdElapsed, out var elapsed) ||
            !memory.TryRead(at + Offsets.MoveCmdSpeedProfile, out byte speedProfile)) return false;

        command = new FightCommand((MoveCommandType)type, id, elapsed, speedProfile);
        return true;
    }

    private bool TryBullet(nint at, out FightBullet bullet)
    {
        bullet = default;
        if (!memory.TryRead(at + Offsets.BulletMoveType, out byte moveType) || !TryDouble(at + Offsets.BulletRange, out var range) ||
            !TryVector3(at + Offsets.BulletOrigin, out var origin) || !TryVector3(at + Offsets.BulletLast, out var last) || !TryVector3(at + Offsets.BulletEnd, out var end) ||
            !memory.TryRead(at + Offsets.BulletTerrainStop, out byte terrainStop) || !memory.TryRead(at + Offsets.BulletArmed, out byte armed) ||
            !memory.TryRead(at + Offsets.BulletFinished, out byte finished) || !TryEntityRef(at + Offsets.BulletSource, out _, out var sourceId)) return false;

        bullet = new FightBullet(moveType, range, origin, last, end, terrainStop != 0, armed != 0, finished != 0, sourceId);
        return true;
    }

    private bool TryTrack(nint at, out FightTrack track)
    {
        track = default;
        if (!TryEntityRef(at + Offsets.TrackTarget, out var bound, out var targetId) || !memory.TryRead(at + Offsets.TrackNeedsTarget, out byte needsTarget) ||
            !memory.TryRead(at + Offsets.TrackSurvives, out byte survives) || !TryDouble(at + Offsets.TrackRange, out var range)) return false;

        track = new FightTrack(bound, targetId, needsTarget != 0, survives != 0, range);
        return true;
    }

    private bool TryLink(nint at, out FightLink link)
    {
        link = default;
        if (!TryId(at + Offsets.SkillAttachSkill, out var skillId) || !TryCount(at + Offsets.SkillAttachHits, out var hits) ||
            !TryCount(at + Offsets.SkillAttachMaxHits, out var maxHits) || !memory.TryRead(at + Offsets.SkillAttachUnlimited, out byte unlimited)) return false;

        link = new FightLink(skillId, hits, maxHits, unlimited != 0);
        return true;
    }

    private bool TryTimed(nint at, out FightTimed timed)
    {
        timed = default;
        if (!TryDouble(at + Offsets.LastedElapsed, out var elapsed) || !TryDouble(at + Offsets.LastedDuration, out var duration)) return false;

        timed = new FightTimed(elapsed, duration);
        return true;
    }

    private bool TryBeam(nint at, out FightBeam beam)
    {
        beam = default;
        if (!TryVector3(at + Offsets.ViewAssistStart, out var start) || !TryVector3(at + Offsets.ViewAssistEnd, out var end)) return false;

        beam = new FightBeam(start, end);
        return true;
    }

    private bool TryNpc(nint at, out FightNpc npc)
    {
        npc = default;
        if (!TryId(at + Offsets.NpcId, out var npcId) || !TryCount(at + Offsets.NpcRarity, out var rarity) ||
            !TryCount(at + Offsets.NpcSource, out var source) || !TryCount(at + Offsets.NpcCategory, out var category)) return false;

        npc = new FightNpc(npcId, rarity, source, category);
        return true;
    }

    private bool TryRole(nint at, out FightRole role)
    {
        role = default;
        if (!TryId(at + Offsets.FightRoleConfig, out var config) || !TryCount(at + Offsets.FightRoleRarity, out var rarity) ||
            !memory.TryRead(at + Offsets.FightRoleLevel, out int level) || level is < 0 or >= (int)IdLimit) return false;

        role = new FightRole(config, rarity, level);
        return true;
    }

    private bool TryShape(nint at, out FightShape shape)
    {
        shape = default;
        if (!memory.TryRead(at + Offsets.ShapeKind, out uint kind) || !memory.TryRead(at + Offsets.ShapeRadius, out uint radius) ||
            !memory.TryRead(at + Offsets.ShapeExtentA, out uint extentA) || !memory.TryRead(at + Offsets.ShapeExtentB, out uint extentB)) return false;

        shape = new FightShape(kind, radius, extentA, extentB);
        return true;
    }

    private bool TryScale(nint at, out double scale) => TryDouble(at + Offsets.ScaleValue, out scale);

    private bool TryCamp(nint at, out int camp)
    {
        camp = 0;
        if (!memory.TryRead(at + Offsets.SideCamp, out byte value)) return false;
        camp = value;
        return true;
    }

    private bool TryOwner(nint at, out int owner) => TryId(at + Offsets.AttachmentOwnerId, out owner);

    private bool TryLifetime(nint at, out double life) =>
        TryDouble(at + Offsets.SuicideLife, out life) && life >= 0 && life <= Offsets.MaxLifetimeSeconds;

    private FightBuff[] ReadBuffs(Dictionary<int, nint> components)
    {
        if (!components.TryGetValue(Offsets.ComponentBuff, out var component)) return [];

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

    private bool TryEntityRef(nint at, out bool bound, out int id)
    {
        id = 0;
        bound = false;
        if (!memory.TryRead(at + Offsets.RefBound, out byte flag)) return false;
        bound = flag != 0;
        return !bound || TryId(at + Offsets.RefId, out id);
    }

    private bool TryId(nint at, out int id)
    {
        id = 0;
        if (!memory.TryRead(at, out uint raw) || raw is 0 or >= IdLimit) return false;
        id = (int)raw;
        return true;
    }

    private bool TryCount(nint at, out int count)
    {
        count = 0;
        if (!memory.TryRead(at, out uint raw) || raw >= IdLimit) return false;
        count = (int)raw;
        return true;
    }

    private bool TryDouble(nint at, out double value) => memory.TryRead(at, out value) && IsSane(value);

    private bool TryVector2(nint at, out Vector2 value)
    {
        value = default;
        Span<double> pair = stackalloc double[2];
        if (!memory.TryRead(at, pair) || !IsSane(pair[0]) || !IsSane(pair[1])) return false;
        value = new Vector2((float)pair[0], (float)pair[1]);
        return true;
    }

    private bool TryVector3(nint at, out Vector3 value)
    {
        value = default;
        Span<double> triple = stackalloc double[3];
        if (!memory.TryRead(at, triple) || !IsSane(triple[0]) || !IsSane(triple[1]) || !IsSane(triple[2])) return false;
        value = new Vector3((float)triple[0], (float)triple[1], (float)triple[2]);
        return true;
    }

    private static bool IsSane(double value) => double.IsFinite(value) && Math.Abs(value) < MaxCoordinate;
}
