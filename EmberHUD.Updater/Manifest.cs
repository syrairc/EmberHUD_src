using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EmberHUD.Updater;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class Manifest
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string Version = "";
    public Dictionary<string, string> Files = [];

    public static Manifest Build(string root, string version, Func<string, bool> include)
    {
        var files = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (include(relative)) files[relative] = Hash(file);
        }
        return new Manifest { Version = version, Files = files };
    }

    public static Manifest? Load(string path, Action<string> report)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), Options) is not { } manifest)
            {
                report("manifest.json is empty");
                return null;
            }
            manifest.Version ??= "";
            manifest.Files ??= [];
            return manifest;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            report($"manifest.json could not be read - {ex.Message}");
            return null;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, Options));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static bool SafeRelative(string path) =>
        path.Length > 0 && !Path.IsPathRooted(path) && !path.Contains(':') && !path.Contains('\\') && !path.Split('/').Contains("..");
}
