using System.Numerics;

namespace EmberHUD;

public enum FightArchetype { Unknown, Player, Monster, Minion, Bullet, Laser, LastedEffect, DirectHit, GroundEffect, ThunderCloud, BuffArea, TriggerArea, MapTrap, Anchor }

public enum MoveCommandType { None = 0, Stop = 1, Straight = 2, Seek = 3, Path = 4, Circle = 5, Curve = 6, Arc = 7, Blink = 8, ToPoint = 9, Spiral = 10, Polyline = 11, Bezier = 12 }

public enum BulletMoveType { Straight = 0, StraightGround = 1, Lob = 2, LobSolved = 3, Link = 4, Flat = 5, FollowOwner = 6, FollowTarget = 7, Rise = 8 }

public readonly record struct FightMotion(double Speed, double TickDistance, Vector2 Velocity, Vector2 LastStep, bool Blocked, bool Moving, int CommandId, double Travelled);

public readonly record struct FightCommand(MoveCommandType Type, int Id, double Elapsed, int SpeedProfile);

public readonly record struct FightBullet(int MoveType, double Range, Vector3 Origin, Vector3 Last, Vector3 End, bool TerrainStop, bool Armed, bool Finished, int SourceId)
{
    public BulletMoveType? Kind => MoveType is >= 0 and <= 8 ? (BulletMoveType)MoveType : null;
}

public readonly record struct FightTrack(bool HasTarget, int TargetId, bool NeedsTarget, bool SurvivesTargetLoss, double Range);

public readonly record struct FightLink(int SkillId, int Hits, int MaxHits, bool Unlimited);

public readonly record struct FightTimed(double Elapsed, double Duration);

public readonly record struct FightBeam(Vector3 Start, Vector3 End);

public readonly record struct FightNpc(int NpcId, int Rarity, int SourceType, int Category);

public readonly record struct FightRole(int Config, int Rarity, int Level);

public readonly record struct FightShape(uint Kind, uint Radius, uint ExtentA, uint ExtentB, uint Inner = 0, int AngleA = 0, int AngleB = 0, double OffsetX = 0, double OffsetY = 0);

public readonly record struct FightAttack(double Delay, double Duration, int Hits, double Interval);

public readonly record struct FightInfo(FightArchetype Archetype, int Camp, int Level, int SourceType);

/// <summary>Observed Side camp ids.</summary>
public static class FightCamps
{
    public const int Hostile = 1;
    public const int Player = 2;
    public const int BossArea = 3;
    public const int Servant = 5;
}

public sealed record FightEntity(int Id, Vector3 Position, Vector2 Direction, FightArchetype Archetype, int[] Components)
{
    public FightMotion? Motion { get; init; }

    public FightCommand? Command { get; init; }

    public FightBullet? Bullet { get; init; }

    public FightTrack? Track { get; init; }

    public FightLink? Link { get; init; }

    public FightTimed? Timed { get; init; }

    public FightBeam? Beam { get; init; }

    public FightNpc? Npc { get; init; }

    public FightRole? Role { get; init; }

    public FightShape? Shape { get; init; }

    public FightAttack? Attack { get; init; }

    public double? Scale { get; init; }

    public int? Camp { get; init; }

    public int OwnerId { get; init; }

    public double? Lifetime { get; init; }

    public FightBuff[] Buffs { get; init; } = [];

    public CrowdControl Control => EmberHUD.Buffs.Union(Buffs.Select(buff => buff.Id));

    public bool IsHostile => Camp is FightCamps.Hostile or FightCamps.BossArea;

    private const int CircleVertices = 32;
    private const double ArcStepDegrees = 12;

    public bool Covers(Vector2 point)
    {
        if (!TryFrame(out var shape, out var centre, out var forward, out var left, out var scale) || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;

        var dx = point.X - centre.X;
        var dy = point.Y - centre.Y;
        var distanceSquared = dx * dx + dy * dy;
        var radius = shape.Radius * scale;
        switch (shape.Kind & 0xFF)
        {
            case 1:
                return radius > 0 && distanceSquared <= radius * radius;
            case 5:
                var hole = shape.Inner * scale;
                return radius > 0 && distanceSquared <= radius * radius && distanceSquared >= hole * hole;
            case 2:
                return shape.ExtentA > 0 && shape.ExtentB > 0 &&
                    Math.Abs(dx * forward.X + dy * forward.Y) <= shape.ExtentA * scale / 2 && Math.Abs(dx * left.X + dy * left.Y) <= shape.ExtentB * scale / 2;
            case 4:
                var span = shape.AngleB - shape.AngleA;
                if (radius <= 0 || span <= 0 || distanceSquared > radius * radius) return false;
                if (span >= 360 || distanceSquared == 0) return true;
                var angle = Math.Atan2(dx * left.X + dy * left.Y, dx * forward.X + dy * forward.Y) * 180 / Math.PI;
                var from = (angle - shape.AngleA) % 360;
                return (from < 0 ? from + 360 : from) <= span;
            default:
                return false;
        }
    }

    public int Outline(Span<Vector2> into)
    {
        if (!TryFrame(out var shape, out var centre, out var forward, out var left, out var scale)) return 0;

        var radius = shape.Radius * scale;
        switch (shape.Kind & 0xFF)
        {
            case 1 or 5:
                return radius > 0 ? Arc(into, centre, forward, left, radius, 0, 360, closed: false) : 0;
            case 2:
                if (shape.ExtentA == 0 || shape.ExtentB == 0 || into.Length < 4) return 0;
                var halfLength = (float)(shape.ExtentA * scale / 2);
                var halfWidth = (float)(shape.ExtentB * scale / 2);
                into[0] = centre + forward * halfLength + left * halfWidth;
                into[1] = centre - forward * halfLength + left * halfWidth;
                into[2] = centre - forward * halfLength - left * halfWidth;
                into[3] = centre + forward * halfLength - left * halfWidth;
                return 4;
            case 4:
                var span = shape.AngleB - shape.AngleA;
                if (radius <= 0 || span <= 0) return 0;
                if (span >= 360) return Arc(into, centre, forward, left, radius, 0, 360, closed: false);
                if (into.Length < 1) return 0;
                into[0] = centre;
                var written = Arc(into[1..], centre, forward, left, radius, shape.AngleA, span, closed: true);
                return written == 0 ? 0 : written + 1;
            default:
                return 0;
        }
    }

    private static int Arc(Span<Vector2> into, Vector2 centre, Vector2 forward, Vector2 left, double radius, double startDegrees, double spanDegrees, bool closed)
    {
        var segments = closed ? (int)Math.Ceiling(spanDegrees / ArcStepDegrees) : CircleVertices;
        var count = closed ? segments + 1 : segments;
        if (into.Length < count) return 0;

        for (var i = 0; i < count; i++)
        {
            var angle = (startDegrees + spanDegrees * i / segments) * Math.PI / 180;
            into[i] = centre + forward * (float)(radius * Math.Cos(angle)) + left * (float)(radius * Math.Sin(angle));
        }
        return count;
    }

    private bool TryFrame(out FightShape shape, out Vector2 centre, out Vector2 forward, out Vector2 left, out double scale)
    {
        centre = forward = left = default;
        scale = 1;
        if (Shape is not { } found) { shape = default; return false; }

        shape = found;
        if (!float.IsFinite(Position.X) || !float.IsFinite(Position.Y) || !float.IsFinite(Direction.X) || !float.IsFinite(Direction.Y) ||
            !double.IsFinite(shape.OffsetX) || !double.IsFinite(shape.OffsetY)) return false;

        scale = Scale is { } value && double.IsFinite(value) && value > 0 ? value : 1;
        var length = Direction.Length();
        forward = length > 1e-6f ? Direction / length : Vector2.UnitX;
        left = new Vector2(-forward.Y, forward.X);
        centre = new Vector2(Position.X, Position.Y) + (forward * (float)(shape.OffsetX * scale)) + (left * (float)(shape.OffsetY * scale));
        return float.IsFinite(centre.X) && float.IsFinite(centre.Y);
    }
}

public static class Fight
{
    internal static Func<Vector2, float, FightEntity[]>? Lister { get; set; }

    public static FightEntity[] Scan(Vector2 at, float radius) => Lister?.Invoke(at, radius) ?? [];
}
