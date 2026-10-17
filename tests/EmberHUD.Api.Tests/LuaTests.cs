using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class LuaTests : IDisposable
{
    public void Dispose() => TestGame.UseLua(null);

    [Fact]
    public void NoSourceDumpsNothing() => Assert.Null(Lua.Dump("", 10));

    [Fact]
    public void DumpPassesThroughToTheSource()
    {
        var dump = new LuaDump(true, "", [new LuaEntry("name", "string", "Gold")], 1);
        TestGame.UseLua((path, max) => path == "a.b" && max == 5 ? dump : null);

        Assert.Same(dump, Lua.Dump("a.b", 5));
    }
}
