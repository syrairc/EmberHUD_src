using System.Diagnostics;
using System.Numerics;

namespace EmberHUD;

public static class Game
{
    static int _minimapRescans;
    static long _combatAsked;
    static long _inventoryAsked;
    static long _tradeGoodsAsked;
    static long _castsAsked;
    static long _projectilesAsked;
    static long _fightersAsked;
    static long _groundHazardsAsked;
    static long _characterAsked;
    static long _tartarusAsked;
    static long _tartarusRawAsked;
    static long _tarotAsked;
    static long _voraxAsked;
    static long _frozenCanvasAsked;
    static long _xpAsked;
    static long _itemMenuAsked;
    static long _groundItemsAsked;

    public static GameState State { get; internal set; } = GameState.Empty;

    internal static Func<nint>? WindowSource { get; set; }

    public static nint WindowHandle => WindowSource?.Invoke() ?? 0;

    public static string? ExePath { get; internal set; }

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

    public static void WantTradeGoods() => Interlocked.Exchange(ref _tradeGoodsAsked, Stopwatch.GetTimestamp());

    internal static long TradeGoodsAsked
    {
        get => Interlocked.Read(ref _tradeGoodsAsked);
        set => Interlocked.Exchange(ref _tradeGoodsAsked, value);
    }

    internal static bool TradeGoodsWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _tradeGoodsAsked);
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

    public static void WantFighters() => Interlocked.Exchange(ref _fightersAsked, Stopwatch.GetTimestamp());

    internal static long FightersAsked
    {
        get => Interlocked.Read(ref _fightersAsked);
        set => Interlocked.Exchange(ref _fightersAsked, value);
    }

    internal static bool FightersWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _fightersAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantGroundHazards() => Interlocked.Exchange(ref _groundHazardsAsked, Stopwatch.GetTimestamp());

    internal static long GroundHazardsAsked
    {
        get => Interlocked.Read(ref _groundHazardsAsked);
        set => Interlocked.Exchange(ref _groundHazardsAsked, value);
    }

    internal static bool GroundHazardsWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _groundHazardsAsked);
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

    public static void WantTartarus() => Interlocked.Exchange(ref _tartarusAsked, Stopwatch.GetTimestamp());

    internal static long TartarusAsked
    {
        get => Interlocked.Read(ref _tartarusAsked);
        set => Interlocked.Exchange(ref _tartarusAsked, value);
    }

    internal static bool TartarusWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _tartarusAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantTartarusRaw() => Interlocked.Exchange(ref _tartarusRawAsked, Stopwatch.GetTimestamp());

    internal static long TartarusRawAsked
    {
        get => Interlocked.Read(ref _tartarusRawAsked);
        set => Interlocked.Exchange(ref _tartarusRawAsked, value);
    }

    internal static bool TartarusRawWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _tartarusRawAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantTarot() => Interlocked.Exchange(ref _tarotAsked, Stopwatch.GetTimestamp());

    internal static long TarotAsked
    {
        get => Interlocked.Read(ref _tarotAsked);
        set => Interlocked.Exchange(ref _tarotAsked, value);
    }

    internal static bool TarotWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _tarotAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantVorax() => Interlocked.Exchange(ref _voraxAsked, Stopwatch.GetTimestamp());

    internal static long VoraxAsked
    {
        get => Interlocked.Read(ref _voraxAsked);
        set => Interlocked.Exchange(ref _voraxAsked, value);
    }

    internal static bool VoraxWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _voraxAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantFrozenCanvas() => Interlocked.Exchange(ref _frozenCanvasAsked, Stopwatch.GetTimestamp());

    internal static long FrozenCanvasAsked
    {
        get => Interlocked.Read(ref _frozenCanvasAsked);
        set => Interlocked.Exchange(ref _frozenCanvasAsked, value);
    }

    internal static bool FrozenCanvasWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _frozenCanvasAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantXp() => Interlocked.Exchange(ref _xpAsked, Stopwatch.GetTimestamp());

    internal static long XpAsked
    {
        get => Interlocked.Read(ref _xpAsked);
        set => Interlocked.Exchange(ref _xpAsked, value);
    }

    internal static bool XpWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _xpAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantItemMenu() => Interlocked.Exchange(ref _itemMenuAsked, Stopwatch.GetTimestamp());

    internal static long ItemMenuAsked
    {
        get => Interlocked.Read(ref _itemMenuAsked);
        set => Interlocked.Exchange(ref _itemMenuAsked, value);
    }

    internal static bool ItemMenuWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _itemMenuAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    public static void WantGroundItems() => Interlocked.Exchange(ref _groundItemsAsked, Stopwatch.GetTimestamp());

    internal static long GroundItemsAsked
    {
        get => Interlocked.Read(ref _groundItemsAsked);
        set => Interlocked.Exchange(ref _groundItemsAsked, value);
    }

    internal static bool GroundItemsWanted(long now, double seconds)
    {
        var asked = Interlocked.Read(ref _groundItemsAsked);
        return asked != 0 && now - asked < seconds * Stopwatch.Frequency;
    }

    [ThreadStatic]
    static int _peekDepth;

    internal static void Touch(Action want)
    {
        if (_peekDepth == 0) want();
    }

    public static IDisposable Peek()
    {
        BeginPeek();
        return new PeekScope();
    }

    internal static void BeginPeek() => _peekDepth++;

    internal static void EndPeek() => _peekDepth--;

    sealed class PeekScope : IDisposable
    {
        bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EndPeek();
        }
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

    Projectile[] _projectiles = [];

    public Projectile[] Projectiles
    {
        get
        {
            Game.Touch(Game.WantProjectiles);
            return _projectiles;
        }
        init => _projectiles = value;
    }

    FightEntity[]? _fighters;

    public FightEntity[]? Fighters
    {
        get
        {
            Game.Touch(Game.WantFighters);
            return _fighters;
        }
        init => _fighters = value;
    }

    GroundHazard[] _groundHazards = [];

    public GroundHazard[] GroundHazards
    {
        get
        {
            Game.Touch(Game.WantGroundHazards);
            return _groundHazards;
        }
        init => _groundHazards = value;
    }

    public Terrain? Terrain { get; init; }

    public float? MinimapRotation { get; init; }

    public MapIcon[] MapIcons { get; init; } = [];

    public ExploredTrail? Explored { get; init; }

    public Camera? Camera { get; init; }

    public GameUi Ui { get; init; } = GameUi.Empty;

    public TradePage? Trade { get; init; }

    public EquipCompare? Compare { get; init; }

    TradeGood[]? _tradeGoods;

    public TradeGood[]? TradeGoods
    {
        get
        {
            Game.Touch(Game.WantTradeGoods);
            return _tradeGoods;
        }
        init => _tradeGoods = value;
    }

    BagView? _bag;

    public BagView? Bag
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _bag;
        }
        init => _bag = value;
    }
    BagView? _booth;

    public BagView? Booth
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _booth;
        }
        init => _booth = value;
    }

    public MapRewardView? MapReward { get; init; }

    StashView? _stash;

    public StashView? Stash
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _stash;
        }
        init => _stash = value;
    }

    InventoryItem[]? _stashItems;

    public InventoryItem[]? StashItems
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _stashItems;
        }
        init => _stashItems = value;
    }

    public MapInfo? Area { get; init; }

    public WoodStakeStats? WoodStake { get; init; }

    CombatStats? _combat;

    public CombatStats? Combat
    {
        get
        {
            Game.Touch(Game.WantCombat);
            return _combat;
        }
        init => _combat = value;
    }

    public QuestTarget[] QuestTargets { get; init; } = [];

    InventoryItem[]? _inventory;

    public InventoryItem[]? Inventory
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _inventory;
        }
        init => _inventory = value;
    }

    int[]? _bagCapacities;

    public int[]? BagCapacities
    {
        get
        {
            Game.Touch(Game.WantInventory);
            return _bagCapacities;
        }
        init => _bagCapacities = value;
    }

    TartarusState? _tartarus;

    public TartarusState? Tartarus
    {
        get
        {
            Game.Touch(Game.WantTartarus);
            return _tartarus;
        }
        init => _tartarus = value;
    }

    TarotState? _tarot;

    public TarotState? Tarot
    {
        get
        {
            Game.Touch(Game.WantTarot);
            return _tarot;
        }
        init => _tarot = value;
    }

    VoraxState? _vorax;

    public VoraxState? Vorax
    {
        get
        {
            Game.Touch(Game.WantVorax);
            return _vorax;
        }
        init => _vorax = value;
    }

    FrozenCanvasState? _frozenCanvas;

    public FrozenCanvasState? FrozenCanvas
    {
        get
        {
            Game.Touch(Game.WantFrozenCanvas);
            return _frozenCanvas;
        }
        init => _frozenCanvas = value;
    }

    XpState? _xp;

    public XpState? Xp
    {
        get
        {
            Game.Touch(Game.WantXp);
            return _xp;
        }
        init => _xp = value;
    }

    ScreenRect? _xpBar;

    public ScreenRect? XpBar
    {
        get
        {
            Game.Touch(Game.WantXp);
            return _xpBar;
        }
        init => _xpBar = value;
    }

    ItemMenu? _itemMenu;

    public ItemMenu? ItemMenu
    {
        get
        {
            Game.Touch(Game.WantItemMenu);
            return _itemMenu;
        }
        init => _itemMenu = value;
    }

    DeathState? _death;

    public DeathState? Death
    {
        get
        {
            Game.Touch(Game.WantXp);
            return _death;
        }
        init => _death = value;
    }

    GroundItem[]? _groundItems;

    public GroundItem[]? GroundItems
    {
        get
        {
            Game.Touch(Game.WantGroundItems);
            return _groundItems;
        }
        init => _groundItems = value;
    }

    Buff[]? _buffs;

    public Buff[]? Buffs
    {
        get
        {
            Game.Touch(Game.WantCharacter);
            return _buffs;
        }
        init => _buffs = value;
    }

    Skill[]? _skills;

    public Skill[]? Skills
    {
        get
        {
            Game.Touch(Game.WantCharacter);
            return _skills;
        }
        init => _skills = value;
    }

    ScreenRect?[]? _skillSlots;

    public ScreenRect?[]? SkillSlots
    {
        get
        {
            Game.Touch(Game.WantCharacter);
            return _skillSlots;
        }
        init => _skillSlots = value;
    }

    public GameMinimap? GameMinimap { get; init; }

    public BossBar? BossBar { get; init; }

    public bool? ChatOpen { get; init; }

    public bool? Paused { get; init; }

    const double BossBarTolerance = 0.1;

    public static Entity? BossOnBar(Entity[] entities, float percent)
    {
        Entity? best = null;
        var bestGap = BossBarTolerance;
        foreach (var entity in entities)
        {
            if (entity.Kind != EntityKind.Monster || entity.Rarity != Rarity.Boss || entity.Friendly || entity.Hidden || !(entity.MaxLife > 0)) continue;
            var gap = Math.Abs(entity.Life / entity.MaxLife - percent);
            if (gap > bestGap) continue;
            best = entity;
            bestGap = gap;
        }
        return best;
    }

    public Entity? BossOnBar() => BossBar is { } bar ? BossOnBar(Entities, bar.Percent) : null;

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

public sealed record XpState(int Level, long Exp, long ToNext);

public sealed record DeathState(float XpLossPercent, bool CanUseToken, bool? TokenChecked);

public sealed record BossBar(ScreenRect Rect, float Percent);

public sealed record Player(Vector3 Position, Vector2 Forward)
{
    public string? Name { get; init; }

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

    readonly Cast? _cast;

    public Cast? Cast
    {
        get
        {
            Game.Touch(Game.WantCasts);
            return _cast;
        }
        init => _cast = value;
    }
}

public readonly record struct Cast(int StoneId, byte State, float Progress, float SecondsLeft, Vector2 Target, Vector2 Direction)
{
    public string Name { get; init; } = "";
}

public readonly record struct Projectile(int Id, int OwnerId, Rarity OwnerRarity, Vector2 Position, Vector2 Origin, Vector2 Direction, float Radius, float Speed)
{
    public Vector2? OrbitCentre { get; init; }
}

public enum HazardKind { Area, Damage }

public readonly record struct GroundHazard(int Id, HazardKind Kind, Vector2 Centre, float Radius)
{
    public float InnerRadius { get; init; }
}

public readonly record struct ScreenRect(Vector2 Min, Vector2 Max)
{
    public float Width => Max.X - Min.X;

    public float Height => Max.Y - Min.Y;
}

public sealed record ItemMenu(ScreenRect? TakeOut, ScreenRect? Lock, ScreenRect? More, ScreenRect? DropSource, ScreenRect? Details, ScreenRect? Share, ScreenRect? PriceCheck);

public readonly record struct MapIcon(int Type, string Texture, Vector2 Position, float Size, float Rotation, int EntityId)
{
    public string Name { get; init; } = "";
}

public sealed record ExploredTrail(Vector2[] Buffer, int Count, float Radius)
{
    public ExploredTrail(Vector2[] points, float radius) : this(points, points.Length, radius) { }
}

public enum PanelKind : byte { Other, Inventory, Stash, WorldMap, Skills, Talents, Vendor, Settings }

public readonly record struct NamedRect(string Name, Vector2 Min, Vector2 Max);

public readonly record struct GamePanel(string Name, PanelKind Kind, Vector2 Min, Vector2 Max, bool FullScreen)
{
    public nint Address { get; init; }
    public NamedRect[] Parts { get; init; } = [];
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

public readonly record struct VictimDamage(double Effective, double Raw);

public sealed record CombatStats(long At, double Effective, double Raw, long Hits)
{
    static readonly IReadOnlyDictionary<int, VictimDamage> NoVictims = new Dictionary<int, VictimDamage>();

    public IReadOnlyDictionary<int, VictimDamage> ByVictim { get; init; } = NoVictims;

    public double Seconds => At / (double)Stopwatch.Frequency;
}

public readonly record struct Buff(int Id, string? Name, double Remaining, double Total, string? Icon = null, int Stacks = 1);

public readonly record struct Skill(int CfgId, int Slot, double CooldownRemaining, double CooldownTotal, int Charges, int MaxCharges, string? Icon = null)
{
    public string? Name { get; init; }

    public bool Ready => CooldownRemaining <= 0;
}

public sealed record InventoryItem(InventoryBag Bag, int Slot, string Id, int BaseId, int Level, int Quality, int RareValue, int Count, int StackMax, int Type)
{
    public int[] PrefixIds { get; init; } = [];
    public int[] SuffixIds { get; init; } = [];
    public bool Legendary { get; init; }

    public int SpecialId { get; init; }

    public bool Unidentified { get; init; }
}

public sealed record TradePage(int Page, bool LastPage, int CellsPerPage, TradeListing[] Listings, string PriceIcon);

public readonly record struct CompareRow(float? Damage, float? Survival);

public sealed record EquipCompare(CompareRow[] Rows)
{
    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }
    public long Version { get; init; }
}

public enum TradeAffixKind : byte { Implicit, Prefix, Suffix, BaseAttribute }

public readonly record struct TradeAffix(TradeAffixKind Kind, int Id, int Tier, float[] Rolls, string Text);

public sealed record TradeGood(long GoodId, int PriceType, long Price, string Status, InventoryItem Item)
{
    public bool Advanced { get; init; }
    public TradeAffix[] Affixes { get; init; } = [];
    public string[] Flags { get; init; } = [];
    public int ItemLevel { get; init; }

    public int SpecialId { get; init; }
    public int SkillId { get; init; }
    public int SkillType { get; init; }
    public int HoleNum { get; init; }
    public int EnhanceCount { get; init; }
    public bool Forged { get; init; }
    public bool Corrode { get; init; }

    public bool Unidentified { get; init; }
}

public readonly record struct TradeListing(int Slot, string Name, long Quantity, long Price, string? Status, Vector2 Min, Vector2 Max);

public readonly record struct BagCell(int Slot, Vector2 Min, Vector2 Max);

public sealed record BagView(InventoryBag Tab, Vector2 ClipMin, Vector2 ClipMax, BagCell[] Cells);

public sealed record StashView(int Tab, Vector2 ClipMin, Vector2 ClipMax, BagCell[] Cells);

public sealed record MapRewardItem(int BaseId, int Count, Vector2 Min, Vector2 Max, int SpecialId = 0);

public sealed record MapRewardView(MapRewardItem[] Items, bool Boss = false);

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

public readonly record struct FightBuff(int Id, int Stacks, double Remaining);

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
