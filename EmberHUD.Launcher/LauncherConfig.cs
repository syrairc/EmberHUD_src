using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EmberHUD.Launcher;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class LauncherProgram
{
    public string Path = "";
    public string Arguments = "";
    public bool Enabled = true;
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class LauncherConfig
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string RunAsUser = "";
    public string GameDirectory = "";
    public bool LaunchGame = true;
    public bool LaunchEmberHUD = true;
    public List<LauncherProgram> Programs = [];

    public static LauncherConfig FirstRun(IEnumerable<string> localUsers) =>
        new() { RunAsUser = localUsers.FirstOrDefault(user => user.Equals("POE", StringComparison.OrdinalIgnoreCase)) ?? "" };

    public static LauncherConfig? Load(string path, Action<string> report)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(path), Options) is not { } config)
            {
                report("launcher.json is empty");
                return null;
            }
            config.RunAsUser ??= "";
            config.GameDirectory ??= "";
            config.Programs = [.. (config.Programs ?? []).OfType<LauncherProgram>()];
            foreach (var program in config.Programs)
            {
                program.Path ??= "";
                program.Arguments ??= "";
            }
            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            report($"launcher.json could not be read - {ex.Message}");
            return null;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, Options));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
