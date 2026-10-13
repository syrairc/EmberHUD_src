using System.Globalization;
using System.Text;
using Hexa.NET.ImGui;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace EmberHUD.Host.Plugins;

internal static class PluginCompiler
{
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

    public static CompileResult Compile(string name, IReadOnlyList<SourceFile> sources)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var texts = sources.Select(source => (source.Path, Text: SourceText.From(source.Bytes, source.Bytes.Length, Encoding.UTF8, canBeEmbedded: true))).ToList();
        var compilation = CSharpCompilation.Create(
            name,
            texts.Select(text => CSharpSyntaxTree.ParseText(text.Text, parse, text.Path)),
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable, deterministic: true));
        using var assembly = new MemoryStream();
        var emitted = compilation.Emit(assembly, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded), embeddedTexts: texts.Select(text => EmbeddedText.FromSource(text.Path, text.Text)));
        if (emitted.Success) return new CompileResult(assembly.ToArray(), []);
        return new CompileResult(null, [.. emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic =>
        {
            var span = diagnostic.Location.GetLineSpan();
            return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): {diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
        })]);
    }
}
