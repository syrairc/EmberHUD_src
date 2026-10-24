using EmberHUD.Host.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using EmberHUD.Host.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Plugins;

internal sealed class PluginHost(SdkSettings sdk, string root, Action<string>? unloaded = null, PluginLogLevels? pluginLevels = null) : IDisposable
{
    static readonly SourceLog Log = EmberHUD.Log.For("Loader");

    public PluginLogLevels PluginLevels { get; } = pluginLevels ?? new();

    readonly string _config = Path.Combine(root, "Config");
    readonly string _cache = Path.Combine(root, "Cache");
    readonly List<PluginSlot> _slots = [];
    PluginSlot[] _renderOrder = [];
    readonly ConcurrentQueue<(PluginSlot Slot, CompileResult Result)> _compiled = new();
    readonly Dictionary<string, string> _written = new() { [SdkPath(root)] = SettingsJson.Serialize(sdk) };
    readonly Dictionary<string, string> _warned = [];
    readonly Dictionary<PluginSlot, FileSystemWatcher> _watchers = [];
    readonly HashSet<PluginSlot> _watchFailed = [];
    bool _warnedCache;
    bool _watching;

    internal long SavedAt = Environment.TickCount64;
    bool _settingsOpen;

    internal static string SdkPath(string root) => Path.Combine(root, "Config", "sdk.json");

    public void MarkSettingsOpen() => _settingsOpen = true;

    internal MethodInfo? ClearSerializerCache { get; set; } = typeof(JsonSerializerOptions).Assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler")?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    internal IReadOnlyCollection<FileSystemWatcher> Watchers => _watchers.Values;

    public IReadOnlyList<PluginSlot> Slots => _slots;

    public void Discover(string pluginsDirectory)
    {
        foreach (var directory in PluginCompiler.Plugins(pluginsDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!sdk.Plugins.TryGetValue(name, out var state)) sdk.Plugins[name] = state = new PluginState();
            var slot = new PluginSlot(name, directory, state);
            _slots.Add(slot);
            StartCompile(slot);
        }
        SortForRender();
    }

    public void TickAll()
    {
        while (_compiled.TryDequeue(out var done)) Load(done.Slot, done.Result);
        foreach (var slot in _slots)
        {
            var changedAt = Interlocked.Read(ref slot.ChangedAt);
            if (changedAt != 0 && !slot.Compiling && Environment.TickCount64 - changedAt >= 500 && Interlocked.CompareExchange(ref slot.ChangedAt, 0, changedAt) == changedAt) Reload(slot);
        }
        if (sdk.AutoReload != _watching) SyncWatchers();
        foreach (var slot in _slots)
            if (slot.Enabled) Call(slot, PluginStage.Tick, plugin =>
            {
                plugin.DispatchHotkeys();
                plugin.Tick();
            });
        if (Environment.TickCount64 - SavedAt >= (_settingsOpen ? 2000 : 30000)) SaveAll();
    }

    internal DrawStats Stats { get; set; } = new();

    public void RenderAll()
    {
        var inFrame = InFrame();
        if (inFrame) Draw.SplitLayers();
        foreach (var slot in _renderOrder)
            if (slot.Enabled)
            {
                var before = inFrame ? Draw.Background.VtxBuffer.Size : 0;
                Call(slot, PluginStage.Render, plugin =>
                {
                    try { plugin.Render(); }
                    finally { Ground.FlushGroups(); }
                });
                if (inFrame) Stats.Add(slot.Name, Draw.Background.VtxBuffer.Size - before);
            }
        if (inFrame) Draw.MergeLayers();
        if (Stats.Report(Environment.TickCount64, Game.State.Projectiles.Length) is { } heavy) Log.Warn(heavy);
    }

    public void DrawSettings(PluginSlot slot) => Call(slot, PluginStage.Settings, plugin =>
    {
        EmberHUD.Ui.Controls.SettingsPlugin = plugin;
        try { plugin.DrawSettings(); }
        finally { EmberHUD.Ui.Controls.SettingsPlugin = null; }
    });

    public void SetEnabled(PluginSlot slot, bool enabled)
    {
        if (slot.Enabled == enabled) return;
        slot.Enabled = enabled;
        if (enabled) Enable(slot);
        else
        {
            if (slot.Initialized) Call(slot, PluginStage.Disable, plugin => plugin.OnDisable());
            SaveAll();
        }
    }

    public void SaveAll()
    {
        SavedAt = Environment.TickCount64;
        _settingsOpen = false;
        foreach (var slot in _slots) SyncHotkeys(slot);
        foreach (var slot in _slots)
            if (slot.Instance?.SettingsObject is { } settings) Guard(slot, PluginStage.Settings, () => Write(SettingsPath(slot), SettingsJson.Serialize(settings)));
        Write(SdkPath(root), SettingsJson.Serialize(sdk));
    }

    public void Dispose()
    {
        SaveAll();
        foreach (var slot in _slots) Unload(slot);
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
    }

    public void Reload(PluginSlot slot)
    {
        if (slot.Compiling) return;
        Interlocked.Exchange(ref slot.ChangedAt, 0);
        SaveAll();
        Unload(slot);
        StartCompile(slot);
    }

    public void WriteDefaults(PluginSlot slot)
    {
        if (slot.Instance?.SettingsObject is not { } settings) return;
        var path = PluginCompiler.DefaultsPath(slot.Directory);
        _written.Remove(path);
        if (Write(path, SettingsJson.Serialize(settings))) Log.Info($"{slot.Name} - wrote {path}");
    }

    public void ResetSettings(PluginSlot slot)
    {
        var path = SettingsPath(slot);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"{slot.Name} - could not delete {path} - {ex.Message}");
            return;
        }
        _written.Remove(path);
        if (slot.Instance is { } plugin) Guard(slot, PluginStage.Settings, () => LoadSettings(slot, plugin, null));
        Log.Info($"{slot.Name} - settings reset to defaults");
    }

    public void ClearCache() => PluginCompiler.ClearCache(_cache);

    void StartCompile(PluginSlot slot)
    {
        slot.Compiling = true;
        Task.Run(() =>
        {
            CompileResult result;
            try
            {
                result = PluginCompiler.Build(slot.Name, slot.Directory, _cache);
            }
            catch (Exception ex)
            {
                result = new CompileResult(null, [$"{ex.GetType().Name}: {ex.Message}"]);
            }
            _compiled.Enqueue((slot, result));
        });
    }

    void SyncWatchers()
    {
        _watching = sdk.AutoReload;
        foreach (var slot in _slots)
        {
            var watching = _watchers.ContainsKey(slot);
            if (sdk.AutoReload == watching) continue;
            if (watching)
            {
                _watchers.Remove(slot, out var old);
                old!.Dispose();
                Interlocked.Exchange(ref slot.ChangedAt, 0);
                continue;
            }
            Watch(slot);
        }
    }

    void Watch(PluginSlot slot)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(slot.Directory, "*.cs") { IncludeSubdirectories = true };
            void Touch(object sender, FileSystemEventArgs e) => Interlocked.Exchange(ref slot.ChangedAt, Environment.TickCount64);
            watcher.Changed += Touch;
            watcher.Created += Touch;
            watcher.Deleted += Touch;
            watcher.Renamed += Touch;
            watcher.EnableRaisingEvents = true;
            _watchers[slot] = watcher;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            watcher?.Dispose();
            if (_watchFailed.Add(slot)) Log.Warn($"{slot.Name} - could not watch {slot.Directory} - {ex.Message}");
        }
    }

    void Load(PluginSlot slot, CompileResult result)
    {
        slot.Compiling = false;
        slot.Diagnostics = result.Diagnostics;
        if (result.Assembly is not { } bytes)
        {
            slot.ErrorStage = PluginStage.Compile;
            slot.Error = "compile failed";
            Log.Error($"{slot.Name} compile failed{Environment.NewLine}{string.Join(Environment.NewLine, result.Diagnostics)}");
            return;
        }
        var context = new AssemblyLoadContext(slot.Name, isCollectible: true);
        slot.Context = context;
        Guard(slot, PluginStage.Initialize, () =>
        {
            using var stream = new MemoryStream(bytes);
            var plugin = (Plugin)Activator.CreateInstance(PluginCompiler.FindPluginType(context.LoadFromStream(stream).GetTypes()))!;
            plugin.Name = slot.Name;
            PluginLevels.Set(slot.Name, ReadLogLevel(slot, plugin));
            LoadSettings(slot, plugin, PluginCompiler.ReadText(SettingsPath(slot)));
            slot.Order = plugin.Order;
            slot.Instance = plugin;
        });
        SortForRender();
        if (slot.Enabled) Enable(slot);
    }

    static LogLevel ReadLogLevel(PluginSlot slot, Plugin plugin)
    {
        try
        {
            return LogLevels.Valid(plugin.DefaultLogLevel);
        }
        catch (Exception ex)
        {
            EmberHUD.Log.For(slot.Name).Warn($"DefaultLogLevel failed, using Info - {PluginCompiler.Unwrap(ex).Message}");
            return LogLevel.Info;
        }
    }

    void SortForRender() => _renderOrder = [.. _slots.OrderBy(slot => slot.Order).ThenBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase)];

    void LoadSettings(PluginSlot slot, Plugin plugin, string? user)
    {
        if (plugin.SettingsType is not { } type) return;
        var settings = SettingsJson.Load(type, PluginCompiler.ReadDefaults(slot.Directory), user, problem => Log.Warn($"{slot.Name} settings - {problem}"));
        plugin.SettingsObject = settings;
        _written[SettingsPath(slot)] = user ?? SettingsJson.Serialize(settings);
    }

    void Enable(PluginSlot slot)
    {
        if (!slot.Initialized)
        {
            slot.Initialized = Call(slot, PluginStage.Initialize, plugin => plugin.Initialize());
            if (slot.Initialized) ApplyHotkeys(slot);
        }
        if (slot.Initialized) Call(slot, PluginStage.Enable, plugin => plugin.OnEnable());
    }

    void ApplyHotkeys(PluginSlot slot)
    {
        if (slot.Instance is not { } plugin || !sdk.PluginHotkeys.TryGetValue(slot.Name, out var saved)) return;
        foreach (var binding in plugin.Hotkeys)
            if (saved.TryGetValue(binding.Id, out var key)) binding.Value = key;
    }

    void SyncHotkeys(PluginSlot slot)
    {
        if (slot.Instance is not { } plugin || !slot.Initialized) return;
        var saved = sdk.PluginHotkeys.GetValueOrDefault(slot.Name) ?? [];
        foreach (var binding in plugin.Hotkeys)
        {
            if (binding.Value == binding.Default) saved.Remove(binding.Id);
            else saved[binding.Id] = binding.Value;
        }
        if (saved.Count == 0) sdk.PluginHotkeys.Remove(slot.Name);
        else sdk.PluginHotkeys[slot.Name] = saved;
    }

    internal string? ConflictFor(Hotkey key, HotkeyBinding? self)
    {
        if (key.Key == Key.None) return null;
        if (sdk.Hotkeys.Settings == key) return "EmberHUD / Settings window";
        if (sdk.Hotkeys.Quit == key) return "EmberHUD / Quit";
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.Enabled || !slot.Initialized || slot.Instance is not { } instance) continue;
            var bindings = instance.Hotkeys;
            for (var j = 0; j < bindings.Count; j++)
                if (!ReferenceEquals(bindings[j], self) && bindings[j].Value == key) return $"{slot.Name} / {bindings[j].Id}";
        }
        return null;
    }

    void Unload(PluginSlot slot)
    {
        if (slot.Enabled && slot.Initialized) Call(slot, PluginStage.Disable, plugin => plugin.OnDisable());
        if (slot.Initialized) Call(slot, PluginStage.Dispose, plugin => plugin.Dispose());
        slot.Instance = null;
        PluginLevels.Remove(slot.Name);
        slot.Initialized = false;
        slot.ErrorStage = null;
        slot.Error = null;
        slot.Diagnostics = [];
        slot.Context?.Unload();
        slot.Context = null;
        ClearJsonCache();
        unloaded?.Invoke(slot.Name);
    }

    void ClearJsonCache()
    {
        if (ClearSerializerCache is not { } clear)
        {
            if (!_warnedCache) Log.Warn("serializer cache hook not found - unloaded plugins may stay in memory");
            _warnedCache = true;
            return;
        }
        try
        {
            clear.Invoke(null, [null]);
        }
        catch (Exception ex)
        {
            Log.Warn($"serializer cache clear failed - {PluginCompiler.Unwrap(ex).Message}");
        }
    }

    bool Call(PluginSlot slot, PluginStage stage, Action<Plugin> call) =>
        slot.Instance is { } plugin && slot.ErrorStage is null && Guard(slot, stage, call, plugin);

    static bool InFrame()
    {
        var context = ImGui.GetCurrentContext();
        return !context.IsNull && context.WithinFrameScope;
    }

    static bool Guard(PluginSlot slot, PluginStage stage, Action action) => Guard(slot, stage, static run => run(), action);

    static bool Guard<T>(PluginSlot slot, PluginStage stage, Action<T> action, T argument)
    {
        var inFrame = InFrame();
        var state = default(ImGuiErrorRecoveryState);
        if (inFrame)
        {
            ImGui.PushID(slot.Name);
            ImGuiP.ErrorRecoveryStoreState(ref state);
        }
        var outer = EmberHUD.Log.CurrentPlugin.Value;
        var retag = outer != slot.Name;
        if (retag) EmberHUD.Log.CurrentPlugin.Value = slot.Name;
        try
        {
            action(argument);
            return true;
        }
        catch (Exception ex)
        {
            ex = PluginCompiler.Unwrap(ex);
            if (slot.ErrorStage is null)
            {
                slot.ErrorStage = stage;
                slot.Error = $"{ex.GetType().Name}: {ex.Message}";
            }
            Log.Error(ex, $"{slot.Name} {stage}");
            return false;
        }
        finally
        {
            if (retag) EmberHUD.Log.CurrentPlugin.Value = outer;
            if (inFrame)
            {
                ImGuiP.ErrorRecoveryTryToRecoverState(ref state);
                Draw.ResetLayer();
                ImGui.PopID();
            }
        }
    }

    bool Write(string path, string text)
    {
        if (_written.TryGetValue(path, out var last) && last == text) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", text);
            File.Move(path + ".tmp", path, overwrite: true);
            _written[path] = text;
            _warned.Remove(path);
            return true;
        }
        catch (Exception ex)
        {
            _written.Remove(path);
            if (!_warned.TryGetValue(path, out var warnedText) || warnedText != text) Log.Warn($"could not save {path} - {ex.Message}");
            _warned[path] = text;
            return false;
        }
    }

    string SettingsPath(PluginSlot slot) => Path.Combine(_config, "Plugins", slot.Name + ".json");
}
