namespace EmberHUD.Host.Reading;

internal static class InvulnerableMonsters
{
    public static readonly HashSet<uint> Ids = Load();

    static HashSet<uint> Load()
    {
        using var reader = new StreamReader(typeof(InvulnerableMonsters).Assembly.GetManifestResourceStream("invulnerable")!);
        return [.. reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(uint.Parse)];
    }
}
