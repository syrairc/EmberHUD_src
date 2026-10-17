using System.Reflection;
using System.Text.Json;

namespace EmberHUD.Updater;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class StagedUpdate
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };

    public string Version = "";
    public string FilesDirectory = "";
    public string ManifestPath = "";

    static string StatePath(string updatesDir) => Path.Combine(updatesDir, "state.json");

    public static StagedUpdate? Load(string updatesDir)
    {
        try
        {
            if (JsonSerializer.Deserialize<StagedUpdate>(File.ReadAllText(StatePath(updatesDir)), Options) is not { } staged) return null;
            return !string.IsNullOrEmpty(staged.Version) && !string.IsNullOrEmpty(staged.FilesDirectory) && !string.IsNullOrEmpty(staged.ManifestPath) && Directory.Exists(staged.FilesDirectory) && File.Exists(staged.ManifestPath) ? staged : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(string updatesDir)
    {
        Directory.CreateDirectory(updatesDir);
        var path = StatePath(updatesDir);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, Options));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public static void Clear(string updatesDir) => File.Delete(StatePath(updatesDir));
}
