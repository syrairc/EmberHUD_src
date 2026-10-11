namespace TorchlightSDK;

public static class Textures
{
    internal static Action<string?, string, byte[], int, int>? Uploader { get; set; }

    internal static Func<string?, string, Sprite?>? Resolver { get; set; }

    internal static Action<string?, string>? Releaser { get; set; }

    public static void Upload(string key, byte[] rgba, int width, int height) => Uploader?.Invoke(Log.CurrentPlugin.Value, key, rgba, width, height);

    public static bool TryGet(string key, out Sprite sprite)
    {
        if (Resolver?.Invoke(Log.CurrentPlugin.Value, key) is { } found)
        {
            sprite = found;
            return true;
        }
        sprite = default;
        return false;
    }

    public static void Release(string key) => Releaser?.Invoke(Log.CurrentPlugin.Value, key);
}
