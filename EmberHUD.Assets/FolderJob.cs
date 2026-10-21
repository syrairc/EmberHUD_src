namespace EmberHUD.Assets;

internal sealed class FolderJob(AssetType type, string title, string folder, string[] textureFolderGlobs) : IAssetJob
{
    public AssetType Type { get; } = type;
    public string Title { get; } = title;
    public string Folder { get; } = folder;

    public AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks) => AssetJobs.ReadStatus(Path.Combine(iconsRoot, Folder), paks);

    public AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel)
    {
        var target = Path.Combine(iconsRoot, Folder);
        var staging = AssetJobs.Staging(target);
        var index = IconExport.ExportFolders(paks, key, staging, textureFolderGlobs, progress, cancel, () => IconExport.Swap(staging, target));
        progress("done");
        return new AssetResult(index.Icons.Count, index.Missing.Count, index.Failed.Count);
    }
}
