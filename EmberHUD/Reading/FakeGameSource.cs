using System.Collections;
using System.Diagnostics;
using System.Numerics;

namespace EmberHUD.Host.Reading;

internal sealed class FakeGameSource(Func<long> clock) : IGameSource
{
    private const float LapSeconds = 20f;
    private const float WalkRadius = 300f;
    private const float OuterRadius = 900f;
    private const float InnerRadius = 120f;
    private const int Cells = 80;
    private const float CellSize = 25f;
    private const float TrailRadius = 250f;
    private static readonly Vector2 Center = new(1000, 1000);
    private const float UiCycleSeconds = 10f;
    private const float InventoryOpenSeconds = 4f;
    private static readonly GameUi InventoryOpen = new([new GamePanel("PCBag_C", PanelKind.Inventory, new Vector2(2400, 200), new Vector2(3300, 1200), false)]);
    private const float VigilCycleSeconds = 30f;
    private const float MarketPageSeconds = 15f;
    private const int MarketPageCount = 3;
    private const int MarketColumns = 18;
    private const int MarketCells = MarketColumns * 4;
    private const int MarketLastPageListings = 50;
    private const int MarketSoldSlot = 7;
    private static readonly TradeListing[][] MarketPages = BuildMarket();
    private static readonly TradeListing[] MarketFirstPageSold = [.. MarketPages[0].Select(l => l.Slot == MarketSoldSlot ? l with { Name = "", Status = "Sold" } : l)];
    private static readonly string MarketPriceIcon = EmberHUD.Draw.GameIconPrefix + Offsets.TradePriceTexture;

    private static readonly Terrain SceneTerrain = BuildTerrain();
    private static readonly Entity[] SceneEntities = BuildEntities();
    private static readonly MapIcon[] SceneIcons = BuildIcons();

    private readonly List<Vector2> _trail = [];
    private ExploredTrail _published = new([], TrailRadius);
    private int _trailArea = -1;
    private long _nextSample;

    public bool TryRead(out GameState state, out string? failure)
    {
        var now = clock();
        var angle = MathF.Tau * (float)(now / (double)Stopwatch.Frequency % LapSeconds / LapSeconds);
        var player = new Player(
            new Vector3(Center.X + WalkRadius * MathF.Cos(angle), Center.Y + WalkRadius * MathF.Sin(angle), 0),
            new Vector2(-MathF.Sin(angle), MathF.Cos(angle)));
        state = new GameState("fake game", 1, player, SceneEntities, SceneTerrain, -90)
        {
            MapIcons = SceneIcons,
            Explored = Sample(1, new Vector2(player.Position.X, player.Position.Y), now),
            Camera = new Camera(player.Position + new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90),
            Ui = UiAt(now),
            Map = MapAt(now),
            Trade = TradeAt(now),
            WoodStake = WoodStakeAt(now),
        };
        failure = null;
        return true;
    }

    internal static WoodStakeStats WoodStakeAt(long now)
    {
        var seconds = now / (double)Stopwatch.Frequency;
        var unlimited = (long)(6.0e12 + 2.5e12 * Math.Sin(seconds / 4));
        const long Average = 6_200_000_000_000;
        const long Cap = 5_000_000_000_000;
        return new WoodStakeStats(true, (int)(seconds % 60), Cap, Math.Min(unlimited, Cap), Cap, unlimited, 8_500_000_000_000, Average);
    }

    internal static GameUi UiAt(long now) =>
        now / (double)Stopwatch.Frequency % UiCycleSeconds < InventoryOpenSeconds ? InventoryOpen : GameUi.Empty;

    internal static MapInfo MapAt(long now)
    {
        var seconds = now / (double)Stopwatch.Frequency;
        var vigil = (seconds % VigilCycleSeconds) switch { < 10 => ContentState.Pending, < 20 => ContentState.Active, _ => ContentState.Done };
        return new MapInfo("Rainforest of Divine Legacy", 78, Math.Max(0, 272 - (int)(seconds % 60 * 4)), "1%", false,
            [new MapContent("Vigil", "svg:quest", vigil), new MapContent("The Devil", "svg:gem", ContentState.Active, 1, 2), new MapContent("The Hermit", "svg:gem", ContentState.Pending)],
            ["+15% additional Drop Quantity", "1 additional Rare monsters"]);
    }

    internal static TradePage TradeAt(long now)
    {
        var seconds = now / (double)Stopwatch.Frequency;
        var step = (long)(seconds / MarketPageSeconds);
        var page = (int)(step % MarketPageCount) + 1;
        var sold = step / MarketPageCount % 2 == 1;
        var listings = page == 1 && sold ? MarketFirstPageSold : MarketPages[page - 1];
        return new TradePage(page, page == MarketPageCount, MarketCells, listings, MarketPriceIcon);
    }

    internal ExploredTrail Sample(int areaId, Vector2 at, long now)
    {
        if (areaId != _trailArea)
        {
            _trailArea = areaId;
            _trail.Clear();
            _published = new ExploredTrail([], TrailRadius);
            _nextSample = now;
        }
        if (now < _nextSample) return _published;
        _nextSample = now + Stopwatch.Frequency / 2;
        if (_trail.Count > 0 && MathF.Abs(at.X - _trail[^1].X) <= 0.5f && MathF.Abs(at.Y - _trail[^1].Y) <= 0.5f) return _published;
        _trail.Add(at);
        return _published = new ExploredTrail([.. _trail], TrailRadius);
    }

    public void Dispose()
    {
    }

    private static Terrain BuildTerrain()
    {
        var walkable = new BitArray(Cells * Cells);
        var terrain = new Terrain(Cells, Cells, CellSize, Vector2.Zero, walkable);
        for (var row = 0; row < Cells; row++)
            for (var col = 0; col < Cells; col++)
            {
                var distance = Vector2.Distance(terrain.CellCenter(col, row), Center);
                var blocked = col is >= 55 and < 61 && row is >= 37 and < 43;
                walkable[row * Cells + col] = distance is >= InnerRadius and <= OuterRadius && !blocked;
            }
        return terrain;
    }

    private static Entity[] BuildEntities()
    {
        var entities = new List<Entity>();
        void Add(Vector2 offset, EntityKind kind, Rarity rarity)
        {
            var index = entities.Count;
            entities.Add(new Entity(index + 1, 1000 + index, new Vector3(Center + offset, 0), kind, rarity));
        }

        Rarity[] rarities = [Rarity.Normal, Rarity.Magic, Rarity.Rare];
        for (var i = 0; i < 8; i++)
        {
            var angle = MathF.Tau * i / 8;
            Add(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 600, EntityKind.Monster, i == 7 ? Rarity.Boss : rarities[i % 3]);
        }
        Add(new Vector2(750, 0), EntityKind.Portal, Rarity.Normal);
        Add(new Vector2(-750, 0), EntityKind.Portal, Rarity.Normal);
        Add(new Vector2(200, 0), EntityKind.Npc, Rarity.Normal);
        Add(new Vector2(0, -200), EntityKind.Interactive, Rarity.Normal);
        return [.. entities.OrderBy(e => e.Kind).ThenBy(e => e.Rarity)];
    }

    private static MapIcon[] BuildIcons() =>
    [
        .. SceneEntities
            .Where(e => e.Kind is EntityKind.Portal or EntityKind.Interactive || e.Rarity is Rarity.Rare or Rarity.Boss)
            .Select(e => e switch
            {
                { Kind: EntityKind.Portal } => new MapIcon(10, "UI_MiniMap_Portal", new Vector2(e.Position.X, e.Position.Y), 1, 0, e.Id) { Name = "Portal" },
                { Kind: EntityKind.Interactive } => new MapIcon(2001, "UI_MiniMap_Entrepot", new Vector2(e.Position.X, e.Position.Y), 1, 0, e.Id) { Name = "Entrepot" },
                { Rarity: Rarity.Boss } => new MapIcon(2, "UI_MiniMap_BOSS", new Vector2(e.Position.X, e.Position.Y), 1, 0, e.Id) { Name = "Boss" },
                _ => new MapIcon(1040, "UI_Fight_IconBoss", new Vector2(e.Position.X, e.Position.Y), 1, 0, e.Id) { Name = "EliteBoss" },
            }),
    ];

    private static TradeListing[][] BuildMarket()
    {
        var random = new Random(23);
        long[] stacks = [1000, 2000, 4999, 9999];
        return [.. Enumerable.Range(0, MarketPageCount).Select(page =>
        {
            var count = page == MarketPageCount - 1 ? MarketLastPageListings : MarketCells;
            return Enumerable.Range(0, count).Select(slot =>
            {
                var quantity = slot % 3 == 0 ? random.Next(1, 10000) : stacks[random.Next(stacks.Length)];
                var price = Math.Max(1, (long)Math.Round(quantity * (0.08 + random.NextDouble() * 0.12)));
                var min = new Vector2(203 + 169 * (slot % MarketColumns), 150 + 274 * (slot / MarketColumns));
                return new TradeListing(slot, "Precious Ember", quantity, price, null, min, min + new Vector2(163, 267));
            }).ToArray();
        })];
    }
}
