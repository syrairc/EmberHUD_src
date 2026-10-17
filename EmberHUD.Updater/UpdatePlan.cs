namespace EmberHUD.Updater;

public sealed class UpdatePlan
{
    public List<string> Replace = [];
    public List<string> Remove = [];
    public List<string> KeptModifiedPlugins = [];

    public static UpdatePlan Build(string installDir, Manifest? installed, Manifest incoming, bool overwriteModifiedPlugins)
    {
        foreach (var path in incoming.Files.Keys.Concat(installed?.Files.Keys ?? Enumerable.Empty<string>()))
            if (!Manifest.SafeRelative(path)) throw new InvalidDataException($"unsafe manifest path - {path}");

        var plan = new UpdatePlan();
        foreach (var (path, hash) in incoming.Files)
        {
            var disk = DiskHash(installDir, path);
            if (disk == hash) continue;
            if (disk is null || !IsPlugin(path)) plan.Replace.Add(path);
            else if (IsModified(installed, path, disk) && !overwriteModifiedPlugins) plan.KeptModifiedPlugins.Add(path);
            else plan.Replace.Add(path);
        }

        foreach (var path in installed?.Files.Keys.Where(p => !incoming.Files.ContainsKey(p)) ?? Enumerable.Empty<string>())
        {
            var disk = DiskHash(installDir, path);
            if (disk is null) continue;
            if (!IsPlugin(path) || !IsModified(installed, path, disk)) plan.Remove.Add(path);
            else plan.KeptModifiedPlugins.Add(path);
        }
        return plan;
    }

    static bool IsPlugin(string path) => path.StartsWith("Plugins/", StringComparison.Ordinal);

    static bool IsModified(Manifest? installed, string path, string diskHash) =>
        installed is null || !installed.Files.TryGetValue(path, out var shipped) || shipped != diskHash;

    static string? DiskHash(string installDir, string path)
    {
        var file = Path.Combine(installDir, path);
        return File.Exists(file) ? Manifest.Hash(file) : null;
    }
}
