using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EmberHUD.Host;
using EmberHUD.Setup.Pak;

namespace EmberHUD.Setup.Icons;

internal sealed record ExportResult(int Icons, int Missing, int Failed);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconEntry(string Asset, string Pak, string Kind, string? Atlas, int[]? Rect, string File, string Format, int[] Size, string[]? Alternatives);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconIndex(Dictionary<string, long> Source, SortedDictionary<string, IconEntry> Icons, List<string> Missing, SortedDictionary<string, string> Failed, List<MapIconRow> MapIconConfig);

internal static partial class IconExport
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
            Dictionary<string, (PakFile Pak, PakEntry Entry)> files = new(StringComparer.Ordinal);
            foreach (var path in paks)
            {
                progress($"reading {Path.GetFileName(path)}");
                var pak = PakFile.Open(path, key);
                opened.Add(pak);
                foreach (var entry in pak.Entries) files[entry.Path] = (pak, entry);
            }
            byte[]? Read(string path) => files.TryGetValue(path, out var hit) ? hit.Pak.Read(hit.Entry) : null;

            var config = UAsset.MapIconConfig(Read("UE_game/Content/Script/Data/ConfigView/config_map_icon.bin") ?? throw new InvalidDataException("config_map_icon.bin not found in the paks - game patched?"));
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

            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            var index = new IconIndex(paks.ToDictionary(path => Path.GetFileName(path), path => new FileInfo(path).Length), new(StringComparer.Ordinal), [], new(StringComparer.Ordinal), config);
            Dictionary<string, (byte[] Rgba, int Width, int Height, string Format)> atlases = [];
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
                var path = candidates[0];
                var assetBase = path[..^".uasset".Length];
                try
                {
                    var names = UAsset.Names(Read(path)!);
                    var uexp = Read(assetBase + ".uexp") ?? throw new InvalidDataException("no .uexp");
                    var asset = AssetPath(assetBase);
                    string kind;
                    string? atlas = null;
                    int[]? rect = null;
                    (byte[] Rgba, int Width, int Height, string Format) image;
                    if (names.Contains("PaperSprite") && names.Contains("Default__PaperSprite"))
                    {
                        atlas = names.First(n => n.StartsWith("/Game/", StringComparison.Ordinal) && n != asset);
                        if (!atlases.TryGetValue(atlas, out var sheet))
                        {
                            var atlasBase = "UE_game/Content/" + atlas[6..];
                            atlases[atlas] = sheet = UAsset.Texture(Read(atlasBase + ".uexp") ?? throw new InvalidDataException($"atlas {atlas} not found"), Read(atlasBase + ".ubulk"));
                        }
                        var (x, y) = UAsset.Vector2D(uexp, names, "BakedSourceUV");
                        var (w, h) = UAsset.Vector2D(uexp, names, "BakedSourceDimension");
                        rect = [(int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h)];
                        image = (UAsset.Crop(sheet.Rgba, sheet.Width, sheet.Height, rect[0], rect[1], rect[2], rect[3]), rect[2], rect[3], sheet.Format);
                        kind = "sprite";
                    }
                    else if (names.Contains("Texture2D") && names.Contains("Default__Texture2D"))
                    {
                        image = UAsset.Texture(uexp, Read(assetBase + ".ubulk"));
                        kind = "texture";
                    }
                    else continue;
                    UAsset.WritePng(Path.Combine(staging, name + ".png"), image.Rgba, image.Width, image.Height);
                    index.Icons[name] = new IconEntry(asset, Path.GetFileName(files[path].Pak.Path), kind, atlas, rect, name + ".png", image.Format, [image.Width, image.Height], candidates.Length > 1 ? [.. candidates[1..].Select(c => AssetPath(c[..^".uasset".Length]))] : null);
                }
                catch (Exception ex)
                {
                    index.Failed[name] = $"{ex.GetType().Name}: {ex.Message} ({path})";
                }
            }
            File.WriteAllText(Path.Combine(staging, "index.json"), JsonSerializer.Serialize(index, Options));
            foreach (var pak in opened) pak.Dispose();
            opened.Clear();
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

    static string ShortName(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..^".uasset".Length];
        return name.EndsWith("_Sprite", StringComparison.Ordinal) ? name[..^"_Sprite".Length] : name;
    }

    static string AssetPath(string assetBase) => "/Game/" + assetBase[(assetBase.IndexOf("/Content/", StringComparison.Ordinal) + "/Content/".Length)..];

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
