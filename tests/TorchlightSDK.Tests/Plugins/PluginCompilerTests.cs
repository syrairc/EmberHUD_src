using System.Reflection.Metadata;
using System.Text;
using TorchlightSDK.Host.Plugins;

namespace TorchlightSDK.Tests;

public sealed class PluginCompilerTests
{
    static CompileResult Compile(string code) => PluginCompiler.Compile("Probe", [new SourceFile("Main.cs", Encoding.UTF8.GetBytes(code))]);

    [Fact]
    public void GoodSourceCompilesAgainstTheApiAndImGui()
    {
        var result = Compile("""
            using TorchlightSDK;
            using Hexa.NET.ImGui;

            namespace Probe;

            public sealed class Main : Plugin
            {
                public override void Render() => ImGui.TextUnformatted(Game.State.Status);

                unsafe int* Nothing => null;

                int[] Values => [1, 2];
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Assembly);
    }

    [Fact]
    public void ErrorsCarryFileLineAndCode()
    {
        var result = Compile("namespace Probe;\npublic static class A\n{\n    public static int X = \"no\";\n    static void Unused() { int y = 0; }\n}\n");
        Assert.Null(result.Assembly);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("Main.cs(4,", error);
        Assert.Contains("CS0029", error);
    }

    [Fact]
    public void StackTracesCarryFileAndLine()
    {
        var result = Compile("namespace Probe;\npublic static class A\n{\n    public static void Boom() => throw new System.InvalidOperationException();\n}\n");
        Assert.NotNull(result.Assembly);
        var boom = System.Reflection.Assembly.Load(result.Assembly).GetType("Probe.A")!.GetMethod("Boom")!;
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => boom.Invoke(null, null));
        Assert.Contains("Main.cs:line 4", ex.InnerException!.StackTrace);
    }

    [Fact]
    public void EmbeddedSourceMatchesTheFile()
    {
        var code = "namespace Probe;\npublic static class A { }\n";
        var result = Compile(code);
        Assert.NotNull(result.Assembly);
        using var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(result.Assembly));
        var entry = pe.ReadDebugDirectory().Single(directory => directory.Type == System.Reflection.PortableExecutable.DebugDirectoryEntryType.EmbeddedPortablePdb);
        using var provider = pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
        var reader = provider.GetMetadataReader();
        var embeddedSource = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
        var document = reader.Documents.Single(handle => reader.GetString(reader.GetDocument(handle).Name) == "Main.cs");
        var row = reader.GetCustomDebugInformation(document).Select(reader.GetCustomDebugInformation).Single(info => reader.GetGuid(info.Kind) == embeddedSource);
        var blob = reader.GetBlobBytes(row.Value);
        var size = BitConverter.ToInt32(blob, 0);
        string text;
        if (size == 0) text = Encoding.UTF8.GetString(blob, 4, blob.Length - 4);
        else
        {
            using var inflated = new System.IO.Compression.DeflateStream(new MemoryStream(blob, 4, blob.Length - 4), System.IO.Compression.CompressionMode.Decompress);
            using var plain = new MemoryStream();
            inflated.CopyTo(plain);
            text = Encoding.UTF8.GetString(plain.ToArray());
        }
        Assert.Equal(code, text);
    }

    [Fact]
    public void HostTypesAreOutOfReach()
    {
        var result = Compile("namespace Probe; public static class A { public static object X => typeof(TorchlightSDK.Host.Plugins.PluginCache); }");
        Assert.Null(result.Assembly);
        Assert.Contains(result.Diagnostics, line => line.Contains("CS0234") && line.Contains("'Host'"));
    }
}
