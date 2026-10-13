using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using EmberHUD;

namespace Mcp;

internal sealed class Watches
{
    public const int MaxWatches = 16;
    public const int MaxLength = 4096;
    readonly Lock _gate = new();
    readonly Dictionary<int, Watch> _watches = [];
    int _next = 1;

    public int Count
    {
        get
        {
            lock (_gate) return _watches.Count;
        }
    }

    public int Start(string label, nint at, int length)
    {
        if (length < 1 || length > MaxLength) throw new ToolError($"length must be between 1 and {MaxLength}");
        var watch = new Watch(label, at, length);
        if (Memory.Read(at, watch.Last) != length) throw new ToolError($"nothing readable at {Hex.Of(at)}");
        watch.Readable = true;
        watch.Last.CopyTo(watch.Baseline, 0);
        lock (_gate)
        {
            if (_watches.Count >= MaxWatches) throw new ToolError($"at most {MaxWatches} watches at once, stop one first");
            var id = _next++;
            _watches[id] = watch;
            return id;
        }
    }

    public void Sample()
    {
        lock (_gate)
            foreach (var watch in _watches.Values)
            {
                watch.Samples++;
                if (Memory.Read(watch.At, watch.Scratch) != watch.Length)
                {
                    watch.Readable = false;
                    continue;
                }
                if (!watch.Readable)
                {
                    watch.Readable = true;
                    watch.Scratch.CopyTo(watch.Last, 0);
                    watch.Scratch.CopyTo(watch.Baseline, 0);
                    Array.Clear(watch.Dirty);
                    continue;
                }
                var changed = false;
                for (var i = 0; i < watch.Length; i++)
                {
                    if (watch.Scratch[i] == watch.Last[i]) continue;
                    watch.Dirty[i] = true;
                    changed = true;
                }
                if (changed) watch.ChangedSamples++;
                watch.Scratch.CopyTo(watch.Last, 0);
            }
    }

    public JsonObject Read(int id)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(id, out var watch)) throw new ToolError($"no watch with id {id}");
            var changes = new JsonArray();
            for (var i = 0; i < watch.Length; i++)
            {
                if (!watch.Dirty[i]) continue;
                var end = i;
                while (end < watch.Length && watch.Dirty[end]) end++;
                changes.Add(new JsonObject
                {
                    ["offset"] = Hex.Of(i),
                    ["was"] = Convert.ToHexString(watch.Baseline, i, end - i),
                    ["now"] = Convert.ToHexString(watch.Last, i, end - i),
                });
                i = end;
            }
            var result = new JsonObject
            {
                ["id"] = id,
                ["label"] = watch.Label,
                ["address"] = Hex.Of(watch.At),
                ["length"] = watch.Length,
                ["readable"] = watch.Readable,
                ["samples"] = watch.Samples,
                ["changedSamples"] = watch.ChangedSamples,
                ["changes"] = changes,
                ["current"] = Convert.ToHexString(watch.Last),
            };
            watch.Last.CopyTo(watch.Baseline, 0);
            Array.Clear(watch.Dirty);
            watch.Samples = 0;
            watch.ChangedSamples = 0;
            return result;
        }
    }

    public bool Stop(int id)
    {
        lock (_gate) return _watches.Remove(id);
    }

    public void StopAll()
    {
        lock (_gate) _watches.Clear();
    }

    sealed class Watch(string label, nint at, int length)
    {
        public readonly string Label = label;
        public readonly nint At = at;
        public readonly int Length = length;
        public readonly byte[] Baseline = new byte[length];
        public readonly byte[] Last = new byte[length];
        public readonly byte[] Scratch = new byte[length];
        public readonly bool[] Dirty = new bool[length];
        public bool Readable;
        public int Samples;
        public int ChangedSamples;
    }
}
