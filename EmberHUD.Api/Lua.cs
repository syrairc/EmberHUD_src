using System;

namespace EmberHUD;

public sealed record LuaEntry(string Key, string Type, string Value);

public sealed record LuaDump(bool Resolved, string FailedAt, LuaEntry[] Entries, int Total);

public static class Lua
{
    internal static Func<string, int, int, string, LuaDump?>? Dumper { get; set; }

    public static LuaDump? Dump(string path, int max, int skip = 0, string filter = "") => Dumper?.Invoke(path, max, skip, filter);
}
