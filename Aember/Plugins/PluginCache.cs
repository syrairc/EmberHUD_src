using System.Security.Cryptography;
using System.Text;
using Hexa.NET.ImGui;

namespace Aember.Host.Plugins;

internal sealed record SourceFile(string Path, byte[] Bytes);

internal sealed record CompileResult(byte[]? Assembly, IReadOnlyList<string> Diagnostics, bool FromCache = false);

internal static class PluginCache
{
    public static IEnumerable<string> Plugins(string pluginsDirectory) =>
        Directory.Exists(pluginsDirectory)
            ? Directory.EnumerateDirectories(pluginsDirectory).Where(directory => !Skipped(Path.GetFileName(directory)) && Files(directory, tests: false).Any()).Order(StringComparer.OrdinalIgnoreCase)
            : [];

    public static IReadOnlyList<SourceFile> Read(string directory, bool tests) =>
        [.. Files(directory, tests).Select(relative => new SourceFile(relative, File.ReadAllBytes(Path.Combine(directory, relative))))];

    public static string Key(IReadOnlyList<SourceFile> sources)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var source in sources)
        {
            var path = Encoding.UTF8.GetBytes(source.Path);
            hash.AppendData(BitConverter.GetBytes(path.Length));
            hash.AppendData(path);
            hash.AppendData(BitConverter.GetBytes(source.Bytes.Length));
            hash.AppendData(source.Bytes);
        }
        hash.AppendData(typeof(Plugin).Assembly.ManifestModule.ModuleVersionId.ToByteArray());
        hash.AppendData(Encoding.UTF8.GetBytes($"{typeof(ImGui).Assembly.GetName().Version};latest;nullable;unsafe;release;embedded"));
        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    public static CompileResult Build(string name, string directory, string cacheDirectory)
    {
        var sources = Read(directory, tests: false);
        var path = Path.Combine(cacheDirectory, $"{name}-{Key(sources)}.dll");
        if (File.Exists(path)) return new CompileResult(File.ReadAllBytes(path), [], FromCache: true);
        var result = PluginCompiler.Compile(name, sources);
        if (result.Assembly is null) return result;
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(path + ".tmp", result.Assembly);
            File.Move(path + ".tmp", path, overwrite: true);
            foreach (var stale in Directory.EnumerateFiles(cacheDirectory, $"{name}-*").Where(file => file != path && IsOwnCacheFile(Path.GetFileName(file), name))) File.Delete(stale);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"{name} - could not update the compile cache - {ex.Message}");
        }
        return result;
    }

    static bool IsOwnCacheFile(string file, string name) =>
        file.Length == name.Length + 21 && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.Length == name.Length + 25 && file.EndsWith(".dll.tmp", StringComparison.OrdinalIgnoreCase);

    static IEnumerable<string> Files(string directory, bool tests) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(directory, file))
            .Where(relative =>
            {
                var parts = relative.Split(Path.DirectorySeparatorChar);
                return !parts[..^1].Any(Skipped) && (tests || parts.Length == 1 || !parts[0].Equals("Tests", StringComparison.OrdinalIgnoreCase));
            })
            .Order(StringComparer.Ordinal);

    static bool Skipped(string folder) => folder.StartsWith('.') || folder.Equals("bin", StringComparison.OrdinalIgnoreCase) || folder.Equals("obj", StringComparison.OrdinalIgnoreCase);
}
