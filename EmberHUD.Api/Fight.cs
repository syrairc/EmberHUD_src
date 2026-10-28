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

public readonly record struct FightShape(uint Kind, uint Radius, uint ExtentA, uint ExtentB);

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

    public double? Scale { get; init; }

    public int? Camp { get; init; }

    public int OwnerId { get; init; }

    public double? Lifetime { get; init; }

    public FightBuff[] Buffs { get; init; } = [];
}

public static class Fight
{
    internal static Func<Vector2, float, FightEntity[]>? Lister { get; set; }

    public static FightEntity[] Scan(Vector2 at, float radius) => Lister?.Invoke(at, radius) ?? [];
}
