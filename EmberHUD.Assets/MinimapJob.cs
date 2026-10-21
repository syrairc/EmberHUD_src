using System.Text.Json;

namespace EmberHUD.Assets;

internal sealed class MinimapJob(string? extraIcon) : IAssetJob
{
    public AssetType Type => AssetType.Minimap;
    public string Title => "Minimap";
    public string Folder => "";

    public AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks) => AssetJobs.ReadStatus(iconsRoot, paks);

    public AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel)
    {
        var staging = AssetJobs.Staging(iconsRoot);
        var index = IconExport.ExportMapIcons(paks, key, staging, extraIcon, progress, cancel, false, () => Merge(staging, iconsRoot));
        progress("done");
        return new AssetResult(index.Icons.Count, index.Missing.Count, index.Failed.Count);
    }

    static void Merge(string staging, string root)
    {
        Directory.CreateDirectory(root);
        var incoming = Directory.GetFiles(staging).Select(file => Path.GetFileName(file)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var old in OldFiles(root).Where(name => !incoming.Contains(name))) File.Delete(Path.Combine(root, old));
        foreach (var file in Directory.GetFiles(staging).OrderBy(file => Path.GetFileName(file) == "index.json"))
            File.Move(file, Path.Combine(root, Path.GetFileName(file)), true);
    }

    static string[] OldFiles(string root)
    {
        var indexPath = Path.Combine(root, "index.json");
        if (!File.Exists(indexPath)) return [];
        try
        {
            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            return [.. index.RootElement.GetProperty("icons").EnumerateObject().Select(icon => Path.GetFileName(icon.Value.GetProperty("file").GetString())).OfType<string>()];
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException or ArgumentException)
        {
            return [];
        }
    }
}
