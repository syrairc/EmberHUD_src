using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using TorchlightSDK.Host.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Host.Plugins;

internal sealed class PluginHost(SdkSettings sdk, string root) : IDisposable
{
    readonly string _config = Path.Combine(root, "Config");
    readonly string _cache = Path.Combine(root, "Cache");
    readonly List<PluginSlot> _slots = [];
    readonly ConcurrentQueue<(PluginSlot Slot, CompileResult Result)> _compiled = new();
    readonly Dictionary<string, string> _written = new() { [Path.Combine(root, "Config", "sdk.json")] = SettingsJson.Serialize(sdk) };
    readonly Dictionary<string, string> _warned = [];
    readonly Dictionary<PluginSlot, FileSystemWatcher> _watchers = [];
    long _savedAt = Environment.TickCount64;
    readonly HashSet<PluginSlot> _watchFailed = [];
    bool _warnedCache;

    internal MethodInfo? ClearSerializerCache { get; set; } = typeof(JsonSerializerOptions).Assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler")?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    internal IReadOnlyCollection<FileSystemWatcher> Watchers => _watchers.Values;

    public IReadOnlyList<PluginSlot> Slots => _slots;

    public void Discover(string pluginsDirectory)
    {
        foreach (var directory in PluginCache.Plugins(pluginsDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!sdk.Plugins.TryGetValue(name, out var state)) sdk.Plugins[name] = state = new PluginState();
            var slot = new PluginSlot(name, directory, state);
            _slots.Add(slot);
            StartCompile(slot);
        }
    }

    public void TickAll()
    {
        while (_compiled.TryDequeue(out var done)) Load(done.Slot, done.Result);
        foreach (var slot in _slots)
        {
            var changedAt = Interlocked.Read(ref slot.ChangedAt);
            if (changedAt != 0 && !slot.Compiling && Environment.TickCount64 - changedAt >= 500 && Interlocked.CompareExchange(ref slot.ChangedAt, 0, changedAt) == changedAt) Reload(slot);
        }
        SyncWatchers();
        foreach (var slot in _slots)
            if (slot.Enabled) Call(slot, PluginStage.Tick, plugin => plugin.Tick());
        if (Environment.TickCount64 - _savedAt >= 2000) SaveAll();
    }

    public void RenderAll()
    {
        var inFrame = InFrame();
        if (inFrame) Draw.SplitLayers();
        foreach (var slot in _slots.Where(slot => slot.Enabled).OrderBy(slot => slot.Order).ThenBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase))
            Call(slot, PluginStage.Render, plugin => plugin.Render());
        if (inFrame) Draw.MergeLayers();
    }

    public void DrawSettings(PluginSlot slot) => Call(slot, PluginStage.Settings, plugin => plugin.DrawSettings());

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
        _savedAt = Environment.TickCount64;
        foreach (var slot in _slots)
            if (slot.Instance?.SettingsObject is { } settings) Guard(slot, PluginStage.Settings, () => Write(SettingsPath(slot), SettingsJson.Serialize(settings)));
        Write(Path.Combine(_config, "sdk.json"), SettingsJson.Serialize(sdk));
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
        var path = Path.Combine(slot.Directory, "defaults.json");
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

    public void ClearCache()
    {
        var failed = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cache, "*.dll"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                    Log.Warn($"could not delete {file} - {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed++;
            Log.Warn($"could not read {_cache} - {ex.Message}");
        }
        if (failed == 0) Log.Info("compile cache cleared");
    }

    void StartCompile(PluginSlot slot)
    {
        slot.Compiling = true;
        Task.Run(() =>
        {
            CompileResult result;
            try
            {
                result = PluginCache.Build(slot.Name, slot.Directory, _cache);
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
            var types = context.LoadFromStream(stream).GetTypes().Where(type => type.IsSubclassOf(typeof(Plugin)) && !type.IsAbstract).ToList();
            if (types.Count != 1) throw new InvalidOperationException($"found {types.Count} Plugin subclasses, expected one");
            var plugin = (Plugin)Activator.CreateInstance(types[0])!;
            plugin.Name = slot.Name;
            plugin.Directory = slot.Directory;
            LoadSettings(slot, plugin, ReadText(SettingsPath(slot)));
            slot.Order = plugin.Order;
            slot.Instance = plugin;
        });
        if (slot.Enabled) Enable(slot);
    }

    void LoadSettings(PluginSlot slot, Plugin plugin, string? user)
    {
        if (plugin.SettingsType is not { } type) return;
        var settings = SettingsJson.Load(type, ReadText(Path.Combine(slot.Directory, "defaults.json")), user, problem => Log.Warn($"{slot.Name} settings - {problem}"));
        plugin.SettingsObject = settings;
        _written[SettingsPath(slot)] = user ?? SettingsJson.Serialize(settings);
    }

    void Enable(PluginSlot slot)
    {
        if (!slot.Initialized) slot.Initialized = Call(slot, PluginStage.Initialize, plugin => plugin.Initialize());
        if (slot.Initialized) Call(slot, PluginStage.Enable, plugin => plugin.OnEnable());
    }

    void Unload(PluginSlot slot)
    {
        if (slot.Enabled && slot.Initialized) Call(slot, PluginStage.Disable, plugin => plugin.OnDisable());
        if (slot.Initialized) Call(slot, PluginStage.Dispose, plugin => plugin.Dispose());
        slot.Instance = null;
        slot.Initialized = false;
        slot.ErrorStage = null;
        slot.Error = null;
        slot.Diagnostics = [];
        slot.Context?.Unload();
        slot.Context = null;
        ClearJsonCache();
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
            Log.Warn($"serializer cache clear failed - {(ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message}");
        }
    }

    bool Call(PluginSlot slot, PluginStage stage, Action<Plugin> call) =>
        slot.Instance is { } plugin && slot.ErrorStage is null && Guard(slot, stage, () => call(plugin));

    static bool InFrame()
    {
        var context = ImGui.GetCurrentContext();
        return !context.IsNull && context.WithinFrameScope;
    }

    static bool Guard(PluginSlot slot, PluginStage stage, Action action)
    {
        var inFrame = InFrame();
        var state = default(ImGuiErrorRecoveryState);
        if (inFrame)
        {
            ImGui.PushID(slot.Name);
            ImGuiP.ErrorRecoveryStoreState(ref state);
        }
        var outer = Log.CurrentPlugin.Value;
        Log.CurrentPlugin.Value = slot.Name;
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            if (ex is TargetInvocationException { InnerException: { } inner }) ex = inner;
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
            Log.CurrentPlugin.Value = outer;
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

    static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
