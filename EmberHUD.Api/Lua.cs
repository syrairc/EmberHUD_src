using System;

namespace EmberHUD;

public sealed record LuaEntry(string Key, string Type, string Value);

public sealed record LuaDump(bool Resolved, string FailedAt, LuaEntry[] Entries, int Total);

public static class Lua
{
    internal static Func<string, int, LuaDump?>? Dumper { get; set; }

    public static LuaDump? Dump(string path, int max) => Dumper?.Invoke(path, max);
}
