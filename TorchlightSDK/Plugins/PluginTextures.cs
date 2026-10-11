using System.Numerics;
using TorchlightSDK.Host.Icons;

namespace TorchlightSDK.Host.Plugins;

internal sealed class PluginTextures(Action<Action> beforeFrame, Func<string, byte[], int, int, bool> upload, TextureLookup lookup, Action<string> release, Func<string> uploadError)
{
    readonly Dictionary<string, HashSet<string>> _owned = [];
    readonly HashSet<string> _warned = [];

    public static string Key(string? plugin, string key) => $"plugin:{plugin}/{key}";

    public void Upload(string? plugin, string key, byte[] rgba, int width, int height) => beforeFrame(() =>
    {
        var full = Key(plugin, key);
        string? failure;
        try
        {
            failure = upload(full, rgba, width, height) ? null : $"upload failed - {uploadError()}";
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }
        if (failure is null)
        {
            _warned.Remove(full);
            Owned(plugin).Add(full);
        }
        else if (_warned.Add(full)) Log.Warn($"{plugin} texture {key} - {failure}");
    });

    public Sprite? Resolve(string? plugin, string key) => lookup(Key(plugin, key), out var texture) ? new Sprite(texture, Vector2.Zero, Vector2.One) : null;

    public void Release(string? plugin, string key) => beforeFrame(() =>
    {
        var full = Key(plugin, key);
        release(full);
        Owned(plugin).Remove(full);
        _warned.Remove(full);
    });

    public void ReleaseAll(string plugin) => beforeFrame(() =>
    {
        if (!_owned.Remove(plugin, out var keys)) return;
        foreach (var full in keys) release(full);
    });

    HashSet<string> Owned(string? plugin)
    {
        var name = plugin ?? "";
        if (!_owned.TryGetValue(name, out var keys)) _owned[name] = keys = [];
        return keys;
    }
}
