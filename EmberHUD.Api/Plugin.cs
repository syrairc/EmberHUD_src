namespace EmberHUD;

public abstract class Plugin
{
    public string Name { get; internal set; } = "";

    public string Directory { get; internal set; } = "";

    public virtual int Order => 0;

    public virtual void Initialize() { }

    public virtual void OnEnable() { }

    public virtual void OnDisable() { }

    public virtual void Tick() { }

    public virtual void Render() { }

    public virtual void DrawSettings() { }

    public virtual void Dispose() { }

    readonly List<HotkeyBinding> _hotkeys = [];

    public IReadOnlyList<HotkeyBinding> Hotkeys => _hotkeys;

    protected void RegisterHotkey(string name, string description, Action callback, Hotkey? defaultKey = null, string? category = null)
    {
        var binding = new HotkeyBinding(name, description, callback, defaultKey ?? default, category);
        if (_hotkeys.Any(existing => existing.Id == binding.Id)) throw new InvalidOperationException($"hotkey '{binding.Id}' is already registered");
        _hotkeys.Add(binding);
    }

    internal void DispatchHotkeys()
    {
        foreach (var binding in _hotkeys)
            if (Input.Pressed(binding.Value)) binding.Callback();
    }

    internal virtual PluginSettings? SettingsObject { get => null; set { } }

    internal virtual Type? SettingsType => null;
}

public abstract class Plugin<TSettings> : Plugin where TSettings : PluginSettings, new()
{
    public TSettings Settings { get; internal set; } = new();

    public override void DrawSettings() => Settings.Draw();

    internal override PluginSettings? SettingsObject { get => Settings; set => Settings = (TSettings)value!; }

    internal override Type? SettingsType => typeof(TSettings);
}

public abstract class PluginSettings
{
    public virtual void Draw() => SettingsDrawer.DrawNodes(this);
}
