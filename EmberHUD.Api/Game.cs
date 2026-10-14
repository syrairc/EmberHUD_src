using System.Numerics;

namespace EmberHUD;

public static class Game
{
    static int _minimapRescans;

    public static GameState State { get; internal set; } = GameState.Empty;

    public static void RescanMinimap() => Interlocked.Increment(ref _minimapRescans);

    internal static bool TakeMinimapRescan() => Interlocked.Exchange(ref _minimapRescans, 0) > 0;
}

public enum EntityKind : byte { Monster, Ally, Player, Portal, Npc, Interactive, Coffin, Altar, Elevator, MapObject, Destructible }

public enum Rarity : byte { Normal, Magic, Rare, Boss }

public sealed record GameState(string Status, int AreaId, Player? Player, Entity[] Entities, Terrain? Terrain, float? MinimapRotation = null)
{
    public static readonly GameState Empty = new("starting", 0, null, [], null);

    public MapIcon[] MapIcons { get; init; } = [];

    public ExploredTrail? Explored { get; init; }

    public Camera? Camera { get; init; }

    public GameUi Ui { get; init; } = GameUi.Empty;

    public TradePage? Trade { get; init; }

    public MapInfo? Map { get; init; }
}

public sealed record Player(Vector3 Position, Vector2 Forward);

public readonly record struct Entity(int Id, int CfgId, Vector3 Position, EntityKind Kind, Rarity Rarity)
{
    public nint Address { get; init; }
}

public readonly record struct MapIcon(int Type, string Texture, Vector2 Position, float Size, float Rotation, int EntityId)
{
    public string Name { get; init; } = "";
}

public sealed record ExploredTrail(Vector2[] Points, float Radius);

public enum PanelKind : byte { Other, Inventory, Stash, WorldMap, Skills, Talents, Vendor, Settings }

public readonly record struct GamePanel(string Name, PanelKind Kind, Vector2 Min, Vector2 Max, bool FullScreen)
{
    public nint Address { get; init; }

    public bool Contains(Vector2 point) => point.X >= Min.X && point.X < Max.X && point.Y >= Min.Y && point.Y < Max.Y;
}

public sealed record GameUi(GamePanel[] Panels)
{
    internal static volatile Func<nint, string, (Vector2 Min, Vector2 Max)?>? Finder;

    public static readonly GameUi Empty = new([]);

    public bool AnyFullScreen => Panels.Any(p => p.FullScreen);

    public GamePanel? Get(PanelKind kind)
    {
        for (var i = Panels.Length - 1; i >= 0; i--)
            if (Panels[i].Kind == kind) return Panels[i];
        return null;
    }

    public bool Covers(Vector2 point) => Panels.Any(p => p.Contains(point));

    public static (Vector2 Min, Vector2 Max)? Find(GamePanel panel, string childName) =>
        panel.Address != 0 && Finder is { } finder ? finder(panel.Address, childName) : null;
}

public enum ContentState : byte { Pending, Active, Done }

public readonly record struct MapContent(string Name, string Icon, ContentState State, int Done = 0, int Total = 1);

public sealed record MapInfo(string Name, int MonsterLevel, int? Remaining, string? XpGain, bool IsTown, MapContent[] Content, string[] Modifiers);

public sealed record TradePage(int Page, bool LastPage, int CellsPerPage, TradeListing[] Listings, string PriceIcon);

public readonly record struct TradeListing(int Slot, string Name, long Quantity, long Price, string? Status, Vector2 Min, Vector2 Max);

public sealed record Camera(Vector3 Location, Vector3 Rotation, float Fov)
{
    public Vector2? ToScreen(Vector3 world, Vector2 viewport)
    {
        if (!float.IsFinite(Fov) || Fov <= 0 || Fov >= 180) return null;
        var (sp, cp) = MathF.SinCos(Rotation.X * MathF.PI / 180);
        var (sy, cy) = MathF.SinCos(Rotation.Y * MathF.PI / 180);
        var (sr, cr) = MathF.SinCos(Rotation.Z * MathF.PI / 180);
        var forward = new Vector3(cp * cy, cp * sy, sp);
        var right = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
        var up = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        var delta = world - Location;
        var depth = Vector3.Dot(delta, forward);
        if (depth < 1f) return null;
        var centre = viewport / 2;
        var focal = centre.X / MathF.Tan(Fov * MathF.PI / 360);
        var screen = new Vector2(centre.X + Vector3.Dot(delta, right) * focal / depth, centre.Y - Vector3.Dot(delta, up) * focal / depth);
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

    public static (string Key, string Text)[] Find(string needle, int max = 50) => Find(needle, max, out _);

    public static (string Key, string Text)[] Find(string needle, int max, out bool truncated)
    {
        (var matches, truncated) = Finder?.Invoke(needle, max) ?? ([], false);
        return matches;
    }
}
