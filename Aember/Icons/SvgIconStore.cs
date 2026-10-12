using System.Drawing;
using System.Numerics;

namespace Aember.Host.Icons;

internal sealed class SvgIconStore
{
    const int Capacity = 256;
    const int MaxQueued = 64;

    readonly record struct Request(string Icon, int Pixels, int Colour, int Accent);

    sealed record Rendered(Request Request, DecodedImage? Image, string? Failure, bool SkiaMissing);

    readonly IReadOnlyDictionary<string, string> _sources;
    readonly Func<string, byte[], int, int, bool> _upload;
    readonly TextureLookup _lookup;
    readonly Action<string> _release;
    readonly Func<string, int, DecodedImage> _rasterize;
    readonly BackgroundJob<Rendered> _job;
    readonly Queue<Request> _queue = new();
    readonly HashSet<Request> _queued = [];
    readonly Dictionary<Request, LinkedListNode<(Request Request, string Key)>> _cache = [];
    readonly LinkedList<(Request Request, string Key)> _recent = new();
    readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    bool _off;

    public IReadOnlyList<string> Names { get; }

    public SvgIconStore(IReadOnlyDictionary<string, string> sources, Func<string, byte[], int, int, bool> upload, TextureLookup lookup, Action<string> release, Func<string, int, DecodedImage>? rasterize = null, bool inline = false)
    {
        _sources = sources;
        Names = [.. sources.Keys.Order(StringComparer.Ordinal)];
        _upload = upload;
        _lookup = lookup;
        _release = release;
        _rasterize = rasterize ?? SvgRasterizer.Rasterize;
        _job = new BackgroundJob<Rendered>(inline);
    }

    public static int Bucket(int screenPixels) => (int)Math.Clamp(BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(screenPixels, 1, 1 << 16) * 2), 16, 256);

    public Sprite? Resolve(string icon, int screenPixels, Color colour, Color accent)
    {
        if (_off || _failed.Contains(icon)) return null;
        if (!_sources.ContainsKey(icon))
        {
            _failed.Add(icon);
            Log.Warn($"svg icon {icon} - not found");
            return null;
        }
        var request = new Request(icon, Bucket(screenPixels), colour.ToArgb() & 0xFFFFFF, accent.ToArgb() & 0xFFFFFF);
        if (_cache.TryGetValue(request, out var node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
            return Lookup(node.Value.Key);
        }
        if (_queue.Count < MaxQueued && _queued.Add(request)) _queue.Enqueue(request);
        return null;
    }

    public void Pump()
    {
        if (_job.TakeFinished() is { } finished) Finish(finished.GetAwaiter().GetResult());
        if (_off || _job.Running) return;
        while (_queue.TryDequeue(out var next))
        {
            if (_failed.Contains(next.Icon) || _cache.ContainsKey(next))
            {
                _queued.Remove(next);
                continue;
            }
            var svg = _sources[next.Icon];
            _job.Start(() => Render(next, svg));
            return;
        }
    }

    Rendered Render(Request request, string svg)
    {
        try
        {
            return new Rendered(request, _rasterize(SvgRecolor.Apply(svg, Color.FromArgb(request.Colour), Color.FromArgb(request.Accent)), request.Pixels), null, false);
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or BadImageFormatException or EntryPointNotFoundException)
        {
            return new Rendered(request, null, ex.Message, true);
        }
        catch (Exception ex)
        {
            return new Rendered(request, null, ex.Message, false);
        }
    }

    void Finish(Rendered done)
    {
        _queued.Remove(done.Request);
        if (done.SkiaMissing)
        {
            _off = true;
            _queue.Clear();
            _queued.Clear();
            Log.Warn($"svg icons off - SkiaSharp failed to load - {done.Failure}");
            return;
        }

        var request = done.Request;
        var key = $"{request.Icon}@{request.Pixels}#{request.Colour:X6}#{request.Accent:X6}";
        if (done.Image is not { } image || !Upload(key, image))
        {
            if (_failed.Add(request.Icon)) Log.Warn($"svg icon {request.Icon} - {done.Failure ?? "texture upload failed"}");
            return;
        }

        _cache[request] = _recent.AddFirst((request, key));
        while (_cache.Count > Capacity)
        {
            var oldest = _recent.Last!.Value;
            _recent.RemoveLast();
            _cache.Remove(oldest.Request);
            Release(oldest.Key);
        }
    }

    Sprite? Lookup(string key)
    {
        try
        {
            return _lookup(key, out var texture) ? new Sprite(texture, Vector2.Zero, Vector2.One) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    void Release(string key)
    {
        try
        {
            _release(key);
        }
        catch (Exception)
        {
        }
    }

    bool Upload(string key, DecodedImage image)
    {
        try
        {
            return _upload(key, image.Rgba, image.Width, image.Height);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
