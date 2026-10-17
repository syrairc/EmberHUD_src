using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class GroundItemReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    private sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
        public readonly FakeLua Lua;
        public readonly LuaTable Items;

        public Fixture(bool withManager = true, int logItems = 3)
        {
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
}
