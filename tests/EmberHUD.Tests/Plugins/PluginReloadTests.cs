using System.Reflection;
using System.Runtime.CompilerServices;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class PluginReloadTests : IDisposable
{
    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();

    public void Dispose()
    {
        _log.Dispose();
        _fixture.Dispose();
    }

    static string Ticking(string name, string version) => PluginFixture.Source(name, $$"""public override void Tick() => Log.Info("{{version}}");""");

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference Weak(PluginSlot slot) => new(slot.Context);

    [Fact]
    public void ReloadUnloadsTheOldContextAndRunsTheNewSource()
    {
        _fixture.Write("Hot", "Main.cs", Ticking("Hot", "v1"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        host.SetEnabled(slot, true);
        host.TickAll();
        var old = Weak(slot);

        _fixture.Write("Hot", "Main.cs", Ticking("Hot", "v2"));
        host.Reload(slot);
        Assert.True(slot.Compiling);
        Assert.Null(slot.Instance);
        PluginFixture.Settle(host);
        host.TickAll();

        for (var i = 0; i < 20 && old.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(old.IsAlive);
        Assert.Equal(["v1", "v2", "v2"], _log.Messages("Hot"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference WeakAssembly(PluginSlot slot) => new(slot.Instance!.GetType().Assembly);

    [Fact]
    public void ReloadUnloadsTheContextEvenWhileSomethingStillHoldsIt()
    {
        _fixture.Write("Held", "Main.cs", Ticking("Held", "v1"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        var held = slot.Context;
        var assembly = WeakAssembly(slot);
        host.Reload(slot);
        PluginFixture.Settle(host);
        for (var i = 0; i < 20 && assembly.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(assembly.IsAlive);
        GC.KeepAlive(held);
    }

    [Fact]
    public void ReloadIsTheRetryForAnErroredPlugin()
    {
        _fixture.Write("Fixable", "Main.cs", "namespace Fixable; public sealed class Main : EmberHUD.Plugin { int X = \"no\"; }");
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        Assert.Equal(PluginStage.Compile, slot.ErrorStage);

        _fixture.Write("Fixable", "Main.cs", PluginFixture.Source("Fixable", ""));
        host.Reload(slot);
        PluginFixture.Settle(host);
        Assert.Null(slot.ErrorStage);
        Assert.Empty(slot.Diagnostics);
        Assert.NotNull(slot.Instance);
    }

    [Fact]
    public void TheWatcherReloadsAChangedPluginWhenAutoReloadIsOn()
    {
        _fixture.Write("Watched", "Main.cs", Ticking("Watched", "v1"));
        _fixture.Sdk.AutoReload = true;
        using var host = _fixture.Start();
        host.SetEnabled(host.Slots[0], true);
        host.TickAll();

        _fixture.Write("Watched", "Main.cs", Ticking("Watched", "v2"));
        var deadline = Environment.TickCount64 + 60_000;
        while (!_log.Messages("Watched").Contains("v2"))
        {
            Assert.True(Environment.TickCount64 < deadline, "no reload 60 s after the change");
            Thread.Sleep(50);
            host.TickAll();
        }
    }

    [Fact]
    public void WriteDefaultsDumpsTheCurrentValuesAndResetRebuildsFromThem()
    {
        _fixture.Write("Resettable", "Main.cs", PluginFixture.Source("Resettable", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        var plugin = slot.Instance!;
        var user = Path.Combine(_fixture.Root, "Config", "Plugins", "Resettable.json");

        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 42 }""", _ => { });
        host.WriteDefaults(slot);
        Assert.Contains("42", File.ReadAllText(Path.Combine(slot.Directory, "defaults.json")));

        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 77 }""", _ => { });
        host.SaveAll();
        Assert.Contains("77", File.ReadAllText(user));

        host.ResetSettings(slot);
        Assert.Contains("42", SettingsJson.Serialize(plugin.SettingsObject!));
        Assert.False(File.Exists(user));
        host.SaveAll();
        Assert.False(File.Exists(user));
    }

    [Fact]
    public void ClearCacheDeletesCompiledPluginsOnly()
    {
        _fixture.Write("Cached", "Main.cs", PluginFixture.Source("Cached", ""));
        using var host = _fixture.Start();
        var cache = Path.Combine(_fixture.Root, "Cache");
        File.WriteAllText(Path.Combine(cache, "slots.bin"), "class slots");
        Assert.NotEmpty(Directory.GetFiles(cache, "*.dll"));
        host.ClearCache();
        Assert.Empty(Directory.GetFiles(cache, "*.dll"));
        Assert.True(File.Exists(Path.Combine(cache, "slots.bin")));
    }

    [Fact]
    public void AChangeStampOnlyReloadsOnceTheDebounceHasPassed()
    {
        _fixture.Write("Debounced", "Main.cs", Ticking("Debounced", "v1"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];

        slot.ChangedAt = Environment.TickCount64;
        host.TickAll();
        Assert.False(slot.Compiling);
        Assert.NotNull(slot.Instance);

        slot.ChangedAt = Environment.TickCount64 - 600;
        host.TickAll();
        Assert.True(slot.Compiling);
        Assert.Equal(0, slot.ChangedAt);
        PluginFixture.Settle(host);
        Assert.NotNull(slot.Instance);
    }

    [Fact]
    public void WatchersFollowTheAutoReloadToggleAndAreDisposedWithTheHost()
    {
        _fixture.Write("A", "Main.cs", PluginFixture.Source("A", ""));
        _fixture.Write("B", "Main.cs", PluginFixture.Source("B", ""));
        var host = _fixture.Start();
        Assert.Empty(host.Watchers);

        _fixture.Sdk.AutoReload = true;
        host.TickAll();
        var first = host.Watchers.ToList();
        Assert.Equal(2, first.Count);
        Assert.All(first, watcher => Assert.True(watcher.EnableRaisingEvents));
        host.TickAll();
        Assert.Equal(first, host.Watchers);

        _fixture.Sdk.AutoReload = false;
        host.TickAll();
        Assert.Empty(host.Watchers);
        Assert.All(first, watcher => Assert.False(watcher.EnableRaisingEvents));

        _fixture.Sdk.AutoReload = true;
        host.TickAll();
        var second = host.Watchers.ToList();
        Assert.Equal(2, second.Count);
        host.Dispose();
        Assert.Empty(host.Watchers);
        Assert.All(second, watcher => Assert.False(watcher.EnableRaisingEvents));
    }

    [Fact]
    public void ReloadingAPluginThatNeverInitializedDoesNotDisposeIt()
    {
        _fixture.Write("Cold", "Main.cs", PluginFixture.Source("Cold", PluginFixture.Lifecycle));
        using var host = _fixture.Start();
        host.Reload(host.Slots[0]);
        PluginFixture.Settle(host);
        Assert.DoesNotContain("dispose", _log.Messages("Cold"));
        Assert.DoesNotContain("disable", _log.Messages("Cold"));
    }

    [Fact]
    public void ReloadKeepsAnEnabledPluginEnabledAndRunsItsLifecycleAgain()
    {
        _fixture.Write("Life", "Main.cs", PluginFixture.Source("Life", PluginFixture.Lifecycle));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        host.SetEnabled(slot, true);
        host.Reload(slot);
        PluginFixture.Settle(host);
        Assert.True(slot.Enabled);
        Assert.Equal(["init", "enable", "disable", "dispose", "init", "enable", "tick"], _log.Messages("Life"));
    }

    [Fact]
    public void AMissingSerializerCacheHookWarnsOnceAcrossUnloads()
    {
        _fixture.Write("One", "Main.cs", PluginFixture.Source("One", ""));
        _fixture.Write("Two", "Main.cs", PluginFixture.Source("Two", ""));
        var host = _fixture.Start();
        host.ClearSerializerCache = null;
        host.Reload(host.Slots[0]);
        PluginFixture.Settle(host);
        host.Reload(host.Slots[1]);
        PluginFixture.Settle(host);
        host.Dispose();
        Assert.Single(_log.Messages(), message => message.Contains("serializer cache"));
    }

    [Fact]
    public void AThrowingSerializerCacheHookIsLoggedAndNeverAbortsTheUnloadLoop()
    {
        _fixture.Write("One", "Main.cs", PluginFixture.Source("One", PluginFixture.Lifecycle));
        _fixture.Write("Two", "Main.cs", PluginFixture.Source("Two", PluginFixture.Lifecycle));
        var host = _fixture.Start();
        host.ClearSerializerCache = typeof(PluginReloadTests).GetMethod(nameof(Throwing), BindingFlags.Static | BindingFlags.NonPublic);
        host.SetEnabled(host.Slots[0], true);
        host.SetEnabled(host.Slots[1], true);
        host.Dispose();
        Assert.All(host.Slots, slot => Assert.Null(slot.Context));
        Assert.Contains("dispose", _log.Messages("Two"));
        Assert.Contains("hook exploded", _log.Text);
    }

    [Fact]
    public void UnloadingAPluginTellsTheHostItsTexturesCanGo()
    {
        _fixture.Write("Hot", "Main.cs", Ticking("Hot", "v1"));
        var gone = new List<string>();
        using (var host = new PluginHost(_fixture.Sdk, _fixture.Root, gone.Add))
        {
            host.Discover(_fixture.Plugins);
            PluginFixture.Settle(host);
            host.Reload(host.Slots[0]);
            Assert.Equal(["Hot"], gone);
            PluginFixture.Settle(host);
        }
        Assert.Equal(["Hot", "Hot"], gone);
    }

    [Fact]
    public void AWatcherThatCannotBeCreatedWarnsOncePerSlotAndNeverStopsOtherPluginsTicking()
    {
        _fixture.Write("Gone", "Main.cs", PluginFixture.Source("Gone", ""));
        _fixture.Write("Live", "Main.cs", Ticking("Live", "t"));
        using var host = _fixture.Start();
        host.SetEnabled(host.Slots.Single(slot => slot.Name == "Live"), true);
        Directory.Delete(Path.Combine(_fixture.Plugins, "Gone"), true);
        _fixture.Sdk.AutoReload = true;
        for (var i = 0; i < 3; i++) host.TickAll();
        Assert.Equal(3, _log.Messages("Live").Count(message => message == "t"));
        Assert.Single(_log.Messages(), message => message.Contains("could not watch"));
        Assert.Single(host.Watchers);
    }

    [Fact]
    public void ReloadClearsAPendingStampSoItReloadsOnlyOnce()
    {
        _fixture.Write("Once", "Main.cs", Ticking("Once", "v"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        slot.ChangedAt = Environment.TickCount64 - 600;
        host.Reload(slot);
        Assert.Equal(0, slot.ChangedAt);
        PluginFixture.Settle(host);
    }

    [Fact]
    public void TurningAutoReloadOffDropsAStaleStamp()
    {
        _fixture.Write("Stale", "Main.cs", Ticking("Stale", "v"));
        _fixture.Sdk.AutoReload = true;
        using var host = _fixture.Start();
        host.TickAll();
        var slot = host.Slots[0];
        Assert.Single(host.Watchers);
        slot.ChangedAt = Environment.TickCount64;
        _fixture.Sdk.AutoReload = false;
        host.TickAll();
        Assert.Equal(0, slot.ChangedAt);
    }

    [Fact]
    public void WriteDefaultsRewritesAFileThatWasDeletedBehindItsBack()
    {
        _fixture.Write("Defaulted", "Main.cs", PluginFixture.Source("Defaulted", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        var path = Path.Combine(slot.Directory, "defaults.json");
        host.WriteDefaults(slot);
        File.Delete(path);
        host.WriteDefaults(slot);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ResetSettingsDeletesTheUserFileEvenWithoutAnInstance()
    {
        _fixture.Write("Broken", "Main.cs", "namespace Broken; public sealed class Main : EmberHUD.Plugin { int X = \"no\"; }");
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        Assert.Null(slot.Instance);
        var user = Path.Combine(_fixture.Root, "Config", "Plugins", "Broken.json");
        Directory.CreateDirectory(Path.GetDirectoryName(user)!);
        File.WriteAllText(user, "{}");
        host.ResetSettings(slot);
        Assert.False(File.Exists(user));
    }

    [Fact]
    public void ResetSettingsWithALockedUserFileWarnsAndDoesNotErrorAHealthyPlugin()
    {
        _fixture.Write("Locked", "Main.cs", PluginFixture.Source("Locked", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = _fixture.Start();
        var slot = host.Slots[0];
        var user = Path.Combine(_fixture.Root, "Config", "Plugins", "Locked.json");
        Directory.CreateDirectory(Path.GetDirectoryName(user)!);
        File.WriteAllText(user, "{}");
        using (new FileStream(user, FileMode.Open, FileAccess.Read, FileShare.None))
            host.ResetSettings(slot);
        Assert.Null(slot.ErrorStage);
        Assert.Contains("could not delete", _log.Text);
        Assert.DoesNotContain("reset to defaults", _log.Text);
    }

    [Fact]
    public void ClearCacheWithALockedFileWarnsAndDoesNotClaimSuccess()
    {
        _fixture.Write("Cached", "Main.cs", PluginFixture.Source("Cached", ""));
        using var host = _fixture.Start();
        var locked = Directory.GetFiles(Path.Combine(_fixture.Root, "Cache"), "*.dll").Single();
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            host.ClearCache();
        Assert.True(File.Exists(locked));
        Assert.Contains("could not delete", _log.Text);
        Assert.DoesNotContain("compile cache cleared", _log.Text);
    }

    [Fact]
    public void ClearCacheWithoutACacheDirectoryDoesNotThrowOrClaimSuccess()
    {
        using var host = _fixture.Host();
        host.ClearCache();
        Assert.DoesNotContain("compile cache cleared", _log.Text);
    }

    static void Throwing(object? options) => throw new InvalidOperationException("hook exploded");
}
