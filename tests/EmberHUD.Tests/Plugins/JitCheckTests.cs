using System.Reflection;
using System.Runtime.Loader;
using EmberHUD.Host.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EmberHUD.Tests;

public class JitCheckTests
{
    [Fact]
    public void HostAssemblyJitsClean()
    {
        var report = new StringWriter();
        Assert.Equal(0, CheckRunner.Jit(typeof(CheckRunner).Assembly, report));
        Assert.Matches(@"host: jit [1-9]\d* methods, 0 failed", report.ToString());
    }

    [Fact]
    public void AJitFailureFailsTheCheckRun()
    {
        var before = Compile("Shapes", "public interface IShape { int Sides(); }");
        var after = Compile("Shapes", "public interface IShape { int Sides(); int Corners(); }");
        var square = Compile("Square", "public sealed class Square : IShape { public int Sides() => 4; }", before);
        var context = new SwapContext(after);
        using var temp = new TempDir();
        var report = new StringWriter();
        try
        {
            Assert.Equal(1, CheckRunner.Run(context.LoadFromStream(new MemoryStream(square)), temp.Path, report));
            Assert.Contains("check: 0 plugins, 0 tests, 1 failed", report.ToString());
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void ATypeLoadFailureDoesNotHideAMethodFailureInAnotherType()
    {
        var before = Compile("Shapes", "public interface IShape { int Sides(); } public static class Tool { public static int Old() => 1; }");
        var after = Compile("Shapes", "public interface IShape { int Sides(); int Corners(); } public static class Tool { }");
        var mixed = Compile("Mixed", "public sealed class Square : IShape { public int Sides() => 4; } public static class Caller { public static int Call() => Tool.Old(); }", before);
        var context = new SwapContext(after);
        var report = new StringWriter();
        try
        {
            Assert.Equal(2, CheckRunner.Jit(context.LoadFromStream(new MemoryStream(mixed)), report));
            Assert.Contains("host: type load", report.ToString());
            Assert.Contains("host: jit Caller.Call - ", report.ToString());
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void MethodCallingAMissingMemberFails()
    {
        var before = Compile("Shapes", "public static class Tool { public static int Old() => 1; }");
        var after = Compile("Shapes", "public static class Tool { }");
        var caller = Compile("Caller", "public static class Caller { public static int Call() => Tool.Old(); }", before);
        var context = new SwapContext(after);
        var report = new StringWriter();

        try
        {
            Assert.Equal(1, CheckRunner.Jit(context.LoadFromStream(new MemoryStream(caller)), report));
            Assert.Contains("host: jit Caller.Call - ", report.ToString());
            Assert.Contains("1 failed", report.ToString());
        }
        finally
        {
            context.Unload();
        }
    }

    static byte[] Compile(string name, string source, byte[]? reference = null)
    {
        List<MetadataReference> references = [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        if (reference is not null) references.Add(MetadataReference.CreateFromImage(reference));
        var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return image.ToArray();
    }

    sealed class SwapContext(byte[] shapes) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) => name.Name == "Shapes" ? LoadFromStream(new MemoryStream(shapes)) : null;
    }
}
