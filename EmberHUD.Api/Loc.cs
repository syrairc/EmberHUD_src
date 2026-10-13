namespace EmberHUD;

public static class Loc
{
    internal static Func<string, string?>? Getter { get; set; }
    internal static Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? Finder { get; set; }

    public static string? Get(string key) => Getter?.Invoke(key);

    public static (string Key, string Text)[] Find(string needle, int max = 50) => Find(needle, max, out _);

    public static (string Key, string Text)[] Find(string needle, int max, out bool truncated)
    {
        (var matches, truncated) = Finder?.Invoke(needle, max) ?? ([], false);
        return matches;
    }
}
