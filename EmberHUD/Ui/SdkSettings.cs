using System.Reflection;

namespace EmberHUD.Host.Ui;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed class SdkSettings : PluginSettings
{
    public SdkHotkeys Hotkeys = new();
    public string Theme = "Torchlight";
    public bool AutoReload;
    public string GameIconsDirectory = "";
    public Dictionary<string, PluginState> Plugins = [];
    public Dictionary<string, Dictionary<string, Hotkey>> PluginHotkeys = [];
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed class SdkHotkeys
{
    public Hotkey Settings = new(Key.F11);
    public Hotkey Quit = new(Key.F12, KeyModifiers.Ctrl);
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed class PluginState
{
    public bool Enabled;
}
