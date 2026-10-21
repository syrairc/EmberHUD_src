using System.Text.Json;

namespace EmberHUD.Assets;

internal enum AssetType { Minimap, Items, Skills, Buffs }

internal enum AssetState { NotExtracted, Current, OutOfDate, Unreadable }

internal sealed record AssetStatus(AssetState State, int Files, long Bytes);

internal sealed record AssetResult(int Files, int Missing, int Failed);

internal interface IAssetJob
{
    AssetType Type { get; }
    string Title { get; }
    string Folder { get; }
    AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks);
    AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel);
}

internal static class AssetJobs
{
    public static IReadOnlyList<IAssetJob> All { get; } = Create(null);

    public static IReadOnlyList<IAssetJob> Create(string? marketIcon) =>
    [
        new MinimapJob(marketIcon),
        new FolderJob(AssetType.Items, "Items", "Items",
        [
            "/Icon/Item/",
            "/EquipCommon/*/128/",
            "/Icon/Ash/128/",
            "/Icon/Ticket/128/",
            "/Icon/Coin/128/",
            "/Season/**/Icon/GamePlay/",
            "/Season/**/ActivityBg/",
        ]),
        new FolderJob(AssetType.Skills, "Skills", "Skills", ["/Icon/Skill/CommonSkill/128/"]),
        new FolderJob(AssetType.Buffs, "Buffs", "Buffs", ["/Icon/Skill/Buff/32/"]),
    ];

    internal static AssetStatus ReadStatus(string folder, IReadOnlyList<string> paks)
    {
        var indexPath = Path.Combine(folder, "index.json");
        if (!File.Exists(indexPath)) return new AssetStatus(AssetState.NotExtracted, 0, 0);
        try
        {
            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            var icons = index.RootElement.GetProperty("icons").EnumerateObject().ToList();
            var bytes = icons.Sum(icon => FileLength(Path.Combine(folder, Path.GetFileName(icon.Value.GetProperty("file").GetString()!))));
            var installed = paks.ToDictionary(path => Path.GetFileName(path), path => path, StringComparer.OrdinalIgnoreCase);
            var source = index.RootElement.GetProperty("source").EnumerateObject().ToList();
            var current = source.Count > 0 && source.All(pak => installed.TryGetValue(pak.Name, out var path) && File.Exists(path) && new FileInfo(path).Length == pak.Value.GetInt64());
            return new AssetStatus(current ? AssetState.Current : AssetState.OutOfDate, icons.Count, bytes);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            return new AssetStatus(AssetState.Unreadable, 0, 0);
        }
    }

    internal static string Staging(string target)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(target));
        if (parent is not null && Directory.Exists(parent))
            foreach (var stale in Directory.GetDirectories(parent, Path.GetFileName(target) + ".new*").Where(path => Directory.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-1)))
            {
                try
                {
                    Directory.Delete(stale, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        return $"{target}.new-{Environment.ProcessId}";
    }

    static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
}
