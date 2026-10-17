using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class GroundItemReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory;
        public readonly FakeLua Lua;
        public readonly LuaTable Items;

        public Fixture(bool withManager = true, int logItems = 3, FakeWorld? world = null)
        {
            Memory = world?.Memory ?? new();
            Lua = new FakeLua(Memory, Module);
            var manager = Lua.NewTable(0, 2);
            Items = Lua.NewTable(0, logItems);
            Lua.SetTable(manager, Offsets.LuaGroundItems, Items);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaGroundManager, manager);
        }

        public GroundItemReader Reader() => new(Memory, Module);

        public void Drop(int key, int[] prefixes, int[] suffixes, bool owned = true, bool withTables = true, bool withDropId = true)
        {
            var entry = Lua.NewTable(0, 4);
            var drop = Lua.NewTable(0, 2);
            if (withDropId) Lua.SetInt(drop, Offsets.LuaGroundDropId, key);
            if (withTables)
            {
                var item = Lua.NewTable(0, 2);
                Lua.SetTable(item, Offsets.LuaGroundPrefix, Affixes(prefixes));
                Lua.SetTable(item, Offsets.LuaGroundSuffix, Affixes(suffixes));
                Lua.SetTable(drop, Offsets.LuaGroundItem, item);
            }

            Lua.SetTable(entry, Offsets.LuaGroundDropItem, drop);
            Lua.SetBool(entry, Offsets.LuaGroundBelong, owned);
            Lua.SetIntKeyTable(Items, key, entry);
        }

        public LuaTable Affixes(int[] ids)
        {
            var table = Lua.NewTable(ids.Length, 1);
            for (var i = 0; i < ids.Length; i++)
            {
                var row = Lua.NewTable(0, 2);
                Lua.SetInt(row, Offsets.LuaGroundAffixId, ids[i]);
                Lua.SetIntKeyTable(table, i + 1, row);
            }

            return table;
        }
    }

    [Fact]
    public void ReadsPrefixAndSuffixIds()
    {
        var f = new Fixture();
        f.Drop(1, [106060407, 140060404], []);

        var read = Assert.Single(f.Reader().ReadLua());

        Assert.Equal(1, read.DropId);
        Assert.True(read.Owned);
        Assert.Equal([106060407, 140060404], read.PrefixIds);
        Assert.Empty(read.SuffixIds);
    }

    [Fact]
    public void ReadsSuffixes()
    {
        var f = new Fixture();
        f.Drop(3, [], [200060101], owned: false);

        var read = Assert.Single(f.Reader().ReadLua());

        Assert.False(read.Owned);
        Assert.Equal([200060101], read.SuffixIds);
    }

    [Fact]
    public void HoleInTheKeysIsFine()
    {
        var f = new Fixture();
        f.Drop(4, [1], []);
        f.Drop(1, [2], []);
        f.Drop(2, [3], []);

        Assert.Equal([1, 2, 4], f.Reader().ReadLua().Select(g => g.DropId));
    }

    [Fact]
    public void ItemWithoutAffixTablesGivesEmptyArrays()
    {
        var f = new Fixture();
        f.Drop(5, [], [], withTables: false, withDropId: false);

        var read = Assert.Single(f.Reader().ReadLua());

        Assert.Equal(5, read.DropId);
        Assert.Empty(read.PrefixIds);
        Assert.Empty(read.SuffixIds);
    }

    [Fact]
    public void MissingManagerGivesEmptyArray() => Assert.Empty(new Fixture(withManager: false).Reader().ReadLua());

    [Fact]
    public void CapsAtMaxGroundItems()
    {
        var f = new Fixture(logItems: 10);
        for (var i = 1; i <= Offsets.MaxGroundItems + 5; i++) f.Lua.SetIntKeyTable(f.Items, i, f.Lua.NewTable(0, 1));

        Assert.Equal(Offsets.MaxGroundItems, f.Reader().ReadLua().Length);
    }

    [Fact]
    public void BadAffixRowIsSkipped()
    {
        var f = new Fixture();
        var entry = f.Lua.NewTable(0, 4);
        var drop = f.Lua.NewTable(0, 2);
        var item = f.Lua.NewTable(0, 2);
        var prefixes = f.Lua.NewTable(3, 1);
        var good = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(good, Offsets.LuaGroundAffixId, 106060407);
        f.Lua.SetIntKeyTable(prefixes, 1, good);
        f.Lua.SetIntKeyTable(prefixes, 2, f.Lua.NewTable(0, 1));
        f.Lua.SetTable(item, Offsets.LuaGroundPrefix, prefixes);
        f.Lua.SetTable(drop, Offsets.LuaGroundItem, item);
        f.Lua.SetTable(entry, Offsets.LuaGroundDropItem, drop);
        f.Lua.SetIntKeyTable(f.Items, 7, entry);

        var read = Assert.Single(f.Reader().ReadLua());

        Assert.Equal([106060407], read.PrefixIds);
    }

    private static FakeNameTagLabel Label(int dropId, float x = 10, float y = -20, float width = 100, float height = 30, bool valid = true, bool allocated = true) =>
        new(dropId, new(x, y), new(x + width, y + height), valid, allocated);

    private static FakeNameTagGroup Group(int key, Vector2 screenPos, params FakeNameTagLabel[] labels) => new(key, new(5, 6, 7), screenPos, labels);

    private static (GroundItemReader Reader, Fixture Fixture) Joined(FakeWorld world, params int[] dropIds)
    {
        var f = new Fixture(world: world);
        foreach (var id in dropIds) f.Drop(id, [100 + id], [200 + id], owned: id != 2);
        return (f.Reader(), f);
    }

    [Fact]
    public void JoinsRectsWithAffixes()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400), Label(2), Label(1))]);
        var (reader, _) = Joined(world, 1, 2);

        var items = reader.Read()!;

        Assert.Equal([1, 2], items.Select(i => i.DropId));
        var first = items[0];
        Assert.Equal(new Vector2(510, 380), first.LabelMin);
        Assert.Equal(new Vector2(610, 410), first.LabelMax);
        Assert.Equal(new Vector3(5, 6, 7), first.Position);
        Assert.Equal([101], first.PrefixIds);
        Assert.Equal([201], first.SuffixIds);
        Assert.True(first.Owned);
        Assert.False(items[1].Owned);
    }

    [Fact]
    public void ItemWithoutRectIsDropped()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400), Label(1))]);
        var (reader, _) = Joined(world, 1, 2);

        Assert.Equal([1], reader.Read()!.Select(i => i.DropId));
    }

    [Fact]
    public void RectWithoutAffixesIsDropped()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400), Label(1), Label(9))]);
        var (reader, _) = Joined(world, 1);

        Assert.Equal([1], reader.Read()!.Select(i => i.DropId));
    }

    [Fact]
    public void GroupNotInScreenIsSkipped()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400), Label(1)) with { InScreen = false }, Group(2, new(900, 400), Label(2))]);
        var (reader, _) = Joined(world, 1, 2);

        Assert.Equal([2], reader.Read()!.Select(i => i.DropId));
    }

    [Fact]
    public void InvalidAndInvertedRectsAreSkipped()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400),
            Label(1, valid: false),
            Label(2, width: 0),
            Label(3, height: -5),
            Label(4, x: float.NaN),
            Label(5, width: 1001),
            Label(6, height: 1001),
            Label(7))]);
        var (reader, _) = Joined(world, 1, 2, 3, 4, 5, 6, 7);

        Assert.Equal([7], reader.Read()!.Select(i => i.DropId));
    }

    [Fact]
    public void FreeSlotsAreIgnored()
    {
        var world = new FakeWorld();
        world.AddDropMgr([Group(1, new(500, 400), Label(1), Label(2, allocated: false))]);
        var (reader, _) = Joined(world, 1, 2);

        Assert.Equal([1], reader.Read()!.Select(i => i.DropId));
    }

    [Fact]
    public void ReturnsNullWhenDropMgrMissing()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("Other_C"), "DropMgr_0");
        var (reader, _) = Joined(world, 1);

        Assert.Null(reader.Read());
    }

    [Fact]
    public void ReturnsEmptyWhenNothingOnTheGround()
    {
        var world = new FakeWorld();
        world.AddDropMgr([]);
        var (reader, _) = Joined(world, 1);

        Assert.Empty(reader.Read()!);
    }

    [Fact]
    public void MissingDropMgrIsNotRescannedInsideTheWindow()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("Other_C"), "Other_0");
        long now = 0;
        var f = new Fixture(world: world);
        f.Drop(1, [1], []);
        var reader = new GroundItemReader(world.Memory, Module, () => now);
        var second = System.Diagnostics.Stopwatch.Frequency;
        int ChunkReads() => world.Memory.Reads.Count(r => r.Length >= 100 * Offsets.GObjectsItemSize);

        Assert.Null(reader.Read());
        Assert.Equal(1, ChunkReads());

        now += second;
        Assert.Null(reader.Read());
        Assert.Equal(1, ChunkReads());

        world.AddDropMgr([Group(1, new(500, 400), Label(1))]);
        now += second * Offsets.DropMgrRescanSeconds;
        Assert.Single(reader.Read()!);
        Assert.Equal(2, ChunkReads());
    }

    [Fact]
    public void ReturnsNullWhenGroupsCannotBeRead()
    {
        var world = new FakeWorld();
        var manager = world.AddDropMgr([Group(1, new(500, 400), Label(1))]);
        world.Memory.Write(manager + Offsets.DropMgrNameTagGroups + Offsets.TArrayNum, -1);
        var (reader, _) = Joined(world, 1);

        Assert.Null(reader.Read());
    }

    [Fact]
    public void LabelRectIsScreenPosPlusBox()
    {
        var (min, max) = GroundItemReader.LabelRect(new(500, 400), (new(-50, -10), new(50, 10)));

        Assert.Equal(new Vector2(450, 390), min);
        Assert.Equal(new Vector2(550, 410), max);
    }

    [Fact]
    public void FindsTheDropMgrAgainWhenItMoves()
    {
        var world = new FakeWorld();
        var first = world.AddDropMgr([Group(1, new(500, 400), Label(1))]);
        long now = 0;
        var f = new Fixture(world: world);
        f.Drop(1, [1], []);
        var reader = new GroundItemReader(world.Memory, Module, () => now);
        Assert.Single(reader.Read()!);

        world.Memory.Write(first + Offsets.UObjectClass, FakeWorld.Unmapped);
        world.AddDropMgr([Group(1, new(500, 400), Label(1))], objectName: "DropMgr_1");
        now += System.Diagnostics.Stopwatch.Frequency;

        Assert.Single(reader.Read()!);
    }

    [Fact]
    public void LuaIsCachedButRectsAreReadEveryPass()
    {
        var world = new FakeWorld();
        var first = world.AddDropMgr([Group(1, new(500, 400), Label(1))]);
        long now = 0;
        var f = new Fixture(world: world);
        f.Drop(1, [1], []);
        var reader = new GroundItemReader(world.Memory, Module, () => now);
        var luaSlot = Module + Offsets.LuaGlobalRva;
        int LuaReads() => world.Memory.Reads.Count(r => r.Address == luaSlot);
        Assert.Equal(new Vector2(510, 380), Assert.Single(reader.Read()!).LabelMin);
        Assert.Equal(1, LuaReads());

        world.Memory.Write(first + Offsets.UObjectClass, FakeWorld.Unmapped);
        world.AddDropMgr([Group(1, new(700, 400), Label(1))], objectName: "DropMgr_1");
        now += System.Diagnostics.Stopwatch.Frequency * Offsets.GroundItemsRefreshMilliseconds / 2000;

        Assert.Equal(new Vector2(710, 380), Assert.Single(reader.Read()!).LabelMin);
        Assert.Equal(1, LuaReads());

        now += System.Diagnostics.Stopwatch.Frequency;
        reader.Read();
        Assert.Equal(2, LuaReads());
    }
}
