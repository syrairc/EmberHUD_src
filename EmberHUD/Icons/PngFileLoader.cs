namespace EmberHUD.Host.Icons;

internal sealed class PngFileLoader(Action<string?, string, byte[], int, int> upload)
{
    readonly HashSet<string> _warned = [];

    public bool Load(string key, string path)
    {
        try
        {
            if (PngDecoder.TryDecode(File.ReadAllBytes(path), out var image, out var failure))
            {
                upload(Log.CurrentPlugin.Value, key, image.Rgba, image.Width, image.Height);
                return true;
            }
            Warn(path, failure);
        }
        catch (Exception exception)
        {
            Warn(path, exception.Message);
        }
        return false;
    }

    void Warn(string path, string failure)
    {
        if (_warned.Add(path)) Log.Warn($"png {path} - {failure}");
    }
}
