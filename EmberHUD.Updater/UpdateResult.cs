using System.Reflection;
using System.Text.Json;

namespace EmberHUD.Updater;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class UpdateResult
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };

    public string Outcome = "";
    public string Version = "";
    public string Reason = "";
    public List<string> KeptPlugins = [];

    static string ResultPath(string updatesDir) => Path.Combine(updatesDir, "result.json");

    public static UpdateResult? Load(string updatesDir)
    {
        try
        {
            return JsonSerializer.Deserialize<UpdateResult>(File.ReadAllText(ResultPath(updatesDir)), Options) is { Outcome: not null, Version: not null, Reason: not null, KeptPlugins: not null } result ? result : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(string updatesDir)
    {
        Directory.CreateDirectory(updatesDir);
        var path = ResultPath(updatesDir);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, Options));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public static void Delete(string updatesDir) => File.Delete(ResultPath(updatesDir));
}
