using System.Reflection;
using System.Runtime.Loader;
using TorchlightSDK.Host.Ui;

namespace TorchlightSDK.Host.Plugins;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal enum PluginStage { Compile, Initialize, Enable, Tick, Render, Settings, Disable, Dispose }

internal sealed class PluginSlot(string name, string directory, PluginState state)
{
    public long ChangedAt;

    public string Name => name;

    public string Directory => directory;

    public bool Enabled { get => state.Enabled; set => state.Enabled = value; }

    public Plugin? Instance { get; set; }

    public PluginStage? ErrorStage { get; set; }

    public string? Error { get; set; }

    public IReadOnlyList<string> Diagnostics { get; set; } = [];

    public bool Compiling { get; set; }

    public int Order { get; set; }

    public bool Initialized { get; set; }

    public AssemblyLoadContext? Context { get; set; }
}
