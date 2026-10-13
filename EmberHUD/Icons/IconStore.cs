using System.Numerics;
using System.Text.Json;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Icons;

internal delegate bool TextureLookup(string key, out ImTextureRef texture);

internal sealed class IconStore
{
    sealed record Decoded(string Name, DecodedImage? Image, string? Failure);

    readonly string _directory;
    readonly Dictionary<string, string> _files;

    public IReadOnlyList<string> Names { get; }
    readonly Func<string, byte[], int, int, bool> _upload;
    readonly TextureLookup _lookup;
    readonly BackgroundJob<Decoded> _job;
    readonly Queue<string> _queue = new();
    readonly HashSet<string> _requested = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _uploaded = new(StringComparer.OrdinalIgnoreCase);

    IconStore(string directory, Dictionary<string, string> files, Func<string, byte[], int, int, bool> upload, TextureLookup lookup, bool inline)
    {
        _directory = directory;
        _files = files;
        Names = [.. files.Keys.Order(StringComparer.Ordinal)];
        _upload = upload;
        _lookup = lookup;
        _job = new BackgroundJob<Decoded>(inline);
    }

    public static IconStore? Open(string directory, Func<string, byte[], int, int, bool> upload, TextureLookup lookup, bool inline = false)
    {
        try
        {
            var files = GameIconIndex.Parse(File.ReadAllText(Path.Combine(directory, "index.json")));
            if (files.Count > 0) return new IconStore(directory, files, upload, lookup, inline);
            Log.Warn($"game icons off - no icons in {directory}\\index.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn($"game icons off - {ex.Message}");
        }
        return null;
    }

    public Sprite? Resolve(string texture)
    {
        if (string.IsNullOrEmpty(texture)) return null;
        if (_uploaded.Contains(texture))
            return _lookup(Key(texture), out var handle) ? new Sprite(handle, Vector2.Zero, Vector2.One) : null;
        if (_files.ContainsKey(texture) && _requested.Add(texture)) _queue.Enqueue(texture);
        return null;
    }

    public void Pump()
    {
        if (_job.TakeFinished() is { } finished)
        {
            var result = finished.GetAwaiter().GetResult();
            var failure = result.Failure;
            if (result.Image is { } image && Upload(result.Name, image, ref failure)) _uploaded.Add(result.Name);
            else Log.Warn($"game icon {result.Name} - {failure ?? "texture upload failed"}");
        }

        if (_job.Running || !_queue.TryDequeue(out var next)) return;
        var path = Path.Combine(_directory, _files[next]);
        _job.Start(() => Decode(next, path));
    }

    bool Upload(string name, DecodedImage image, ref string? failure)
    {
        try
        {
            return _upload(Key(name), image.Rgba, image.Width, image.Height);
        }
        catch (Exception ex)
        {
            failure = ex.Message;
            return false;
        }
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

    static string Key(string texture) => "game:" + texture.ToLowerInvariant();
}
