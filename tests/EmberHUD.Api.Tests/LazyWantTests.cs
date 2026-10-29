using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class LazyWantTests
{
    sealed record Gate(string Name, Func<GameState, object?> Read, Action<long> SetAsked, Func<bool> Wanted);

    static Gate Of(string name, Func<GameState, object?> read, string want)
    {
        var type = typeof(Game);
        var asked = type.GetProperty(want + "Asked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var wanted = type.GetMethod(want + "Wanted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return new(name, read, v => asked.SetValue(null, v), () => (bool)wanted.Invoke(null, [Stopwatch.GetTimestamp(), 3.0])!);
    }

    static readonly Gate[] Gates =
    [
        Of("Projectiles", s => s.Projectiles, "Projectiles"),
        Of("GroundHazards", s => s.GroundHazards, "GroundHazards"),
        Of("Fighters", s => s.Fighters, "Fighters"),
        Of("Bag", s => s.Bag, "Inventory"),
        Of("Booth", s => s.Booth, "Inventory"),
        Of("Stash", s => s.Stash, "Inventory"),
        Of("StashItems", s => s.StashItems, "Inventory"),
        Of("Inventory", s => s.Inventory, "Inventory"),
        Of("BagCapacities", s => s.BagCapacities, "Inventory"),
        Of("TradeGoods", s => s.TradeGoods, "TradeGoods"),
        Of("Tartarus", s => s.Tartarus, "Tartarus"),
        Of("Tarot", s => s.Tarot, "Tarot"),
        Of("Vorax", s => s.Vorax, "Vorax"),
        Of("FrozenCanvas", s => s.FrozenCanvas, "FrozenCanvas"),
        Of("Xp", s => s.Xp, "Xp"),
        Of("XpBar", s => s.XpBar, "Xp"),
        Of("Death", s => s.Death, "Xp"),
        Of("ItemMenu", s => s.ItemMenu, "ItemMenu"),
        Of("GroundItems", s => s.GroundItems, "GroundItems"),
        Of("Buffs", s => s.Buffs, "Character"),
        Of("Control", s => s.Control, "Character"),
        Of("Skills", s => s.Skills, "Character"),
        Of("SkillSlots", s => s.SkillSlots, "Character"),
        Of("Combat", s => s.Combat, "Combat"),
        Of("Entity.Cast", s => default(Entity).Cast, "Casts"),
    ];

    public static TheoryData<string> AllGates() => [.. Gates.Select(g => g.Name)];

    static Gate Named(string name) => Gates.Single(g => g.Name == name);

    [Theory]
    [MemberData(nameof(AllGates))]
    public void ReadingAGatedPropertyAsksForIt(string name)
    {
        var gate = Named(name);
        gate.SetAsked(0);
        Assert.False(gate.Wanted());
        _ = gate.Read(GameState.Empty);
        Assert.True(gate.Wanted());
    }

    [Theory]
    [MemberData(nameof(AllGates))]
    public void PeekSuppressesTheAsk(string name)
    {
        var gate = Named(name);
        gate.SetAsked(0);
        using (Game.Peek()) _ = gate.Read(GameState.Empty);
        Assert.False(gate.Wanted());
    }

    [Fact]
    public void PeekNestsAndSurvivesDoubleDispose()
    {
        var gate = Gates[0];
        gate.SetAsked(0);
        var outer = Game.Peek();
        var inner = Game.Peek();
        inner.Dispose();
        inner.Dispose();
        _ = gate.Read(GameState.Empty);
        Assert.False(gate.Wanted());
        outer.Dispose();
        _ = gate.Read(GameState.Empty);
        Assert.True(gate.Wanted());
    }

    [Fact]
    public void PeekEndsWithItsScope()
    {
        var gate = Gates[0];
        Game.Peek().Dispose();
        Game.Peek().Dispose();
        gate.SetAsked(0);
        _ = gate.Read(GameState.Empty);
        Assert.True(gate.Wanted());
    }

    [Fact]
    public void ReadingTartarusLeavesTheRawWindowClosed()
    {
        Game.TartarusRawAsked = 0;
        _ = GameState.Empty.Tartarus;
        Assert.False(Game.TartarusRawWanted(Stopwatch.GetTimestamp(), 3));
    }

    [Fact]
    public void EqualityAndWithDoNotAsk()
    {
        int[] capacities = [1, 2];
        InventoryItem[] items = [];
        FightEntity[] fighters = [];
        var a = new GameState { BagCapacities = capacities, Inventory = items, Fighters = fighters, Xp = new XpState(1, 2, 3) };
        var b = new GameState { BagCapacities = capacities, Inventory = items, Fighters = fighters, Xp = new XpState(1, 2, 3) };
        foreach (var gate in Gates) gate.SetAsked(0);
        var c = a with { AreaId = 5 };
        Assert.True(a == b);
        Assert.False(a == c);
        var entity = new Entity(1, 2, default, EntityKind.Monster, Rarity.Normal);
        Assert.True(entity == entity with { Name = "x" } with { Name = "" });
        foreach (var gate in Gates) Assert.False(gate.Wanted(), gate.Name);
    }
}
