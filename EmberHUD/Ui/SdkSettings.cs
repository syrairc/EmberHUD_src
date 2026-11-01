
namespace EmberHUD.Host.Ui;

internal sealed class SdkSettings : PluginSettings
{
    public SdkHotkeys Hotkeys = new();
    public InputSettings Input = new();
    public PerformanceSettings Performance = new();
    public string Theme = ThemeStore.DefaultTheme;
    public bool AutoReload;
    public bool InteractiveOutOfFocus;
    public string GameIconsDirectory = "";
    public bool UpdaterEnabled;
    public int UpdateIntervalHours = 1;
    public bool OverwriteModifiedPlugins;
    public LogLevel LogMinimum = LogLevel.Info;
    public Dictionary<string, PluginState> Plugins = [];
    public Dictionary<string, Dictionary<string, Hotkey>> PluginHotkeys = [];

    public override void Draw() { }
}

internal sealed class SdkHotkeys
{
    public Hotkey Settings = new(Key.F11);
    public Hotkey Quit = new(Key.F12, KeyModifiers.Ctrl);
}

internal sealed class PluginState
{
    public bool Enabled;
}
