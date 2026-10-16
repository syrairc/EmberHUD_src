using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class EntityNamerTests
{
    static Entity Make(EntityKind kind, int cfgId, int id = 1) => new(id, cfgId, Vector3.Zero, kind, Rarity.Normal);

    static EntityNamer NamerFor(FakeWorld world) => new(new LocText(world.Memory, FakeWorld.Module));

    static FakeWorld WorldWith(params (string, string)[] entries)
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, entries);
        return world;
    }

    [Fact]
    public void NamesAMonsterFromItsConfigId()
    {
        var world = WorldWith((Offsets.NpcNameKey + 1080002, "Spike Larva"));
        var named = NamerFor(world).Apply([Make(EntityKind.Monster, 1080002)]);
        Assert.Equal("Spike Larva", named[0].Name);
    }

    [Fact]
    public void AllyUsesTheServantKey()
    {
        var world = WorldWith((Offsets.ServantNameKey + 5320, "Traveling Lizard - Flower"), (Offsets.NpcNameKey + 5320, "Wrong"));
        Assert.Equal("Traveling Lizard - Flower", NamerFor(world).Apply([Make(EntityKind.Ally, 5320)])[0].Name);
    }

    [Fact]
    public void NpcUsesTheNpcViewKey()
    {
        var world = WorldWith((Offsets.NpcViewNameKey + 4250, "Aria"), (Offsets.NpcNameKey + 4250, "Wrong"));
        Assert.Equal("Aria", NamerFor(world).Apply([Make(EntityKind.Npc, 4250)])[0].Name);
    }

    [Fact]
    public void AMissInOneTableDoesNotHideTheSameIdInAnother()
    {
        var world = WorldWith((Offsets.NpcViewNameKey + 5, "Aria"));
        var named = NamerFor(world).Apply([Make(EntityKind.Monster, 5, 1), Make(EntityKind.Npc, 5, 2)]);
        Assert.Equal(("", "Aria"), (named[0].Name, named[1].Name));
    }

    [Fact]
    public void PlayerNamesFromTheReaderAreKept()
    {
        var world = WorldWith((Offsets.NpcNameKey + 54, "Wrong"));
        var player = Make(EntityKind.Player, 54) with { Name = "Terayen#8674" };
        Assert.Equal("Terayen#8674", NamerFor(world).Apply([player])[0].Name);
    }

    [Fact]
    public void MissingKeyGivesEmptyName()
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        Assert.Equal("", NamerFor(world).Apply([Make(EntityKind.Monster, 6)])[0].Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ZeroAndNegativeConfigIdsGiveEmptyName(int cfgId)
    {
        var world = WorldWith((Offsets.NpcNameKey + cfgId, "Nope"));
        Assert.Equal("", NamerFor(world).Apply([Make(EntityKind.Monster, cfgId)])[0].Name);
    }

    [Theory]
    [InlineData(EntityKind.Portal)]
    [InlineData(EntityKind.Interactive)]
    [InlineData(EntityKind.MapObject)]
    public void KindsWithoutANamePathGiveEmptyName(EntityKind kind)
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        Assert.Equal("", NamerFor(world).Apply([Make(kind, 5)])[0].Name);
    }

    [Fact]
    public void OneLookupPerConfigIdPerFrame()
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        var before = world.Memory.Reads.Count;
        NamerFor(world).Apply([Make(EntityKind.Monster, 5)]);
        var single = world.Memory.Reads.Count - before;

        before = world.Memory.Reads.Count;
        var many = NamerFor(world).Apply([.. Enumerable.Range(0, 50).Select(i => Make(EntityKind.Monster, 5, i))]);
        Assert.Equal(single, world.Memory.Reads.Count - before);
        Assert.All(many, e => Assert.Equal("Helper", e.Name));
    }

    [Fact]
    public void MissesAreNotRehashedNextFrame()
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        var namer = NamerFor(world);
        namer.Apply([Make(EntityKind.Monster, 6)]);
        var before = world.Memory.Reads.Count;
        namer.Apply([Make(EntityKind.Monster, 6)]);
        Assert.Equal(1, world.Memory.Reads.Count - before);
    }

    [Fact]
    public void ChangingLanguageDropsCachedNames()
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        var namer = NamerFor(world);
        Assert.Equal("Helper", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
        world.SetLanguageTable(0, 7, (Offsets.NpcNameKey + 5, "Helfer"));
        Assert.Equal("Helfer", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
    }

    [Fact]
    public void MissIsRetriedAfterLanguageChange()
    {
        var world = WorldWith(("x", "y"));
        var namer = NamerFor(world);
        Assert.Equal("", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
        world.SetLanguageTable(0, 7, (Offsets.NpcNameKey + 5, "Helfer"));
        Assert.Equal("Helfer", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
    }

    [Fact]
    public void UnreadableTableIsRetriedNextFrame()
    {
        var world = WorldWith((Offsets.NpcNameKey + 5, "Helper"));
        world.Memory.TryReadPtr(FakeWorld.Module + Offsets.LanguageBucketsRva, out var languageBuckets);
        world.Memory.TryReadPtr(languageBuckets, out var languages);
        var countAt = languages + Offsets.LanguageNodeMap + Offsets.HashBucketCount;
        world.Memory.TryRead(countAt, out uint count);
        var namer = NamerFor(world);

        world.Memory.Write(countAt, 0u);
        Assert.Equal("", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
        world.Memory.Write(countAt, count);
        Assert.Equal("Helper", namer.Apply([Make(EntityKind.Monster, 5)])[0].Name);
    }
}
