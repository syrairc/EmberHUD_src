using EmberHUD.Host.Plugins;
using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

internal sealed class PluginFixture : IDisposable
{
    public const string Lifecycle = """
        public override void Initialize() => Log.Info("init");
        public override void OnEnable() => Log.Info("enable");
        public override void OnDisable() => Log.Info("disable");
        public override void Tick() => Log.Info("tick");
        public override void Dispose() => Log.Info("dispose");
        """;

    readonly TempDir _temp = new();

    public string Root => _temp.Path;

    public SdkSettings Sdk { get; } = new();

    public PluginHost Host() => new(Sdk, Root);

    public PluginHost Start()
    {
        var host = Host();
        host.Discover(Plugins);
        Settle(host);
        return host;
    }

    public static PluginSlot Slot(PluginHost host, string name) => host.Slots.Single(slot => slot.Name == name);

    public static void AssertCollected(Func<bool> alive, string what)
    {
        for (var i = 0; i < 20 && alive(); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(alive(), $"{what} still loaded");
    }

    public static void Settle(PluginHost host)
    {
        var deadline = Environment.TickCount64 + 60_000;
        host.TickAll();
        while (host.Slots.Any(slot => slot.Compiling))
        {
            Assert.True(Environment.TickCount64 < deadline, "plugins still compiling after 60 s");
            Thread.Sleep(20);
            host.TickAll();
        }
    }

    public string Plugins => Path.Combine(Root, "Plugins");

    public void Write(string plugin, string file, string text)
    {
        var path = Path.Combine(Plugins, plugin, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public static string Source(string name, string main, string settings = "") => $$"""
        using System;
        using EmberHUD;
        using Hexa.NET.ImGui;

        namespace {{name}};

        public sealed class Main : Plugin<Settings>
        {
        {{main}}
        }

        public sealed class Settings : PluginSettings
        {
        {{settings}}
        {{(settings.Contains("void Draw(") ? "" : "public override void Draw() { }")}}
        }
        """;

    public void Dispose() => _temp.Dispose();
}
