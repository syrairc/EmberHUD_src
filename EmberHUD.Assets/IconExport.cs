using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Files = System.Collections.Generic.Dictionary<string, (EmberHUD.Assets.PakFile Pak, EmberHUD.Assets.PakEntry Entry)>;
using Raster = (byte[] Rgba, int Width, int Height, string Format);

namespace EmberHUD.Assets;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconEntry(string File, string Pak);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record IconIndex(Dictionary<string, long> Source, SortedDictionary<string, IconEntry> Icons, List<string> Missing, SortedDictionary<string, string> Failed);

internal sealed record Selection(Dictionary<string, List<string>> ByShort, HashSet<string> Names, string? ConfigPak = null);

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

    internal static IconIndex ExportMapIcons(IReadOnlyList<string> paks, byte[] key, string staging, string? extraIcon, Action<string> progress, CancellationToken cancel, bool recordAllPaks, Action publish) =>
        Export(paks, key, staging, progress, cancel, recordAllPaks, publish, files => MapIcons(files, extraIcon, progress));

    internal static IconIndex ExportFolders(IReadOnlyList<string> paks, byte[] key, string staging, IReadOnlyList<string> folderGlobs, Action<string> progress, CancellationToken cancel, Action publish)
    {
        var patterns = folderGlobs.Select(GlobPattern).ToArray();
        return Export(paks, key, staging, progress, cancel, false, publish, files => Folders(files, patterns));
    }

    static IconIndex Export(IReadOnlyList<string> paks, byte[] key, string staging, Action<string> progress, CancellationToken cancel, bool recordAllPaks, Action publish, Func<Files, Selection> select)
    {
        List<PakFile> opened = [];
        try
        {
            var files = OpenPaks(paks, key, opened, progress);
            var selection = select(files);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            var index = new IconIndex([], new(StringComparer.Ordinal), [], new(StringComparer.Ordinal));
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            if (selection.ConfigPak is not null) used.Add(selection.ConfigPak);
            ExportAll(files, selection, index, staging, progress, cancel, used);
            if (index.Icons.Count == 0) throw new InvalidDataException("no icons matched - game patched?");
            foreach (var path in paks.Where(path => recordAllPaks || used.Contains(path))) index.Source[Path.GetFileName(path)] = new FileInfo(path).Length;
            File.WriteAllText(Path.Combine(staging, "index.json"), JsonSerializer.Serialize(index, Options));
            publish();
            return index;
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

    static Dictionary<string, List<string>> IndexByShort(Files files, Func<string, bool> keep)
    {
        Dictionary<string, List<string>> byShort = new(StringComparer.Ordinal);
        foreach (var path in files.Keys.Where(path => path.EndsWith(".uasset", StringComparison.Ordinal) && keep(path)))
        {
            var name = ShortName(path);
            if (!byShort.TryGetValue(name, out var list)) byShort[name] = list = [];
            list.Add(path);
        }
        return byShort;
    }

    static Selection MapIcons(Files files, string? extraIcon, Action<string> progress)
    {
        const string ConfigPath = "UE_game/Content/Script/Data/ConfigView/config_map_icon_ChunkA0.bin";
        var configBytes = Read(files, ConfigPath) ?? throw new InvalidDataException("config_map_icon_ChunkA0.bin not found in the paks - game patched?");
        List<MapIconRow> config;
        try
        {
            config = UAsset.MapIconConfig(configBytes);
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            progress($"config_map_icon_ChunkA0.bin layout changed, skipping ({ex.GetType().Name}) - icons under MiniMap/NoAtlasMap/AtlasMap still export");
            config = [];
        }
        var byShort = IndexByShort(files, _ => true);
        HashSet<string> wanted = new(config.Select(row => row.Texture).Where(texture => texture.Length > 0), StringComparer.Ordinal);
        if (extraIcon is not null) wanted.Add(extraIcon);
        foreach (var (name, paths) in byShort)
            if (paths.Any(path => IconFolder().IsMatch(path))) wanted.Add(name);
        return new Selection(byShort, wanted, files[ConfigPath].Pak.Path);
    }

    static Selection Folders(Files files, Regex[] patterns)
    {
        var byShort = IndexByShort(files, path => patterns.Any(pattern => pattern.IsMatch(path)));
        return new Selection(byShort, [.. byShort.Keys]);
    }

    static Regex GlobPattern(string glob) => new(Regex.Escape(glob).Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static void ExportAll(Files files, Selection selection, IconIndex index, string staging, Action<string> progress, CancellationToken cancel, HashSet<string> used)
    {
        Dictionary<string, Raster> atlases = [];
        var done = 0;
        foreach (var name in selection.Names.Order(StringComparer.Ordinal))
        {
            cancel.ThrowIfCancellationRequested();
            if (++done % 25 == 0) progress($"{done} / {selection.Names.Count} icons");
            if (!selection.ByShort.TryGetValue(name, out var found))
            {
                index.Missing.Add(name);
                continue;
            }
            string[] candidates = [.. found.OrderBy(path => !IconFolder().IsMatch(path)).ThenBy(path => path.EndsWith("_Sprite.uasset", StringComparison.Ordinal)).ThenBy(path => path, StringComparer.Ordinal)];
            try
            {
                if (ExportOne(name, candidates, files, atlases, staging, used) is { } entry) index.Icons[name] = entry;
            }
            catch (Exception ex)
            {
                index.Failed[name] = $"{ex.GetType().Name}: {ex.Message} ({candidates[0]})";
            }
        }
    }

    static void MarkUsed(Files files, HashSet<string> used, string assetBase)
    {
        foreach (var extension in (string[])[".uasset", ".uexp", ".ubulk"])
            if (files.TryGetValue(assetBase + extension, out var hit)) used.Add(hit.Pak.Path);
    }

    static IconEntry? ExportOne(string name, string[] candidates, Files files, Dictionary<string, Raster> atlases, string staging, HashSet<string> used)
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
            var atlasBase = "UE_game/Content/" + atlas[6..];
            if (!atlases.TryGetValue(atlas, out var sheet))
                atlases[atlas] = sheet = UAsset.Texture(Read(files, atlasBase + ".uexp") ?? throw new InvalidDataException($"atlas {atlas} not found"), Read(files, atlasBase + ".ubulk"));
            var (x, y) = UAsset.Vector2D(uexp, names, "BakedSourceUV");
            var (w, h) = UAsset.Vector2D(uexp, names, "BakedSourceDimension");
            int[] rect = [(int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h)];
            image = (UAsset.Crop(sheet.Rgba, sheet.Width, sheet.Height, rect[0], rect[1], rect[2], rect[3]), rect[2], rect[3], sheet.Format);
            MarkUsed(files, used, atlasBase);
        }
        else if (names.Contains("Texture2D") && names.Contains("Default__Texture2D")) image = UAsset.Texture(uexp, Read(files, assetBase + ".ubulk"));
        else return null;
        UAsset.WritePng(Path.Combine(staging, name + ".png"), image.Rgba, image.Width, image.Height);
        MarkUsed(files, used, assetBase);
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
}
