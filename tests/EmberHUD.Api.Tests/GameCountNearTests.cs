namespace EmberHUD.Api.Tests;

public class GameCountNearTests
{
    static readonly Player Origin = new(new Vector3(0, 0, 0), Vector2.UnitX);

    static Entity Monster(float x, float y, Rarity rarity = Rarity.Normal, bool friendly = false, bool hidden = false, float z = 0) =>
        new(0, 0, new Vector3(x, y, z), EntityKind.Monster, rarity) { Friendly = friendly, Hidden = hidden };

    [Fact]
    public void CountsOnlyMonstersInRange()
    {
        var state = new GameState { Player = Origin, Entities = [Monster(3, 4), Monster(30, 0), Monster(0, 5)] };

        Assert.Equal(2, state.CountNear(5));
    }

    [Fact]
    public void ExcludesFriendlyHiddenAndOtherKinds()
    {
        var npc = new Entity(0, 0, new Vector3(1, 0, 0), EntityKind.Npc, Rarity.Normal);
        var ally = new Entity(0, 0, new Vector3(1, 0, 0), EntityKind.Ally, Rarity.Normal);
        var state = new GameState
        {
            Player = Origin,
            Entities = [Monster(1, 0, friendly: true), Monster(1, 0, hidden: true), npc, ally, Monster(1, 0)],
        };

        Assert.Equal(1, state.CountNear(10));
    }

    [Fact]
    public void MinRarityMagicSkipsNormalAndKeepsRareAndBoss()
    {
        var state = new GameState
        {
            Player = Origin,
            Entities = [Monster(1, 0, Rarity.Normal), Monster(1, 0, Rarity.Magic), Monster(1, 0, Rarity.Rare), Monster(1, 0, Rarity.Boss)],
        };

        Assert.Equal(3, state.CountNear(10, Rarity.Magic));
        Assert.Equal(1, state.CountNear(10, Rarity.Boss));
    }

    [Fact]
    public void BoundaryDistanceIsInclusive()
    {
        var state = new GameState { Player = Origin, Entities = [Monster(3, 4)] };

        Assert.Equal(1, state.CountNear(5));
        Assert.Equal(0, state.CountNear(4.99f));
    }

    [Fact]
    public void NoPlayerCountsZero()
    {
        var state = new GameState { Entities = [Monster(0, 0)] };

        Assert.Equal(0, state.CountNear(100));
    }

    [Fact]
    public void NegativeOrNaNRadiusCountsZero()
    {
        var state = new GameState { Player = Origin, Entities = [Monster(0, 0)] };

        Assert.Equal(0, state.CountNear(-1));
        Assert.Equal(0, state.CountNear(float.NaN));
    }

    [Fact]
    public void IgnoresHeight()
    {
        var state = new GameState { Player = new Player(new Vector3(0, 0, 500), Vector2.UnitX), Entities = [Monster(1, 0, z: -900)] };

        Assert.Equal(1, state.CountNear(2));
    }

    static Entity WithInfo(EntityKind kind, FightArchetype? archetype, bool friendly = false) =>
        new(0, 0, new Vector3(1, 0, 0), kind, Rarity.Normal) { Friendly = friendly, Info = archetype is { } a ? new FightInfo(a, FightCamps.Hostile, 1, 0) : null };

    [Theory]
    [InlineData(EntityKind.Monster, null, false, true)]
    [InlineData(EntityKind.Monster, FightArchetype.Monster, false, true)]
    [InlineData(EntityKind.Monster, FightArchetype.Minion, false, false)]
    [InlineData(EntityKind.Monster, FightArchetype.Monster, true, false)]
    [InlineData(EntityKind.Ally, FightArchetype.Monster, false, false)]
    [InlineData(EntityKind.Npc, null, false, false)]
    public void IsEnemyTruthTable(EntityKind kind, FightArchetype? archetype, bool friendly, bool enemy) =>
        Assert.Equal(enemy, WithInfo(kind, archetype, friendly).IsEnemy);

    [Fact]
    public void SkipsNonMonsterArchetypesButCountsInfoNullAndMonsters()
    {
        var state = new GameState
        {
            Player = Origin,
            Entities = [WithInfo(EntityKind.Monster, FightArchetype.Minion), WithInfo(EntityKind.Monster, null), WithInfo(EntityKind.Monster, FightArchetype.Monster)],
        };

        Assert.Equal(2, state.CountNear(10));
    }
}
