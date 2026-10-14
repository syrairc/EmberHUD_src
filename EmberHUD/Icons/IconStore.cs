using System.Text.Json;

namespace EmberHUD.Host.Icons;

internal delegate bool TextureUpload(string key, byte[] rgba, int width, int height, out string failure);

internal sealed class IconStore
{
    sealed record Decoded(string Name, DecodedImage? Image, string? Failure);

    readonly string _directory;
    readonly Dictionary<string, string> _files;

    public IReadOnlyList<string> Names { get; }
    readonly TextureUpload _upload;
    readonly Func<string, Sprite?> _resolve;
    readonly BackgroundJob<Decoded> _job;
    readonly Queue<string> _queue = new();
    readonly HashSet<string> _requested = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _keys = new(StringComparer.OrdinalIgnoreCase);

    IconStore(string directory, Dictionary<string, string> files, TextureUpload upload, Func<string, Sprite?> resolve, bool inline)
    {
        _directory = directory;
        _files = files;
        Names = [.. files.Keys.Order(StringComparer.Ordinal)];
        _upload = upload;
        _resolve = resolve;
        _job = new BackgroundJob<Decoded>(inline);
    }

    public static IconStore? Open(string directory, TextureUpload upload, Func<string, Sprite?> resolve, bool inline = false)
    {
        try
        {
            var files = ParseIndex(File.ReadAllText(Path.Combine(directory, "index.json")));
            if (files.Count > 0) return new IconStore(directory, files, upload, resolve, inline);
            Log.Warn($"game icons off - no icons in {directory}\\index.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn($"game icons off - {ex.Message}");
        }
        return null;
    }

    public Sprite? Resolve(ReadOnlySpan<char> texture)
    {
        if (texture.IsEmpty) return null;
        if (_keys.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(texture, out var key)) return _resolve(key);
        if (!_files.GetAlternateLookup<ReadOnlySpan<char>>().ContainsKey(texture) || _requested.GetAlternateLookup<ReadOnlySpan<char>>().Contains(texture)) return null;
        var name = texture.ToString();
        _requested.Add(name);
        _queue.Enqueue(name);
        return null;
    }

    public void Pump()
    {
        if (_job.TakeFinished() is { } finished)
        {
            var result = finished.GetAwaiter().GetResult();
            var failure = result.Failure ?? "";
            var key = "game:" + result.Name.ToLowerInvariant();
            if (result.Image is { } image && _upload(key, image.Rgba, image.Width, image.Height, out failure)) _keys[result.Name] = key;
            else Log.Warn($"game icon {result.Name} - {failure}");
        }

        if (_job.Running || !_queue.TryDequeue(out var next)) return;
        var path = Path.Combine(_directory, _files[next]);
        _job.Start(() => Decode(next, path));
    }

    static Decoded Decode(string name, string path)
    {
        try
        {
            return PngDecoder.TryDecode(File.ReadAllBytes(path), out var image, out var failure) ? new Decoded(name, image, null) : new Decoded(name, null, failure);
        }
        catch (Exception ex)
        {
            return new Decoded(name, null, ex.Message);
        }
    }

    internal static Dictionary<string, string> ParseIndex(string json)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("icons", out var icons) || icons.ValueKind != JsonValueKind.Object) return files;

        foreach (var icon in icons.EnumerateObject())
        {
            if (icon.Value.ValueKind != JsonValueKind.Object || !icon.Value.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.String) continue;
            if (file.GetString() is { Length: > 0 } name && Path.GetFileName(name) == name) files[icon.Name] = name;
        }
        return files;
    }
}
