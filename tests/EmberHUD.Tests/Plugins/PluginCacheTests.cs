using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class PluginCacheTests : IDisposable
{
    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();

    public void Dispose()
    {
        _log.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void PluginsAreFoldersWithSourceSkippingBuildAndDotFolders()
    {
        _fixture.Write("Beta", "Main.cs", "class B;");
        _fixture.Write("alpha", "Main.cs", "class A;");
        _fixture.Write("bin", "Junk.cs", "class J;");
        _fixture.Write("obj", "Junk.cs", "class J;");
        _fixture.Write(".vs", "Junk.cs", "class J;");
        _fixture.Write("OnlyTests", "Tests/T.cs", "class T;");
        _fixture.Write("NoCode", "readme.txt", "nothing");
        Assert.Equal(["alpha", "Beta"], PluginCache.Plugins(_fixture.Plugins).Select(directory => Path.GetFileName(directory)));
        Assert.Empty(PluginCache.Plugins(Path.Combine(_fixture.Root, "missing")));
    }

    [Fact]
    public void ReadLeavesTestsOutUnlessAskedAndSkipsBuildFoldersAtAnyDepth()
    {
        _fixture.Write("P", "Main.cs", "a");
        _fixture.Write("P", "Ui/Page.cs", "b");
        _fixture.Write("P", "Tests/MainTests.cs", "c");
        _fixture.Write("P", "obj/Gen.cs", "d");
        _fixture.Write("P", "Ui/bin/Gen.cs", "e");
        var directory = Path.Combine(_fixture.Plugins, "P");
        Assert.Equal(["Main.cs", Path.Combine("Ui", "Page.cs")], PluginCache.Read(directory, tests: false).Select(source => source.Path));
        Assert.Equal(["Main.cs", Path.Combine("Tests", "MainTests.cs"), Path.Combine("Ui", "Page.cs")], PluginCache.Read(directory, tests: true).Select(source => source.Path));
        Assert.Equal("a"u8.ToArray(), PluginCache.Read(directory, tests: false)[0].Bytes);
    }

    [Fact]
    public void TestsFolderIsLeftOutInAnyCase()
    {
        _fixture.Write("Lower", "Main.cs", "a");
        _fixture.Write("Lower", "tests/T.cs", "b");
        _fixture.Write("Upper", "Main.cs", "a");
        _fixture.Write("Upper", "TESTS/T.cs", "b");
        foreach (var plugin in new[] { "Lower", "Upper" })
        {
            var directory = Path.Combine(_fixture.Plugins, plugin);
            Assert.Equal(["Main.cs"], PluginCache.Read(directory, tests: false).Select(source => source.Path));
            Assert.Equal(2, PluginCache.Read(directory, tests: true).Count);
        }
    }

    [Fact]
    public void FailedWriteKeepsTheOldCacheAndOrphanTempFilesAreCleaned()
    {
        _fixture.Write("Safe", "Main.cs", "namespace Safe; public static class A { public static int X => 1; }");
        var directory = Path.Combine(_fixture.Plugins, "Safe");
        var cache = Path.Combine(_fixture.Root, "Cache");
        PluginCache.Build("Safe", directory, cache);
        var old = Assert.Single(Directory.GetFiles(cache));

        _fixture.Write("Safe", "Main.cs", "namespace Safe; public static class A { public static int X => 2; }");
        var next = Path.Combine(cache, $"Safe-{PluginCache.Key(PluginCache.Read(directory, tests: false))}.dll");
        Directory.CreateDirectory(next + ".tmp");
        var built = PluginCache.Build("Safe", directory, cache);
        Assert.NotNull(built.Assembly);
        Assert.False(built.FromCache);
        Assert.Contains("could not update the compile cache", _log.Text);
        Assert.True(File.Exists(old));
        Directory.Delete(next + ".tmp");

        var orphan = Path.Combine(cache, "Safe-0123456789ABCDEF.dll.tmp");
        File.WriteAllText(orphan, "half written");
        Assert.False(PluginCache.Build("Safe", directory, cache).FromCache);
        Assert.Equal([Path.GetFileName(next)], Directory.GetFiles(cache).Select(file => Path.GetFileName(file)));
    }

    [Fact]
    public void ALockedStaleCacheFileStillReturnsTheCompiledResult()
    {
        _fixture.Write("Stale", "Main.cs", "namespace Stale; public static class A { public static int X => 1; }");
        var directory = Path.Combine(_fixture.Plugins, "Stale");
        var cache = Path.Combine(_fixture.Root, "Cache");
        PluginCache.Build("Stale", directory, cache);
        var old = Assert.Single(Directory.GetFiles(cache));
        _fixture.Write("Stale", "Main.cs", "namespace Stale; public static class A { public static int X => 2; }");
        using (new FileStream(old, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var built = PluginCache.Build("Stale", directory, cache);
            Assert.NotNull(built.Assembly);
            Assert.Empty(built.Diagnostics);
        }
        Assert.Contains("could not update the compile cache", _log.Text);
    }

    [Fact]
    public void AnUnwritableCacheDirectoryStillReturnsTheCompiledResult()
    {
        _fixture.Write("Blocked", "Main.cs", "namespace Blocked; public static class A { public static int X => 1; }");
        var directory = Path.Combine(_fixture.Plugins, "Blocked");
        var cache = Path.Combine(_fixture.Root, "Cache");
        File.WriteAllText(cache, "");
        var built = PluginCache.Build("Blocked", directory, cache);
        Assert.NotNull(built.Assembly);
        Assert.Contains("could not update the compile cache", _log.Text);
    }

    [Fact]
    public void KeyFollowsContentAndPath()
    {
        SourceFile[] main = [new("Main.cs", "x"u8.ToArray())];
        Assert.Equal(PluginCache.Key(main), PluginCache.Key([new("Main.cs", "x"u8.ToArray())]));
        Assert.NotEqual(PluginCache.Key(main), PluginCache.Key([new("Main.cs", "y"u8.ToArray())]));
        Assert.NotEqual(PluginCache.Key(main), PluginCache.Key([new("Other.cs", "x"u8.ToArray())]));
        Assert.NotEqual(PluginCache.Key(main), PluginCache.Key([new("Mian.cs", "x"u8.ToArray())]));
        Assert.NotEqual(PluginCache.Key([new("a", []), new("b", [])]), PluginCache.Key([new("a\0\0\0\0b", [])]));
        Assert.Equal(16, PluginCache.Key(main).Length);
    }

    [Fact]
    public void BuildHitsTheCacheAndDropsOnlyItsOwnStaleHashes()
    {
        _fixture.Write("Cached", "Main.cs", "namespace Cached; public static class A { public static int X => 1; }");
        var directory = Path.Combine(_fixture.Plugins, "Cached");
        var cache = Path.Combine(_fixture.Root, "Cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "Cached-Extra-0123456789ABCDEF.dll"), "another plugin's cache");

        var first = PluginCache.Build("Cached", directory, cache);
        var second = PluginCache.Build("Cached", directory, cache);
        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.NotNull(first.Assembly);
        Assert.Equal(first.Assembly, second.Assembly);

        _fixture.Write("Cached", "Main.cs", "namespace Cached; public static class A { public static int X => 2; }");
        Assert.False(PluginCache.Build("Cached", directory, cache).FromCache);
        var files = Directory.GetFiles(cache).Select(file => Path.GetFileName(file)).ToList();
        Assert.Equal(2, files.Count);
        Assert.Contains("Cached-Extra-0123456789ABCDEF.dll", files);
        Assert.Contains($"Cached-{PluginCache.Key(PluginCache.Read(directory, tests: false))}.dll", files);

        _fixture.Write("Cached", "Main.cs", "namespace Cached; public static class A { public static int X => \"no\"; }");
        Assert.Null(PluginCache.Build("Cached", directory, cache).Assembly);
        Assert.Equal(2, Directory.GetFiles(cache).Length);
    }
}
