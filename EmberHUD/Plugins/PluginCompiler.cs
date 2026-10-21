using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace EmberHUD.Host.Plugins;

internal sealed record SourceFile(string Path, byte[] Bytes);

internal sealed record CompileResult(byte[]? Assembly, IReadOnlyList<string> Diagnostics, bool FromCache = false);

internal static class PluginCompiler
{
    static readonly SourceLog Log = EmberHUD.Log.For("Loader");

    static readonly Lazy<MetadataReference[]> References = new(() =>
    {
        var framework = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var hexa = typeof(ImGui).Assembly.Location;
        return
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(path => string.Equals(Path.GetDirectoryName(path), framework, StringComparison.OrdinalIgnoreCase))
                .Append(typeof(Plugin).Assembly.Location)
                .Append(hexa)
                .Append(Path.Combine(Path.GetDirectoryName(hexa)!, "HexaGen.Runtime.dll"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => MetadataReference.CreateFromFile(path)),
        ];
    });

    static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    static readonly CSharpCompilationOptions CompilationOptions = new(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable, deterministic: true);

    static readonly EmitOptions EmitOptions = new(debugInformationFormat: DebugInformationFormat.Embedded);

    public static string OptionsSignature => string.Join(';', ParseOptions.LanguageVersion, CompilationOptions.OutputKind, CompilationOptions.OptimizationLevel, CompilationOptions.AllowUnsafe, CompilationOptions.NullableContextOptions, CompilationOptions.Deterministic, EmitOptions.DebugInformationFormat);

    public static CompileResult Compile(string name, IReadOnlyList<SourceFile> sources)
    {
        var texts = sources.Select(source => (source.Path, Text: SourceText.From(source.Bytes, source.Bytes.Length, Encoding.UTF8, canBeEmbedded: true))).ToList();
        var compilation = CSharpCompilation.Create(
            name,
            texts.Select(text => CSharpSyntaxTree.ParseText(text.Text, ParseOptions, text.Path)),
            References.Value,
            CompilationOptions);
        using var assembly = new MemoryStream();
        var emitted = compilation.Emit(assembly, options: EmitOptions, embeddedTexts: texts.Select(text => EmbeddedText.FromSource(text.Path, text.Text)));
        if (emitted.Success) return new CompileResult(assembly.ToArray(), []);
        return new CompileResult(null, [.. emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic =>
        {
            var span = diagnostic.Location.GetLineSpan();
            return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): {diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
        })]);
    }

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
        hash.AppendData(Encoding.UTF8.GetBytes($"{typeof(ImGui).Assembly.GetName().Version};{OptionsSignature}"));
        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    public static CompileResult Build(string name, string directory, string cacheDirectory)
    {
        var sources = Read(directory, tests: false);
        var path = Path.Combine(cacheDirectory, $"{name}-{Key(sources)}.dll");
        if (File.Exists(path)) return new CompileResult(File.ReadAllBytes(path), [], FromCache: true);
        var result = Compile(name, sources);
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

    public static void ClearCache(string cacheDirectory)
    {
        var failed = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(cacheDirectory, "*.dll"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                    Log.Warn($"could not delete {file} - {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed++;
            Log.Warn($"could not read {cacheDirectory} - {ex.Message}");
        }
        if (failed == 0) Log.Info("compile cache cleared");
    }

    public static Type FindPluginType(IEnumerable<Type> types)
    {
        var found = types.Where(type => type.IsSubclassOf(typeof(Plugin)) && !type.IsAbstract).ToList();
        return found.Count == 1 ? found[0] : throw new InvalidOperationException($"found {found.Count} Plugin subclasses, expected one");
    }

    public static string DefaultsPath(string directory) => Path.Combine(directory, "defaults.json");

    public static string? ReadDefaults(string directory) => ReadText(DefaultsPath(directory));

    public static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    public static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    static bool IsOwnCacheFile(string file, string name) =>
        Regex.IsMatch(file, $@"^{Regex.Escape(name)}-[0-9A-F]{{16}}\.dll(\.tmp)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
