using System.Text.Json;
using Microsoft.Win32;

namespace EmberHUD.Launcher;

public sealed record GameInstall(string Folder, int? Build)
{
    public string Binaries => Path.Combine(Folder, "Game", "UE_game", "Binaries", "Win64");

    public const string ExeName = "torchlight_infinite.exe";

    public const string DefaultFolder = @"C:\Program Files (x86)\Torchlight Infinite";

    public string Exe => Path.Combine(Binaries, ExeName);

    string Saved => Path.Combine(Folder, "Game", "UE_game", "Torchlight", "Saved", "PersistentDownloadDir");

    public static GameInstall? At(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        var install = new GameInstall(Path.GetFullPath(folder), null);
        return File.Exists(install.Exe) ? install with { Build = install.ReadBuild() } : null;
    }

    public static GameInstall? Find(string configured, string? registered) =>
        configured.Length > 0 ? At(configured) : At(registered ?? "") ?? At(DefaultFolder);

    public static string? Registered()
    {
        foreach (var view in (RegistryView[])[RegistryView.Registry64, RegistryView.Registry32])
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Torchlight Infinite");
            if (key?.GetValue("UninstallString") is string uninstall && FolderOf(uninstall) is { } folder) return folder;
        }
        return null;
    }

    public static string? FolderOf(string uninstall)
    {
        var text = uninstall.Trim();
        var exe = text.StartsWith('"') ? text[1..(text.IndexOf('"', 1) is var end and > 0 ? end : text.Length)] : text;
        return exe.Length > 0 ? Path.GetDirectoryName(exe) : null;
    }

    public IReadOnlyList<string> Paks() =>
    [
        .. LargestLastSoItWins(Files(Path.Combine(Folder, "Game", "UE_game", "Content", "Paks"), "*.pak")),
        .. Files(Path.Combine(Saved, "PreMountPak"), "UE_game_*_P.pak").OrderBy(PatchVersion).ThenBy(pak => pak, StringComparer.OrdinalIgnoreCase),
    ];

    static IEnumerable<string> LargestLastSoItWins(IEnumerable<string> paks) =>
        paks.OrderBy(pak => new FileInfo(pak).Length).ThenBy(pak => pak, StringComparer.OrdinalIgnoreCase);

    static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern).Where(file => file.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) : [];

    static long PatchVersion(string pak) => long.TryParse(Path.GetFileNameWithoutExtension(pak).Split('_') is [_, _, var version, _] ? version : "", out var number) ? number : -1;

    int? ReadBuild()
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Saved, "ReleaseGlobal", "Config", "live_patch_version.json")));
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("CurrentBuild", out var build) && build.ValueKind == JsonValueKind.Number && build.TryGetInt32(out var value) ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
