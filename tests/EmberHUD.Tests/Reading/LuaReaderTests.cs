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
}
