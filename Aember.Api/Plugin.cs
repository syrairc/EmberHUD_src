namespace Aember;

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
