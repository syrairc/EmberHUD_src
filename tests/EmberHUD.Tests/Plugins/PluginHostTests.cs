using System.Text.Json.Nodes;
using EmberHUD.Host.Plugins;
using EmberHUD.Testing;

namespace EmberHUD.Tests;

public sealed class PluginHostTests : IDisposable
{
    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();

    public void Dispose()
    {
        _log.Dispose();
        _fixture.Dispose();
    }

    PluginHost Start()
    {
        var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        PluginFixture.Settle(host);
        return host;
    }

    static PluginSlot Slot(PluginHost host, string name) => host.Slots.Single(slot => slot.Name == name);

    [Fact]
    public void DiscoveredPluginsStartDisabledAndCompiling()
    {
        _fixture.Write("Alpha", "Main.cs", PluginFixture.Source("Alpha", ""));
        _fixture.Write("Beta", "Main.cs", PluginFixture.Source("Beta", ""));
        using var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        Assert.Equal(["Alpha", "Beta"], host.Slots.Select(slot => slot.Name));
        Assert.All(host.Slots, slot => Assert.True(slot.Compiling));
        Assert.All(host.Slots, slot => Assert.False(slot.Enabled));
        Assert.False(_fixture.Sdk.Plugins["Alpha"].Enabled);
        PluginFixture.Settle(host);
        Assert.All(host.Slots, slot => Assert.NotNull(slot.Instance));
        Assert.Equal("Alpha", Slot(host, "Alpha").Instance!.Name);
    }

    [Fact]
    public void LifecycleRunsInOrderUnderThePluginsName()
    {
        _fixture.Write("Life", "Main.cs", PluginFixture.Source("Life", PluginFixture.Lifecycle));
        using var host = Start();
        var slot = Slot(host, "Life");
        host.SetEnabled(slot, true);
        host.TickAll();
        host.SetEnabled(slot, false);
        host.TickAll();
        host.SetEnabled(slot, true);
        host.TickAll();
        Assert.Equal(["init", "enable", "tick", "disable", "enable", "tick"], _log.Messages("Life"));
    }

    [Fact]
    public void DisposeDisablesAndDisposesEnabledPlugins()
    {
        _fixture.Write("Life", "Main.cs", PluginFixture.Source("Life", PluginFixture.Lifecycle));
        using (var host = Start()) host.SetEnabled(Slot(host, "Life"), true);
        Assert.Equal(["init", "enable", "disable", "dispose"], _log.Messages("Life"));
    }

    [Fact]
    public void ACompileErrorKeepsItsDiagnosticsAndOtherPluginsStillRun()
    {
        _fixture.Write("Broken", "Main.cs", "namespace Broken; public sealed class Main : EmberHUD.Plugin { int X = \"no\"; }");
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", PluginFixture.Lifecycle));
        using var host = Start();
        var broken = Slot(host, "Broken");
        Assert.Equal(PluginStage.Compile, broken.ErrorStage);
        Assert.Null(broken.Instance);
        Assert.Contains(broken.Diagnostics, line => line.StartsWith("Main.cs(1,") && line.Contains("CS0029"));
        Assert.Contains("Broken compile failed", _log.Text);
        host.SetEnabled(Slot(host, "Good"), true);
        host.TickAll();
        Assert.Contains("tick", _log.Messages("Good"));
    }

    [Fact]
    public void AThrowingTickErrorsOnlyThatPluginOnceWithAFileAndLine()
    {
        _fixture.Write("Thrower", "Main.cs", PluginFixture.Source("Thrower", """
            public override void Tick()
            {
                Log.Info("tick");
                throw new InvalidOperationException("boom");
            }
            """));
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", PluginFixture.Lifecycle));
        using var host = Start();
        host.SetEnabled(Slot(host, "Thrower"), true);
        host.SetEnabled(Slot(host, "Good"), true);
        for (var i = 0; i < 3; i++) host.TickAll();
        var thrower = Slot(host, "Thrower");
        Assert.Equal(PluginStage.Tick, thrower.ErrorStage);
        Assert.Equal("InvalidOperationException: boom", thrower.Error);
        Assert.Single(_log.Messages("Thrower"), message => message == "tick");
        Assert.Contains("Main.cs:line", _log.Text);
        Assert.Equal(3, _log.Messages("Good").Count(message => message == "tick"));
    }

    [Fact]
    public void ARenderThatThrowsInsideOpenStacksIsRecoveredInsideTheFrame()
    {
        _fixture.Write("Leaky", "Main.cs", PluginFixture.Source("Leaky", """
            public override void Render()
            {
                ImGui.Begin("leaky");
                ImGui.PushID("deep");
                throw new InvalidOperationException("mid-frame");
            }
            """));
        using var host = Start();
        var slot = Slot(host, "Leaky");
        host.SetEnabled(slot, true);
        TestFrame.Run(host.RenderAll);
        Assert.Equal(PluginStage.Render, slot.ErrorStage);
        TestFrame.Run(host.RenderAll);
        Assert.Single(_log.Messages("Leaky"));
    }

    [Fact]
    public void ARenderThatLeaksABeginWithoutThrowingIsCleanedUpAndKeepsRunning()
    {
        _fixture.Write("Forgetful", "Main.cs", PluginFixture.Source("Forgetful", """public override void Render() => ImGui.Begin("forgot to end");"""));
        using var host = Start();
        var slot = Slot(host, "Forgetful");
        host.SetEnabled(slot, true);
        TestFrame.Run(host.RenderAll);
        TestFrame.Run(host.RenderAll);
        Assert.Null(slot.ErrorStage);
    }

    [Fact]
    public void RenderRunsByOrderThenName()
    {
        _fixture.Write("A", "Main.cs", PluginFixture.Source("A", """public override void Render() => Log.Info("render A");"""));
        _fixture.Write("B", "Main.cs", PluginFixture.Source("B", """
            public override int Order => -1;
            public override void Render() => Log.Info("render B");
            """));
        _fixture.Write("C", "Main.cs", PluginFixture.Source("C", """public override void Render() => Log.Info("render C");"""));
        using var host = Start();
        foreach (var slot in host.Slots) host.SetEnabled(slot, true);
        host.RenderAll();
        Assert.Equal(["render B", "render A", "render C"], _log.Messages().Where(message => message.StartsWith("render")));
    }

    [Fact]
    public void ALeakedLayerScopeIsResetBeforeTheNextPlugin()
    {
        _fixture.Write("Leaker", "Main.cs", PluginFixture.Source("Leaker", """
            public override int Order => -1;
            public override void Render() => _ = Draw.Layer(DrawLayer.Back);
            """));
        _fixture.Write("Next", "Main.cs", PluginFixture.Source("Next", """public override void Render() => Log.Info($"layer {Draw.Background.Splitter.Current}");"""));
        using var host = Start();
        foreach (var slot in host.Slots) host.SetEnabled(slot, true);
        TestFrame.Run(host.RenderAll);
        Assert.Equal(["layer 1"], _log.Messages("Next"));
    }

    [Fact]
    public void TwoPluginClassesIsAnError()
    {
        _fixture.Write("Twins", "Main.cs", """
            namespace Twins;
            public sealed class One : EmberHUD.Plugin;
            public sealed class Two : EmberHUD.Plugin;
            """);
        using var host = Start();
        var slot = Slot(host, "Twins");
        Assert.Equal(PluginStage.Initialize, slot.ErrorStage);
        Assert.Contains("found 2", slot.Error);
    }

    [Fact]
    public void TheTestsFolderIsLeftOutOfTheNormalLoad()
    {
        _fixture.Write("Tested", "Main.cs", PluginFixture.Source("Tested", ""));
        _fixture.Write("Tested", "Tests/MainTests.cs", "this is not C#");
        using var host = Start();
        var slot = Slot(host, "Tested");
        Assert.Null(slot.ErrorStage);
        Assert.NotNull(slot.Instance);
    }

    [Fact]
    public void EnableStateLivesInSdkJson()
    {
        _fixture.Write("Alpha", "Main.cs", PluginFixture.Source("Alpha", ""));
        using var host = Start();
        host.SetEnabled(Slot(host, "Alpha"), true);
        host.SaveAll();
        var sdk = JsonNode.Parse(File.ReadAllText(Path.Combine(_fixture.Root, "Config", "sdk.json")))!;
        Assert.True(sdk["Plugins"]!["Alpha"]!["Enabled"]!.GetValue<bool>());
        Assert.Equal("F11", sdk["Hotkeys"]!["Settings"]!.GetValue<string>());
        Assert.Equal("Ctrl+F12", sdk["Hotkeys"]!["Quit"]!.GetValue<string>());
    }

    const string TwoHotkeys = """
        public static int Fired;
        public override void Initialize()
        {
            RegisterHotkey("Fire", "Fires", () => Fired++, new Hotkey(Key.F1));
            RegisterHotkey("Zoom", "Closer", () => Log.Info("zoom"), new Hotkey(Key.F2), "View");
        }
        public override void Tick() => Log.Info("tick " + Fired);
        """;

    JsonNode ReadSdk() => JsonNode.Parse(File.ReadAllText(Path.Combine(_fixture.Root, "Config", "sdk.json")))!;

    [Fact]
    public void AHotkeyPressFiresItsCallbackBeforeTheTickOfAnEnabledPlugin()
    {
        _fixture.Write("Hk", "Main.cs", PluginFixture.Source("Hk", TwoHotkeys));
        using var host = Start();
        host.SetEnabled(Slot(host, "Hk"), true);
        try
        {
            TestInput.Hold(Key.F2);
            host.TickAll();
        }
        finally
        {
            TestInput.Hold();
        }
        Assert.Equal(["zoom", "tick 0"], _log.Messages("Hk"));
    }

    [Fact]
    public void ADisabledPluginsHotkeysDoNotFire()
    {
        _fixture.Write("Hk", "Main.cs", PluginFixture.Source("Hk", TwoHotkeys));
        using var host = Start();
        try
        {
            TestInput.Hold(Key.F2);
            host.TickAll();
        }
        finally
        {
            TestInput.Hold();
        }
        Assert.Empty(_log.Messages("Hk"));
    }

    [Fact]
    public void AThrowingCallbackErrorsOnlyThatPluginAndOthersKeepFiring()
    {
        _fixture.Write("Thrower", "Main.cs", PluginFixture.Source("Thrower", """
            public override void Initialize() => RegisterHotkey("Boom", "Throws", () => throw new InvalidOperationException("boom"), new Hotkey(Key.F3));
            """));
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", PluginFixture.Lifecycle));
        using var host = Start();
        host.SetEnabled(Slot(host, "Thrower"), true);
        host.SetEnabled(Slot(host, "Good"), true);
        try
        {
            TestInput.Hold(Key.F3);
            host.TickAll();
            host.TickAll();
        }
        finally
        {
            TestInput.Hold();
        }
        Assert.Equal(PluginStage.Tick, Slot(host, "Thrower").ErrorStage);
        Assert.Equal(2, _log.Messages("Good").Count(message => message == "tick"));
    }

    [Fact]
    public void ARebindIsAppliedAfterInitializeAndSavedOnlyWhileItDiffersFromTheDefault()
    {
        _fixture.Write("Hk", "Main.cs", PluginFixture.Source("Hk", TwoHotkeys));
        _fixture.Sdk.PluginHotkeys["Hk"] = new() { ["View/Zoom"] = new Hotkey(Key.F5) };
        using var host = Start();
        var slot = Slot(host, "Hk");
        host.SetEnabled(slot, true);
        Assert.Equal(new Hotkey(Key.F5), slot.Instance!.Hotkeys[1].Value);
        Assert.Equal(new Hotkey(Key.F1), slot.Instance.Hotkeys[0].Value);
        host.SaveAll();
        var saved = ReadSdk()["PluginHotkeys"]!["Hk"]!.AsObject();
        Assert.Equal(["View/Zoom"], saved.Select(pair => pair.Key));
        Assert.Equal("F5", saved["View/Zoom"]!.GetValue<string>());
        slot.Instance.Hotkeys[1].Value = new Hotkey(Key.F2);
        host.SaveAll();
        Assert.Null(ReadSdk()["PluginHotkeys"]!["Hk"]);
    }

    [Fact]
    public void ARebindSurvivesAReload()
    {
        _fixture.Write("Hk", "Main.cs", PluginFixture.Source("Hk", TwoHotkeys));
        using var host = Start();
        var slot = Slot(host, "Hk");
        host.SetEnabled(slot, true);
        slot.Instance!.Hotkeys[0].Value = new Hotkey(Key.F7);
        host.Reload(slot);
        PluginFixture.Settle(host);
        Assert.Equal(new Hotkey(Key.F7), slot.Instance!.Hotkeys[0].Value);
    }

    [Fact]
    public void AStaleOverrideForAHotkeyThePluginNoLongerRegistersIsKeptAndNeverThrows()
    {
        _fixture.Write("Hk", "Main.cs", PluginFixture.Source("Hk", TwoHotkeys));
        _fixture.Sdk.PluginHotkeys["Hk"] = new() { ["Gone"] = new Hotkey(Key.F9) };
        using var host = Start();
        host.SetEnabled(Slot(host, "Hk"), true);
        host.SaveAll();
        Assert.Equal("F9", ReadSdk()["PluginHotkeys"]!["Hk"]!["Gone"]!.GetValue<string>());
    }

    [Fact]
    public void ConflictForNamesTheOtherHolderAndIgnoresNoneAndSelf()
    {
        _fixture.Write("A", "Main.cs", PluginFixture.Source("A", TwoHotkeys));
        _fixture.Write("B", "Main.cs", PluginFixture.Source("B", TwoHotkeys));
        using var host = Start();
        host.SetEnabled(Slot(host, "A"), true);
        host.SetEnabled(Slot(host, "B"), true);
        Assert.Equal("B / Fire", host.ConflictFor(new Hotkey(Key.F1), Slot(host, "A").Instance!.Hotkeys[0]));
        Assert.Equal("EmberHUD / Settings window", host.ConflictFor(new Hotkey(Key.F11), null));
        Assert.Equal("EmberHUD / Quit", host.ConflictFor(new Hotkey(Key.F12, KeyModifiers.Ctrl), null));
        Assert.Null(host.ConflictFor(default, null));
        Assert.Null(host.ConflictFor(new Hotkey(Key.F8), null));
    }

    [Fact]
    public void SettingsLayerCodeThenDefaultsJsonThenTheUserFile()
    {
        _fixture.Write("Layered", "Main.cs", PluginFixture.Source("Layered", "", """
            public RangeNode<float> X = new(10, 0, 100);
            public RangeNode<float> Y = new(20, 0, 100);
            public RangeNode<float> Z = new(30, 0, 100);
            """));
        _fixture.Write("Layered", "defaults.json", """{ "X": 60, "Y": 50 }""");
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "Config", "Plugins"));
        File.WriteAllText(Path.Combine(_fixture.Root, "Config", "Plugins", "Layered.json"), """{ "Y": 70, "Z": "big" }""");
        using var host = Start();
        var settings = JsonNode.Parse(SettingsJson.Serialize(Slot(host, "Layered").Instance!.SettingsObject!))!;
        Assert.Equal(60f, settings["X"]!.GetValue<float>());
        Assert.Equal(70f, settings["Y"]!.GetValue<float>());
        Assert.Equal(30f, settings["Z"]!.GetValue<float>());
        Assert.Contains(_log.Messages("Layered"), message => message.StartsWith("Layered settings - Z - "));
    }

    [Fact]
    public void AutosaveWritesOnlyWhenTheTextChanged()
    {
        _fixture.Write("Saver", "Main.cs", PluginFixture.Source("Saver", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = Start();
        var plugin = Slot(host, "Saver").Instance!;
        var path = Path.Combine(_fixture.Root, "Config", "Plugins", "Saver.json");

        host.SaveAll();
        Assert.False(File.Exists(path));

        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 99 }""", _ => { });
        host.SaveAll();
        Assert.Contains("99", File.ReadAllText(path));

        File.Delete(path);
        host.SaveAll();
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void AConstructorThatThrowsIsUnwrappedToItsRealException()
    {
        _fixture.Write("Ctor", "Main.cs", PluginFixture.Source("Ctor", """public Main() => throw new InvalidOperationException("ctor");"""));
        using var host = Start();
        var slot = Slot(host, "Ctor");
        Assert.Equal(PluginStage.Initialize, slot.ErrorStage);
        Assert.Equal("InvalidOperationException: ctor", slot.Error);
    }

    [Fact]
    public void APluginAlreadyEnabledInSdkJsonRunsAsSoonAsItLoads()
    {
        _fixture.Write("Early", "Main.cs", PluginFixture.Source("Early", PluginFixture.Lifecycle));
        _fixture.Sdk.Plugins["Early"] = new() { Enabled = true };
        using var host = Start();
        Assert.Equal(["init", "enable"], _log.Messages("Early").Take(2));
    }

    [Fact]
    public void AutosaveRunsFromTickAllAfterTwoSeconds()
    {
        _fixture.Write("Ticker", "Main.cs", PluginFixture.Source("Ticker", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = Start();
        var plugin = Slot(host, "Ticker").Instance!;
        var path = Path.Combine(_fixture.Root, "Config", "Plugins", "Ticker.json");
        host.SaveAll();
        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 42 }""", _ => { });
        host.TickAll();
        Assert.False(File.Exists(path));
        Thread.Sleep(2100);
        host.TickAll();
        Assert.Contains("42", File.ReadAllText(path));
    }

    [Fact]
    public void AFailedWriteWarnsOnceUntilTheTextChanges()
    {
        _fixture.Write("Locked", "Main.cs", PluginFixture.Source("Locked", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = Start();
        var plugin = Slot(host, "Locked").Instance!;
        var path = Path.Combine(_fixture.Root, "Config", "Plugins", "Locked.json");
        Directory.CreateDirectory(path + ".tmp");
        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 5 }""", _ => { });
        host.SaveAll();
        host.SaveAll();
        Assert.Single(_log.Messages(), message => message.StartsWith("could not save") && message.Contains("Locked.json"));
        plugin.SettingsObject = SettingsJson.Load(plugin.SettingsType!, null, """{ "X": 6 }""", _ => { });
        host.SaveAll();
        Assert.Equal(2, _log.Messages().Count(message => message.StartsWith("could not save") && message.Contains("Locked.json")));
        Directory.Delete(path + ".tmp");
        host.SaveAll();
        Assert.Contains("\"X\": 6", File.ReadAllText(path));
        Assert.Equal(2, _log.Messages().Count(message => message.StartsWith("could not save") && message.Contains("Locked.json")));
    }

    [Fact]
    public void EachPluginGetsItsOwnImGuiIdScope()
    {
        foreach (var name in new[] { "One", "Two" })
            _fixture.Write(name, "Main.cs", PluginFixture.Source(name, """public override void Render() => Log.Info($"id {ImGui.GetID("same")}");"""));
        using var host = Start();
        foreach (var slot in host.Slots) host.SetEnabled(slot, true);
        TestFrame.Run(host.RenderAll);
        var ids = host.Slots.Select(slot => Assert.Single(_log.Messages(slot.Name))).ToList();
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    public void DisposeUnloadsThePluginsCollectibleContext()
    {
        _fixture.Write("Gone", "Main.cs", PluginFixture.Source("Gone", PluginFixture.Lifecycle, """
            public Mode Kind = Mode.B;
            public System.Collections.Generic.List<Item> Items = [new Item()];
            """));
        _fixture.Write("Gone", "Types.cs", """
            namespace Gone;
            public enum Mode { A, B }
            public sealed class Item { public Mode Kind = Mode.B; public int N = 3; }
            """);
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "Config", "Plugins"));
        File.WriteAllText(Path.Combine(_fixture.Root, "Config", "Plugins", "Gone.json"), """{ "Kind": "A", "Items": [ { "N": 7 } ] }""");
        var (context, assembly) = LoadAndDispose();
        for (var i = 0; i < 20 && assembly.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(assembly.IsAlive);
        GC.KeepAlive(context);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    (System.Runtime.Loader.AssemblyLoadContext Context, WeakReference Assembly) LoadAndDispose()
    {
        var host = Start();
        var slot = Slot(host, "Gone");
        host.SetEnabled(slot, true);
        host.SaveAll();
        var context = slot.Context!;
        var assembly = new WeakReference(slot.Instance!.GetType().Assembly);
        host.Dispose();
        Assert.Null(slot.Context);
        return (context, assembly);
    }

    [Fact]
    public void AnUnchangedSdkJsonIsNeverRewrittenAndAChangedOneIs()
    {
        using var host = _fixture.Host();
        var path = Path.Combine(_fixture.Root, "Config", "sdk.json");
        host.SaveAll();
        Assert.False(File.Exists(path));
        _fixture.Sdk.Theme = "Other";
        host.SaveAll();
        Assert.Contains("Other", File.ReadAllText(path));
    }

    [Fact]
    public void AnyWriteFailureIsAWarningNotAnException()
    {
        using var host = new PluginHost(_fixture.Sdk, _fixture.Root + "\0bad");
        _fixture.Sdk.Theme = "Other";
        host.SaveAll();
        host.SaveAll();
        Assert.Single(_log.Messages(), message => message.StartsWith("could not save") && message.Contains("sdk.json"));
    }

    [Fact]
    public void DisposeSkipsPluginsThatNeverInitialized()
    {
        _fixture.Write("Idle", "Main.cs", PluginFixture.Source("Idle", PluginFixture.Lifecycle));
        using (Start()) { }
        Assert.Empty(_log.Messages("Idle"));
    }

    [Fact]
    public void AGuardedCallPutsTheOuterPluginNameBack()
    {
        _fixture.Write("Inner", "Main.cs", PluginFixture.Source("Inner", "", "public RangeNode<float> X = new(10, 0, 100);"));
        using var host = Start();
        host.Slots[0].Instance!.SettingsObject = SettingsJson.Load(host.Slots[0].Instance!.SettingsType!, null, """{ "X": 1 }""", _ => { });
        Log.CurrentPlugin.Value = "Outer";
        try
        {
            host.SaveAll();
            Assert.Equal("Outer", Log.CurrentPlugin.Value);
        }
        finally
        {
            Log.CurrentPlugin.Value = null;
        }
    }

    [Fact]
    public void ASettingsSaveFailureKeepsTheFirstError()
    {
        _fixture.Write("Twice", "Main.cs", PluginFixture.Source("Twice", """public override void Tick() => throw new InvalidOperationException("boom");""", """
            public Bomb B = new();
            public sealed class Bomb { public int X => Environment.GetEnvironmentVariable("EMBERHUD_TEST_BOMB") is null ? 1 : throw new NotSupportedException("bomb"); }
            """));
        using var host = Start();
        var slot = Slot(host, "Twice");
        host.SetEnabled(slot, true);
        host.TickAll();
        Environment.SetEnvironmentVariable("EMBERHUD_TEST_BOMB", "1");
        try
        {
            host.SaveAll();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EMBERHUD_TEST_BOMB", null);
        }
        Assert.Contains("bomb", _log.Text);
        Assert.Equal(PluginStage.Tick, slot.ErrorStage);
        Assert.Equal("InvalidOperationException: boom", slot.Error);
    }
}
