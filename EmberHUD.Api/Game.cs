using System.Diagnostics;
using System.Numerics;

namespace EmberHUD;

public static class Game
{
    static int _minimapRescans;
    static long _combatAsked;
    static long _inventoryAsked;
    static long _castsAsked;
    static long _projectilesAsked;
    static long _characterAsked;

    public static GameState State { get; internal set; } = GameState.Empty;

    internal static Func<nint>? WindowSource { get; set; }

    public static nint WindowHandle => WindowSource?.Invoke() ?? 0;

    public static void RescanMinimap() => Interlocked.Increment(ref _minimapRescans);

    public static void WantCombat() => Interlocked.Exchange(ref _combatAsked, Stopwatch.GetTimestamp());

    internal static long CombatAsked
    {
        get => Interlocked.Read(ref _combatAsked);
        set => Interlocked.Exchange(ref _combatAsked, value);
    }

    internal static bool CombatWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _combatAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantInventory() => Interlocked.Exchange(ref _inventoryAsked, Stopwatch.GetTimestamp());

    internal static long InventoryAsked
    {
        get => Interlocked.Read(ref _inventoryAsked);
        set => Interlocked.Exchange(ref _inventoryAsked, value);
    }

    internal static bool InventoryWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _inventoryAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantCasts() => Interlocked.Exchange(ref _castsAsked, Stopwatch.GetTimestamp());

    internal static long CastsAsked
    {
        get => Interlocked.Read(ref _castsAsked);
        set => Interlocked.Exchange(ref _castsAsked, value);
    }

    internal static bool CastsWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _castsAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantProjectiles() => Interlocked.Exchange(ref _projectilesAsked, Stopwatch.GetTimestamp());

    internal static long ProjectilesAsked
    {
        get => Interlocked.Read(ref _projectilesAsked);
        set => Interlocked.Exchange(ref _projectilesAsked, value);
    }

    internal static bool ProjectilesWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _projectilesAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantCharacter() => Interlocked.Exchange(ref _characterAsked, Stopwatch.GetTimestamp());

    internal static long CharacterAsked
    {
        get => Interlocked.Read(ref _characterAsked);
        set => Interlocked.Exchange(ref _characterAsked, value);
    }

    internal static bool CharacterWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _characterAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    internal static bool TakeMinimapRescan() => Interlocked.Exchange(ref _minimapRescans, 0) > 0;
}

public enum EntityKind : byte { Monster, Ally, Player, Portal, Npc, Interactive, Coffin, Altar, Elevator, MapObject, Destructible }

public enum Rarity : byte { Normal, Magic, Rare, Boss }

public enum InventoryBag : byte { Equipment, Skills, Commodities, Other }

public sealed record GameState
{
    public static readonly GameState Empty = new();

    public string Status { get; init; } = "starting";

    public int AreaId { get; init; }

    public Player? Player { get; init; }

    public Entity[] Entities { get; init; } = [];

    public Projectile[] Projectiles { get; init; } = [];

    public Terrain? Terrain { get; init; }

    public float? MinimapRotation { get; init; }

    public MapIcon[] MapIcons { get; init; } = [];

    public ExploredTrail? Explored { get; init; }

    public Camera? Camera { get; init; }

    public GameUi Ui { get; init; } = GameUi.Empty;

    public TradePage? Trade { get; init; }

    public MapInfo? Area { get; init; }

    public WoodStakeStats? WoodStake { get; init; }

    public CombatStats? Combat { get; init; }

    public QuestTarget[] QuestTargets { get; init; } = [];

    public InventoryItem[]? Inventory { get; init; }

    public Buff[]? Buffs { get; init; }

    public Skill[]? Skills { get; init; }

    public GameMinimap? GameMinimap { get; init; }

    public bool? ChatOpen { get; init; }

    public bool? Paused { get; init; }

    public int CountNear(float radius, Rarity minRarity = Rarity.Normal)
    {
        if (Player is not { } player || float.IsNaN(radius) || radius < 0) return 0;

        var reach = radius * radius;
        var count = 0;
        foreach (var entity in Entities)
        {
            if (entity.Kind != EntityKind.Monster || entity.Friendly || entity.Hidden || entity.Rarity < minRarity) continue;

            var dx = entity.Position.X - player.Position.X;
            var dy = entity.Position.Y - player.Position.Y;
            if (dx * dx + dy * dy <= reach) count++;
        }

        return count;
    }
}

public readonly record struct QuestTarget(int Id, Vector2 Position)
{
    public string Name { get; init; } = "";
}

public sealed record GameMinimap(bool Open, float WorldWidth);

public sealed record Player(Vector3 Position, Vector2 Forward)
{
    public double Life { get; init; }

    public double MaxLife { get; init; }

    public double Shield { get; init; }

    public double MaxShield { get; init; }

    public float FlatDistance(Vector3 at) => Vector2.Distance(new Vector2(Position.X, Position.Y), new Vector2(at.X, at.Y));
}

public readonly record struct Entity(int Id, int CfgId, Vector3 Position, EntityKind Kind, Rarity Rarity)
{
    public nint Address { get; init; }

    public string Name { get; init; } = "";

    public double Life { get; init; }

    public double MaxLife { get; init; }

    public bool Friendly { get; init; }

    public bool Hidden { get; init; }

    public bool Invulnerable { get; init; }

    public ScreenRect? GameBar { get; init; }

    public bool HasGameBar { get; init; }

    public Cast? Cast { get; init; }
}

public readonly record struct Cast(int StoneId, byte State, float Progress, float SecondsLeft, Vector2 Target, Vector2 Direction)
{
    public string Name { get; init; } = "";
}

public readonly record struct Projectile(int Id, int OwnerId, Rarity OwnerRarity, Vector2 Position, Vector2 Origin, Vector2 Direction, float Radius, float Speed);

public readonly record struct ScreenRect(Vector2 Min, Vector2 Max)
{
    public float Width => Max.X - Min.X;

    public float Height => Max.Y - Min.Y;
}

public readonly record struct MapIcon(int Type, string Texture, Vector2 Position, float Size, float Rotation, int EntityId)
{
    public string Name { get; init; } = "";
}

public sealed record ExploredTrail(Vector2[] Buffer, int Count, float Radius)
{
    public ExploredTrail(Vector2[] points, float radius) : this(points, points.Length, radius) { }
}

public enum PanelKind : byte { Other, Inventory, Stash, WorldMap, Skills, Talents, Vendor, Settings }

public readonly record struct GamePanel(string Name, PanelKind Kind, Vector2 Min, Vector2 Max, bool FullScreen)
{
    public nint Address { get; init; }
}

public sealed record GameUi(GamePanel[] Panels)
{
    public static readonly GameUi Empty = new([]);

    public bool AnyFullScreen => Panels.Any(p => p.FullScreen);
}

public enum ContentState : byte { Pending, Active, Done }

public readonly record struct MapContent(string Name, string Icon, ContentState State, int Done = 0, int Total = 1);

public sealed record MapInfo(string Name, int MonsterLevel, int? Remaining, string? XpGain, bool IsTown, MapContent[] Content, string[] Modifiers);

public sealed record WoodStakeStats(bool Running, int Seconds, long Avg, long Cur, long Max, long CurUnlimited, long MaxUnlimited, long AvgUnlimited);

public sealed record CombatStats(long At, double Effective, double Raw, long Hits)
{
    public double Seconds => At / (double)Stopwatch.Frequency;
}

public readonly record struct Buff(int Id, string? Name, double Remaining, double Total);

public readonly record struct Skill(int CfgId, int Slot, double CooldownRemaining, double CooldownTotal, int Charges, int MaxCharges)
{
    public bool Ready => CooldownRemaining <= 0;
}

public sealed record InventoryItem(InventoryBag Bag, int Slot, string Id, int BaseId, int Level, int Quality, int RareValue, int Count, int StackMax, int Type);

public sealed record TradePage(int Page, bool LastPage, int CellsPerPage, TradeListing[] Listings, string PriceIcon);

public readonly record struct TradeListing(int Slot, string Name, long Quantity, long Price, string? Status, Vector2 Min, Vector2 Max);

public sealed record Camera
{
    readonly Vector3 _forward;
    readonly Vector3 _right;
    readonly Vector3 _up;
    readonly float _tanHalfFov;

    public Camera(Vector3 location, Vector3 rotation, float fov)
    {
        Location = location;
        Rotation = rotation;
        Fov = fov;
        var (sp, cp) = MathF.SinCos(rotation.X * MathF.PI / 180);
        var (sy, cy) = MathF.SinCos(rotation.Y * MathF.PI / 180);
        var (sr, cr) = MathF.SinCos(rotation.Z * MathF.PI / 180);
        _forward = new Vector3(cp * cy, cp * sy, sp);
        _right = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
        _up = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        _tanHalfFov = float.IsFinite(fov) && fov > 0 && fov < 180 ? MathF.Tan(fov * MathF.PI / 360) : 0;
    }

    public Vector3 Location { get; }

    public Vector3 Rotation { get; }

    public float Fov { get; }

    internal float Depth(Vector3 world) => Vector3.Dot(world - Location, _forward);

    public Vector2? ToScreen(Vector3 world, Vector2 viewport)
    {
        if (_tanHalfFov == 0) return null;
        var delta = world - Location;
        var depth = Vector3.Dot(delta, _forward);
        if (depth < 1f) return null;
        var centre = viewport / 2;
        var focal = centre.X / _tanHalfFov;
        var screen = new Vector2(centre.X + Vector3.Dot(delta, _right) * focal / depth, centre.Y - Vector3.Dot(delta, _up) * focal / depth);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y) ? screen : null;
    }
}

public readonly record struct SceneEntity(int Id, int CfgId, Vector3? Position, bool Alive, EntityKind? Kind, string Class)
{
    public nint Address { get; init; }
}

public static class Scene
{
    internal static Func<SceneEntity[]>? Lister { get; set; }

    public static SceneEntity[] Entities() => Lister?.Invoke() ?? [];
}

public static class Loc
{
    internal static Func<string, string?>? Getter { get; set; }
    internal static Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? Finder { get; set; }

    public static string? Get(string key) => Getter?.Invoke(key);

    public static (string Key, string Text)[] Find(string needle, int max, out bool truncated)
    {
        (var matches, truncated) = Finder?.Invoke(needle, max) ?? ([], false);
        return matches;
    }
}
