using System.Drawing;
using System.Numerics;

namespace EmberHUD.Host.Icons;

internal sealed class SvgIconStore
{
    const int Capacity = 256;
    const int MaxQueued = 64;

    readonly record struct Request(string Icon, int Pixels, int Colour, int Accent);

    sealed record Rendered(Request Request, DecodedImage? Image, string? Failure, bool SkiaMissing);

    readonly IReadOnlyDictionary<string, string> _sources;
    readonly TextureUpload _upload;
    readonly Func<string, Sprite?> _resolve;
    readonly Action<string> _release;
    readonly Func<string, int, DecodedImage> _rasterize;
    readonly BackgroundJob<Rendered> _job;
    readonly Queue<Request> _queue = new();
    readonly HashSet<Request> _queued = [];
    readonly Dictionary<Request, LinkedListNode<(Request Request, string Key)>> _cache = [];
    readonly LinkedList<(Request Request, string Key)> _recent = new();
    readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    bool _off;

    public IReadOnlyList<string> Names { get; }

    public SvgIconStore(IReadOnlyDictionary<string, string> sources, TextureUpload upload, Func<string, Sprite?> resolve, Action<string> release, Func<string, int, DecodedImage>? rasterize = null, bool inline = false)
    {
        _sources = sources;
        foreach (var name in sources.Keys) _names[name] = name;
        Names = [.. sources.Keys.Order(StringComparer.Ordinal)];
        _upload = upload;
        _resolve = resolve;
        _release = release;
        _rasterize = rasterize ?? SvgIcons.Rasterize;
        _job = new BackgroundJob<Rendered>(inline);
    }

    public static int Bucket(int screenPixels) => (int)Math.Clamp(BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(screenPixels, 1, 1 << 16) * 2), 16, 256);

    public Sprite? Resolve(string icon, int screenPixels, Color colour, Color accent)
    {
        if (_off) return null;
        var request = new Request(icon, Bucket(screenPixels), colour.ToArgb() & 0xFFFFFF, accent.ToArgb() & 0xFFFFFF);
        return _cache.TryGetValue(request, out var node) ? Touch(node) : Miss(request);
    }

    Sprite? Touch(LinkedListNode<(Request Request, string Key)> node)
    {
        _recent.Remove(node);
        _recent.AddFirst(node);
        return _resolve(node.Value.Key);
    }

    Sprite? Miss(Request request)
    {
        if (_failed.Contains(request.Icon)) return null;
        if (!_names.TryGetValue(request.Icon, out var name))
        {
            _failed.Add(request.Icon);
            Log.Warn($"svg icon {request.Icon} - not found");
            return null;
        }
        request = request with { Icon = name };
        if (_cache.TryGetValue(request, out var node)) return Touch(node);
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
            return new Rendered(request, _rasterize(SvgIcons.Recolor(svg, Color.FromArgb(request.Colour), Color.FromArgb(request.Accent)), request.Pixels), null, false);
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
        var failure = done.Failure ?? "";
        if (done.Image is not { } image || !_upload(key, image.Rgba, image.Width, image.Height, out failure))
        {
            if (_failed.Add(request.Icon)) Log.Warn($"svg icon {request.Icon} - {failure}");
            return;
        }

        _cache[request] = _recent.AddFirst((request, key));
        while (_cache.Count > Capacity)
        {
            var oldest = _recent.Last!.Value;
            _recent.RemoveLast();
            _cache.Remove(oldest.Request);
            _release(oldest.Key);
        }
    }
}
