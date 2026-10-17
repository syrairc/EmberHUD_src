using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class LuaReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    private static (FakeMemory Memory, FakeLua Lua, LuaReader Reader) Fixture()
    {
        var memory = new FakeMemory();
        var lua = new FakeLua(memory, Module);
        return (memory, lua, new LuaReader(memory, Module));
    }

    [Fact]
    public void HashMatchesLiveValues()
    {
        Assert.Equal(0x1D0A117Cu, LuaReader.Hash(0x2307D0F9, "ItemMgr"));
        Assert.Equal(0xA8698235u, LuaReader.Hash(0x2307D0F9, "ERewardType"));
    }

    [Fact]
    public void OpensTheGlobalsTable()
    {
        var (_, lua, reader) = Fixture();
        Assert.True(reader.TryOpen(out var globals));
        Assert.Equal(lua.Globals, globals);
    }

    [Fact]
    public void FindsStringIntTableValues()
    {
        var (_, lua, reader) = Fixture();
        var inner = lua.NewTable(0, 0);
        lua.SetString(lua.Globals, "name", "Gold");
        lua.SetInt(lua.Globals, "count", 42);
        lua.SetTable(lua.Globals, "inner", inner);
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryGetString(globals, "name", out var name));
        Assert.Equal("Gold", name);
        Assert.True(reader.TryGetInt(globals, "count", out var count));
        Assert.Equal(42, count);
        Assert.True(reader.TryGetTable(globals, "inner", out var found));
        Assert.Equal(inner, found);
        Assert.False(reader.TryGetInt(globals, "name", out _));
        Assert.False(reader.TryGetTable(globals, "missing", out _));
    }

    [Fact]
    public void FindsAKeyThatIsNotInItsMainPosition()
    {
        var (_, lua, reader) = Fixture();
        var table = lua.NewTable(0, 2);
        string first = "k0", second = "";
        for (var i = 1; i < 1000; i++)
            if (lua.MainPosition(table, $"k{i}") == lua.MainPosition(table, first)) { second = $"k{i}"; break; }

        Assert.NotEqual("", second);
        lua.SetInt(table, first, 1);
        lua.SetInt(table, second, 2);
        lua.SetTable(lua.Globals, "t", table);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var t));

        Assert.True(reader.TryGetInt(t, first, out var a));
        Assert.True(reader.TryGetInt(t, second, out var b));
        Assert.Equal((1, 2), (a, b));
    }

    [Fact]
    public void ShortAndLongStringsRead()
    {
        var (_, lua, reader) = Fixture();
        var longText = new string('x', 80);
        lua.SetString(lua.Globals, "short", "abc");
        lua.SetString(lua.Globals, "long", longText);
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryGetString(globals, "short", out var shortText));
        Assert.Equal("abc", shortText);
        Assert.True(reader.TryGetString(globals, "long", out var read));
        Assert.Equal(longText, read);
    }

    [Fact]
    public void IntKeyedTablesIncludesNodePartAndSkipsArrayHoles()
    {
        var (_, lua, reader) = Fixture();
        var list = lua.NewTable(3, 1);
        var a = lua.NewTable(0, 0);
        var b = lua.NewTable(0, 0);
        var c = lua.NewTable(0, 0);
        lua.SetIntKeyTable(list, 0, a);
        lua.SetIntKeyTable(list, 1, b);
        lua.SetIntKeyTable(list, 3, c);
        lua.SetTable(lua.Globals, "list", list);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "list", out var t));

        Assert.Equal([(1L, b), (3L, c), (0L, a)], reader.IntKeyedTables(t).ToArray());
        Assert.True(reader.TryGetTableAt(t, 3, out var at3));
        Assert.Equal(c, at3);
        Assert.True(reader.TryGetTableAt(t, 0, out var at0));
        Assert.Equal(a, at0);
        Assert.False(reader.TryGetTableAt(t, 2, out _));
    }

    [Fact]
    public void UnreadableGlobalSlotFails()
    {
        var memory = new FakeMemory();
        Assert.False(new LuaReader(memory, Module).TryOpen(out _));
    }

    [Fact]
    public void NullManagerFails()
    {
        var (memory, _, reader) = Fixture();
        memory.Write(Module + Offsets.LuaGlobalRva, 0L);
        Assert.False(reader.TryOpen(out _));
    }

    [Fact]
    public void NullStateFails()
    {
        var (_, lua, reader) = Fixture();
        lua.BreakState();
        Assert.False(reader.TryOpen(out _));
    }

    [Fact]
    public void TornTableIsRejected()
    {
        var (memory, lua, reader) = Fixture();
        var torn = lua.NewTable(1, 0);
        lua.SetTable(lua.Globals, "torn", torn);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "torn", out var t));
        memory.Write(t.Address + Offsets.LuaTableArraySize, 0x7FFFFFFFu);
        memory.Write(t.Address + Offsets.LuaTableLogSize, (byte)40);

        memory.Reads.Clear();
        Assert.False(reader.TryGetInt(t, "x", out _));
        Assert.False(reader.TryGetTable(t, "x", out _));
        Assert.False(reader.TryGetString(t, "x", out _));
        Assert.False(reader.TryGetTableAt(t, 1, out _));
        Assert.Empty(reader.IntKeyedTables(t));
        Assert.True(memory.Reads.Count < 20);
    }

    private static LuaTable Nested(FakeLua lua, LuaTable parent, string key, int arraySize = 0, int logSize = 2)
    {
        var child = lua.NewTable(arraySize, logSize);
        lua.SetTable(parent, key, child);
        return child;
    }

    [Fact]
    public void ResolvesNestedStringKeys()
    {
        var (_, lua, reader) = Fixture();
        var c = Nested(lua, Nested(lua, Nested(lua, lua.Globals, "a"), "b"), "c");
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryResolve(globals, "a.b.c", out var table, out var failedAt));
        Assert.Equal(c, table);
        Assert.Equal("", failedAt);
    }

    [Fact]
    public void ResolvesIntSegmentsInNodeAndArrayParts()
    {
        var (_, lua, reader) = Fixture();
        var list = Nested(lua, lua.Globals, "list", 2, 2);
        var inNode = lua.NewTable(0, 0);
        var inArray = lua.NewTable(0, 0);
        lua.SetIntKeyTable(list, 100, inNode);
        lua.SetIntKeyTable(list, 2, inArray);
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryResolve(globals, "list.100", out var node, out _));
        Assert.Equal(inNode, node);
        Assert.True(reader.TryResolve(globals, "list.2", out var array, out _));
        Assert.Equal(inArray, array);
    }

    [Fact]
    public void MetaSegmentFollowsTheMetatable()
    {
        var (_, lua, reader) = Fixture();
        var obj = Nested(lua, lua.Globals, "obj");
        var meta = lua.NewTable(0, 0);
        lua.SetMetatable(obj, meta);
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryResolve(globals, "obj.@meta", out var table, out _));
        Assert.Equal(meta, table);
        Assert.False(reader.TryResolve(globals, "a.@meta", out _, out var failedAt));
        Assert.Equal("a", failedAt);
    }

    [Fact]
    public void MissingSegmentReportsWhereItFailed()
    {
        var (_, lua, reader) = Fixture();
        Nested(lua, Nested(lua, lua.Globals, "a"), "b");
        Assert.True(reader.TryOpen(out var globals));

        Assert.False(reader.TryResolve(globals, "a.nope.c", out _, out var failedAt));
        Assert.Equal("nope", failedAt);
        Assert.False(reader.TryResolve(globals, "a.b.7", out _, out failedAt));
        Assert.Equal("7", failedAt);
    }

    [Fact]
    public void UnopenableRootFailsWithEmptyFailedAt()
    {
        var (_, _, reader) = Fixture();
        Assert.False(reader.TryResolve(default, "a", out _, out var failedAt));
        Assert.Equal("", failedAt);
    }

    [Fact]
    public void EmptyPathIsTheRoot()
    {
        var (_, lua, reader) = Fixture();
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryResolve(globals, "", out var table, out _));
        Assert.Equal(lua.Globals, table);
    }

    [Fact]
    public void EntriesListArrayThenNodesWithTypesAndValues()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 2, 3);
        var inner = lua.NewTable(0, 0);
        lua.SetIntKey(t, 1, 11, Offsets.LuaTagInt);
        lua.SetIntKey(t, 2, (long)BitConverter.DoubleToInt64Bits(1.5), Offsets.LuaTagFloat);
        lua.SetString(t, "name", "Gold");
        lua.SetInt(t, "count", 42);
        lua.SetValue(t, "flag", 7, Offsets.LuaTagBool);
        lua.SetValue(t, "off", 0, Offsets.LuaTagBool);
        lua.SetTable(t, "inner", inner);
        lua.SetValue(t, "fn", 0x1234, Offsets.LuaTagFunction);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        var entries = reader.Entries(table, 100, 0, "", out var total);

        Assert.Equal(8, total);
        Assert.Equal(new LuaEntryRow("[1]", "int", "11"), entries[0]);
        Assert.Equal(new LuaEntryRow("[2]", "float", "1.5"), entries[1]);
        var byKey = entries.Skip(2).ToDictionary(e => e.Key);
        Assert.Equal(new LuaEntryRow("name", "string", "Gold"), byKey["name"]);
        Assert.Equal(new LuaEntryRow("count", "int", "42"), byKey["count"]);
        Assert.Equal(new LuaEntryRow("flag", "bool", "true"), byKey["flag"]);
        Assert.Equal(new LuaEntryRow("off", "bool", "false"), byKey["off"]);
        Assert.Equal(new LuaEntryRow("inner", "table", $"0x{inner.Address:X}"), byKey["inner"]);
        Assert.Equal(new LuaEntryRow("fn", "function", "0x1234"), byKey["fn"]);
    }

    [Fact]
    public void EntriesTotalIsTrueCountAndArrayIsCut()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 0, 3);
        for (var i = 0; i < 5; i++) lua.SetInt(t, $"k{i}", i);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        var entries = reader.Entries(table, 3, 0, "", out var total);

        Assert.Equal(5, total);
        Assert.Equal(3, entries.Length);
    }

    [Fact]
    public void EntriesSkipEmptyNodesAndCutLongStrings()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 0, 3);
        lua.SetString(t, "long", new string('x', 10) + "\n" + new string('x', 300));
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        var entries = reader.Entries(table, 10, 0, "", out var total);

        Assert.Equal(1, total);
        Assert.Equal("long", entries[0].Key);
        Assert.Equal(Offsets.MaxLuaString, entries[0].Value.Length);
        Assert.DoesNotContain('\n',entries[0].Value);
    }

    [Fact]
    public void BoolReadsOnlyTheLowFourBytes()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 0, 2);
        lua.SetValue(t, "off", 0x1_0000_0000L, Offsets.LuaTagBool);
        lua.SetValue(t, "on", 0x1_0000_0001L, Offsets.LuaTagBool);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        var byKey = reader.Entries(table, 10, 0, "", out _).ToDictionary(e => e.Key);

        Assert.Equal("false", byKey["off"].Value);
        Assert.Equal("true", byKey["on"].Value);
    }

    [Fact]
    public void EntriesListOtherKeyTypesByTag()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 0, 2);
        lua.SetOddKey(t, 0x45, 5, Offsets.LuaTagInt);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        var entries = reader.Entries(table, 10, 0, "", out var total);

        Assert.Equal(1, total);
        Assert.Equal(new LuaEntryRow("<tag 0x45>", "int", "5"), entries[0]);
    }

    [Fact]
    public void EntriesWithUnreadableNodesAreEmpty()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 0, 2);
        lua.BreakNodes(t);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        Assert.Empty(reader.Entries(table, 10, 0, "", out var total));
        Assert.Equal(0, total);
    }

    private static (LuaReader Reader, LuaTable Table) Filterable()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 2, 3);
        lua.SetIntKey(t, 1, 11, Offsets.LuaTagInt);
        lua.SetIntKey(t, 2, 22, Offsets.LuaTagInt);
        lua.SetInt(t, "GoldCount", 1);
        lua.SetInt(t, "goldValue", 2);
        lua.SetInt(t, "silver", 3);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));
        return (reader, table);
    }

    [Fact]
    public void FilterKeepsMatchingKeysCaseInsensitivelyAndTotalIsFiltered()
    {
        var (reader, table) = Filterable();

        var entries = reader.Entries(table, 10, 0, "GOLD", out var total);

        Assert.Equal(2, total);
        Assert.Equal(["GoldCount", "goldValue"], entries.Select(e => e.Key).Order());
    }

    [Fact]
    public void FilterMatchesArrayKeyText()
    {
        var (reader, table) = Filterable();

        var entries = reader.Entries(table, 10, 0, "[2]", out var total);

        Assert.Equal(1, total);
        Assert.Equal(new LuaEntryRow("[2]", "int", "22"), Assert.Single(entries));
    }

    [Fact]
    public void SkipDropsTheFirstMatchesAndMaxCutsAfterIt()
    {
        var (reader, table) = Filterable();

        var all = reader.Entries(table, 10, 0, "", out _);
        var entries = reader.Entries(table, 2, 1, "", out var total);

        Assert.Equal(5, total);
        Assert.Equal(all.Skip(1).Take(2), entries);
    }

    [Fact]
    public void SkipPastTheEndIsEmptyWithTheTrueTotal()
    {
        var (reader, table) = Filterable();

        var entries = reader.Entries(table, 10, 99, "", out var total);

        Assert.Empty(entries);
        Assert.Equal(5, total);
    }

    [Fact]
    public void NegativeSkipActsAsZero()
    {
        var (reader, table) = Filterable();

        Assert.Equal(reader.Entries(table, 10, 0, "", out _), reader.Entries(table, 10, -3, "", out _));
    }

    [Fact]
    public void TryGetBoolReadsTrueAndFalse()
    {
        var (_, lua, reader) = Fixture();
        lua.SetBool(lua.Globals, "a", true);
        lua.SetBool(lua.Globals, "b", false);
        Assert.True(reader.TryOpen(out var globals));

        Assert.True(reader.TryGetBool(globals, "a", out var a));
        Assert.True(reader.TryGetBool(globals, "b", out var b));
        Assert.Equal((true, false), (a, b));
    }

    [Fact]
    public void TryGetBoolRejectsAnInt()
    {
        var (_, lua, reader) = Fixture();
        lua.SetInt(lua.Globals, "n", 1);
        Assert.True(reader.TryOpen(out var globals));

        Assert.False(reader.TryGetBool(globals, "n", out _));
    }

    [Fact]
    public void TryGetIntAtReadsArrayAndNodePart()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 3, 2);
        lua.SetIntKey(t, 2, 22, Offsets.LuaTagInt);
        lua.SetIntKey(t, 0, 10, Offsets.LuaTagInt);
        lua.SetIntKey(t, 40, 40, Offsets.LuaTagInt);
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        Assert.True(reader.TryGetIntAt(table, 2, out var two));
        Assert.True(reader.TryGetIntAt(table, 0, out var zero));
        Assert.True(reader.TryGetIntAt(table, 40, out var forty));
        Assert.Equal((22, 10, 40), (two, zero, forty));
    }

    [Fact]
    public void TryGetIntAtRejectsATableValue()
    {
        var (_, lua, reader) = Fixture();
        var t = Nested(lua, lua.Globals, "t", 3, 2);
        lua.SetIntKeyTable(t, 2, lua.NewTable(0, 0));
        lua.SetIntKeyTable(t, 40, lua.NewTable(0, 0));
        Assert.True(reader.TryOpen(out var globals));
        Assert.True(reader.TryGetTable(globals, "t", out var table));

        Assert.False(reader.TryGetIntAt(table, 2, out _));
        Assert.False(reader.TryGetIntAt(table, 40, out _));
    }
}
