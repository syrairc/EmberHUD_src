using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmberHUD.Host;
using EmberHUD.Setup.Pak;
using Files = System.Collections.Generic.Dictionary<string, (EmberHUD.Setup.Pak.PakFile Pak, EmberHUD.Setup.Pak.PakEntry Entry)>;
using Raster = (byte[] Rgba, int Width, int Height, string Format);

namespace EmberHUD.Setup.Icons;

internal sealed record ExportResult(int Icons, int Missing, int Failed);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconEntry(string File, string Pak);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconIndex(Dictionary<string, long> Source, SortedDictionary<string, IconEntry> Icons, List<string> Missing, SortedDictionary<string, string> Failed);

internal static partial class IconExport
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [GeneratedRegex("/MiniMap/|/NoAtlasMap/|/AtlasMap/", RegexOptions.IgnoreCase)]
    private static partial Regex IconFolder();

    public static ExportResult Run(IReadOnlyList<string> paks, string target, byte[] key, Action<string> progress)
    {
        var staging = target + ".new";
        List<PakFile> opened = [];
        try
        {
            var files = OpenPaks(paks, key, opened, progress);
            var config = UAsset.MapIconConfig(Read(files, "UE_game/Content/Script/Data/ConfigView/config_map_icon.bin") ?? throw new InvalidDataException("config_map_icon.bin not found in the paks - game patched?"));
            var (byShort, wanted) = FindWanted(files, config);

            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            var index = new IconIndex(paks.ToDictionary(path => Path.GetFileName(path), path => new FileInfo(path).Length), new(StringComparer.Ordinal), [], new(StringComparer.Ordinal));
            ExportAll(files, byShort, wanted, index, staging, progress);
            File.WriteAllText(Path.Combine(staging, "index.json"), JsonSerializer.Serialize(index, Options));
            Swap(staging, target);
            progress("done");
            return new ExportResult(index.Icons.Count, index.Missing.Count, index.Failed.Count);
        }
        finally
        {
            foreach (var pak in opened) pak.Dispose();
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    static Files OpenPaks(IReadOnlyList<string> paks, byte[] key, List<PakFile> opened, Action<string> progress)
    {
        Files files = new(StringComparer.Ordinal);
        foreach (var path in paks)
        {
            progress($"reading {Path.GetFileName(path)}");
            var pak = PakFile.Open(path, key);
            opened.Add(pak);
            foreach (var entry in pak.Entries) files[entry.Path] = (pak, entry);
        }
        return files;
    }

    static byte[]? Read(Files files, string path) => files.TryGetValue(path, out var hit) ? hit.Pak.Read(hit.Entry) : null;

    static (Dictionary<string, List<string>> ByShort, HashSet<string> Wanted) FindWanted(Files files, IReadOnlyList<MapIconRow> config)
    {
        Dictionary<string, List<string>> byShort = new(StringComparer.Ordinal);
        HashSet<string> wanted = new(config.Select(row => row.Texture).Where(texture => texture.Length > 0), StringComparer.Ordinal);
        wanted.Add(Offsets.TradePriceTexture);
        foreach (var path in files.Keys.Where(path => path.EndsWith(".uasset", StringComparison.Ordinal)))
        {
            var name = ShortName(path);
            if (!byShort.TryGetValue(name, out var list)) byShort[name] = list = [];
            list.Add(path);
            if (IconFolder().IsMatch(path)) wanted.Add(name);
        }
        return (byShort, wanted);
    }

    static void ExportAll(Files files, Dictionary<string, List<string>> byShort, HashSet<string> wanted, IconIndex index, string staging, Action<string> progress)
    {
        Dictionary<string, Raster> atlases = [];
        var done = 0;
        foreach (var name in wanted.Order(StringComparer.Ordinal))
        {
            if (++done % 25 == 0) progress($"{done} / {wanted.Count} icons");
            if (!byShort.TryGetValue(name, out var found))
            {
                index.Missing.Add(name);
                continue;
            }
            string[] candidates = [.. found.OrderBy(path => !IconFolder().IsMatch(path)).ThenBy(path => path.EndsWith("_Sprite.uasset", StringComparison.Ordinal)).ThenBy(path => path, StringComparer.Ordinal)];
            try
            {
                if (ExportOne(name, candidates, files, atlases, staging) is { } entry) index.Icons[name] = entry;
            }
            catch (Exception ex)
            {
                index.Failed[name] = $"{ex.GetType().Name}: {ex.Message} ({candidates[0]})";
            }
        }
    }

    static IconEntry? ExportOne(string name, string[] candidates, Files files, Dictionary<string, Raster> atlases, string staging)
    {
        var path = candidates[0];
        var assetBase = path[..^".uasset".Length];
        var names = UAsset.Names(Read(files, path)!);
        var uexp = Read(files, assetBase + ".uexp") ?? throw new InvalidDataException("no .uexp");
        Raster image;
        if (names.Contains("PaperSprite") && names.Contains("Default__PaperSprite"))
        {
            var own = "/Game/" + assetBase[(assetBase.IndexOf("/Content/", StringComparison.Ordinal) + "/Content/".Length)..];
            var atlas = names.First(n => n.StartsWith("/Game/", StringComparison.Ordinal) && n != own);
            if (!atlases.TryGetValue(atlas, out var sheet))
            {
                var atlasBase = "UE_game/Content/" + atlas[6..];
                atlases[atlas] = sheet = UAsset.Texture(Read(files, atlasBase + ".uexp") ?? throw new InvalidDataException($"atlas {atlas} not found"), Read(files, atlasBase + ".ubulk"));
            }
            var (x, y) = UAsset.Vector2D(uexp, names, "BakedSourceUV");
            var (w, h) = UAsset.Vector2D(uexp, names, "BakedSourceDimension");
            int[] rect = [(int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h)];
            image = (UAsset.Crop(sheet.Rgba, sheet.Width, sheet.Height, rect[0], rect[1], rect[2], rect[3]), rect[2], rect[3], sheet.Format);
        }
        else if (names.Contains("Texture2D") && names.Contains("Default__Texture2D")) image = UAsset.Texture(uexp, Read(files, assetBase + ".ubulk"));
        else return null;
        UAsset.WritePng(Path.Combine(staging, name + ".png"), image.Rgba, image.Width, image.Height);
        return new IconEntry(name + ".png", Path.GetFileName(files[path].Pak.Path));
    }

    static string ShortName(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..^".uasset".Length];
        return name.EndsWith("_Sprite", StringComparison.Ordinal) ? name[..^"_Sprite".Length] : name;
    }

    public static void Swap(string staging, string target)
    {
        var old = target + ".old";
        if (Directory.Exists(old)) Directory.Delete(old, true);
        if (Directory.Exists(target)) Directory.Move(target, old);
        try
        {
            Directory.Move(staging, target);
        }
        catch
        {
            if (Directory.Exists(old)) Directory.Move(old, target);
            throw;
        }
        try
        {
            if (Directory.Exists(old)) Directory.Delete(old, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static string Describe(string folder, IReadOnlyList<string> paks, int? build)
    {
        var indexPath = Path.Combine(folder, "index.json");
        if (!File.Exists(indexPath)) return "none";
        try
        {
            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            var icons = index.RootElement.GetProperty("icons").EnumerateObject().Count();
            var source = index.RootElement.GetProperty("source").EnumerateObject().ToDictionary(pak => pak.Name, pak => pak.Value.GetInt64(), StringComparer.OrdinalIgnoreCase);
            var current = source.Count == paks.Count && paks.All(pak => source.TryGetValue(Path.GetFileName(pak), out var size) && size == new FileInfo(pak).Length);
            return current ? $"{icons} icons, game build {build?.ToString() ?? "unknown"}" : $"{icons} icons - out of date, extract again";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException)
        {
            return "unreadable - extract again";
        }
    }
}
