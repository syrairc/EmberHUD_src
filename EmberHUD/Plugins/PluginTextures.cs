using EmberHUD.Host.Icons;

namespace EmberHUD.Host.Plugins;

internal sealed class PluginTextures(Action<Action> beforeFrame, TextureUpload upload, Func<string, Sprite?> resolve, Action<string> release)
{
    readonly Dictionary<string, Dictionary<string, string>> _owned = [];
    readonly HashSet<string> _warned = [];

    public static string Key(string? plugin, string key) => $"plugin:{plugin}/{key}";

    public void Upload(string? plugin, string key, byte[] rgba, int width, int height) => beforeFrame(() =>
    {
        var full = Key(plugin, key);
        if (upload(full, rgba, width, height, out var failure))
        {
            _warned.Remove(full);
            Owned(plugin)[key] = full;
        }
        else if (_warned.Add(full)) Log.Warn($"{plugin} texture {key} - {failure}");
    });

    public Sprite? Resolve(string? plugin, string key) => _owned.TryGetValue(plugin ?? "", out var keys) && keys.TryGetValue(key, out var full) ? resolve(full) : null;

    public void Release(string? plugin, string key) => beforeFrame(() =>
    {
        var full = Key(plugin, key);
        release(full);
        Owned(plugin).Remove(key);
        _warned.Remove(full);
    });

    public void ReleaseAll(string plugin) => beforeFrame(() =>
    {
        if (!_owned.Remove(plugin, out var keys)) return;
        foreach (var full in keys.Values) release(full);
    });

    Dictionary<string, string> Owned(string? plugin)
    {
        var name = plugin ?? "";
        if (!_owned.TryGetValue(name, out var keys)) _owned[name] = keys = [];
        return keys;
    }
}
