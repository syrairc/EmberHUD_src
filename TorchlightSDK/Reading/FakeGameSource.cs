using System.Collections;
using System.Diagnostics;
using System.Numerics;

namespace TorchlightSDK.Host.Reading;

internal sealed class FakeGameSource(Func<long> clock) : IGameSource
{
    private const float LapSeconds = 20f;
    private const float WalkRadius = 300f;
    private const float OuterRadius = 900f;
    private const float InnerRadius = 120f;
    private const int Cells = 80;
    private const float CellSize = 25f;
    private static readonly Vector2 Center = new(1000, 1000);

    private static readonly Terrain SceneTerrain = BuildTerrain();
    private static readonly Entity[] SceneEntities = BuildEntities();

    public bool TryRead(out GameState state, out string? failure)
    {
        var angle = MathF.Tau * (float)(clock() / (double)Stopwatch.Frequency % LapSeconds / LapSeconds);
        var player = new Player(
            new Vector3(Center.X + WalkRadius * MathF.Cos(angle), Center.Y + WalkRadius * MathF.Sin(angle), 0),
            new Vector2(-MathF.Sin(angle), MathF.Cos(angle)));
        state = new GameState("fake game", 1, player, SceneEntities, SceneTerrain);
        failure = null;
        return true;
    }

    public void Dispose()
    {
    }

    private static Terrain BuildTerrain()
    {
        var walkable = new BitArray(Cells * Cells);
        var terrain = new Terrain(Cells, Cells, CellSize, Vector2.Zero, true, walkable);
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
}
